using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // JsonSaveStore, SaveData, OverlayGate
using LoveAlgo.Events; // ShowSaveLoadCommand, LoadGameCommand, SaveRequestedEvent
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 세이브/로드 팝업 뷰(*View, ADR-013 Overlay). <see cref="ShowSaveLoadCommand"/>로 표시,
    /// Grid에 슬롯을 런타임 스폰(ScheduleView 패턴)하고 페이지 단위로 <see cref="JsonSaveStore"/>를 읽어 바인딩.
    /// Load 모드: 데이터 슬롯 클릭→<see cref="LoadGameCommand"/>+닫기. Save 모드: 클릭→<see cref="SaveRequestedEvent"/>+갱신.
    /// SetVisible/OverlayGate/누수가드 = SettingsView 1:1(게임플레이 입력 차단).
    /// 슬롯0(자동저장)은 양 모드 첫 칸으로 노출(<see cref="SlotForCell"/> 0-base): Load=해당 시점 로드,
    /// Save=덮어쓰기 금지(안내 모달만). Save 모드는 팝업 표시 전 <see cref="PrimeThumbnailCacheCommand"/>로
    /// 썸네일을 예열해 슬롯 클릭 깜빡임을 없앤다.
    /// </summary>
    public class SaveLoadView : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;
        [Tooltip("슬롯을 스폰할 컨테이너(GridLayoutGroup).")]
        [SerializeField] Transform slotContainer;
        [SerializeField] SaveLoadSlot slotPrefab;

        [Header("페이지네이션/액션")]
        [SerializeField] Button prevButton;
        [SerializeField] Button nextButton;
        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text pageNumberText;

        [Tooltip("페이지당 슬롯 수(Grid 셀 수와 일치).")]
        [SerializeField] int slotsPerPage = 6;
        [Tooltip("총 페이지 수. 슬롯 = 0 ~ slotsPerPage*pageCount-1 (0=자동저장).")]
        [SerializeField] int pageCount = 3;

        // ── 확인 모달 문구(자주 조정 → Inspector에서 바로 수정) ──
        [Header("자동저장 슬롯 안내 모달(Save 모드 클릭 시)")]
        [SerializeField] string autoSaveModalTitle = "자동저장 슬롯";
        [TextArea] [SerializeField] string autoSaveModalMessage = "자동저장 전용 슬롯입니다.\n수동 저장은 다른 슬롯을 사용해 주세요.";
        [SerializeField] string autoSaveModalConfirm = "확인";

        [Header("저장 확인 모달(Save 모드, 일반 슬롯)")]
        [Tooltip("빈 슬롯 클릭 시.")]
        [SerializeField] string newSaveModalTitle = "저장";
        [TextArea] [SerializeField] string newSaveModalMessage = "이 슬롯에 저장하시겠습니까?";
        [Tooltip("데이터가 있는 슬롯 클릭 시.")]
        [SerializeField] string overwriteModalTitle = "덮어쓰기";
        [TextArea] [SerializeField] string overwriteModalMessage = "이미 데이터가 있습니다.\n덮어씌우시겠습니까?";
        [SerializeField] string saveModalConfirm = "예";
        [SerializeField] string saveModalCancel = "아니오";

        [Tooltip("Save 팝업 표시 전 썸네일 캐시 예열을 기다릴 최대 프레임(컨트롤러 없으면 이만큼 후 그냥 표시).")]
        [SerializeField] int primeGuardFrames = 10;

        readonly List<SaveLoadSlot> _slots = new();
        readonly List<UnityEngine.Object> _thumbnails = new(); // 페이지별 생성 텍스처/스프라이트(누수 방지)
        SaveLoadMode _mode = SaveLoadMode.Load;
        int _page;
        bool _visible;
        IDisposable _showSub;
        IDisposable _thumbSub;
        IDisposable _gate; // OverlayGate 토큰(표시 중에만 non-null — 뒤로가기 CloseTop이 닫기 호출)
        ButtonSpriteSwap _prevSwap, _nextSwap;

        public SaveLoadMode Mode => _mode;
        public int Page => _page;
        public IReadOnlyList<SaveLoadSlot> Slots => _slots;

        void Awake()
        {
            _prevSwap = Swap(prevButton);
            _nextSwap = Swap(nextButton);
            Click(prevButton, () => GoToPage(_page - 1));
            Click(nextButton, () => GoToPage(_page + 1));
            Click(closeButton, () => SetVisible(false));
            SetVisible(false); // 부팅 숨김(프리팹 CanvasGroup alpha0과 정합)
        }

        void OnEnable()
        {
            _showSub = EventBus.Subscribe<ShowSaveLoadCommand>(e => Show(e.Mode));
            _thumbSub = EventBus.Subscribe<ThumbnailSavedEvent>(OnThumbnailSaved);
        }

        void OnDisable()
        {
            _showSub?.Dispose();
            _showSub = null;
            _thumbSub?.Dispose();
            _thumbSub = null;
            _gate?.Dispose(); // 표시 중 비활성 시 게이트 누수 방지(중복 무해)
            _gate = null;
            _visible = false;
        }

        static ButtonSpriteSwap Swap(Button b) => b != null ? b.GetComponent<ButtonSpriteSwap>() : null;
        static void Click(Button b, UnityAction a) { if (b != null) b.onClick.AddListener(a); }

        void EnsureSlots()
        {
            if (_slots.Count > 0 || slotPrefab == null || slotContainer == null) return;
            for (int i = 0; i < slotsPerPage; i++)
                _slots.Add(Instantiate(slotPrefab, slotContainer));
        }

        /// <summary>모드로 팝업을 열고 첫 페이지를 표시한다. Save 모드는 썸네일 캐시를 예열한 뒤(깜빡임 제거) 표시.</summary>
        public void Show(SaveLoadMode mode)
        {
            _mode = mode;
            _page = 0;
            EnsureSlots();
            RefreshPage();
            if (mode == SaveLoadMode.Save && isActiveAndEnabled) StartCoroutine(PrimeThenShow());
            else SetVisible(true); // Load(타이틀 포함) 또는 비활성 폴백 — 예열 불필요/불가
        }

        // 팝업이 아직 안 보이는 동안(alpha 0) 스테이지를 1회 캡처해 캐시 → 완료 후 표시. 캡처 중엔 팝업이
        // 안 보이고, 이후 슬롯 클릭은 캐시를 재사용하므로 클릭마다 깜빡이던 현상이 사라진다. 컨트롤러(구독자)가
        // 없으면 핸들이 안 풀리므로 primeGuardFrames만큼만 기다렸다 그냥 표시(hang 방지).
        IEnumerator PrimeThenShow()
        {
            var handle = new CompletionHandle();
            EventBus.Publish(new PrimeThumbnailCacheCommand(handle));
            int guard = 0;
            while (!handle.IsComplete && guard++ < primeGuardFrames) yield return null;
            SetVisible(true);
        }

        void GoToPage(int page)
        {
            _page = Mathf.Clamp(page, 0, Mathf.Max(0, pageCount - 1));
            RefreshPage();
        }

        /// <summary>페이지·셀 인덱스 → 글로벌 슬롯 번호(0-base, 1페이지 첫 칸=슬롯0=자동저장). 순수 — 테스트 가능.</summary>
        public static int SlotForCell(int page, int cellIndex, int slotsPerPage) => page * slotsPerPage + cellIndex;

        void RefreshPage()
        {
            ClearThumbnails();
            for (int i = 0; i < _slots.Count; i++)
            {
                int slot = SlotForCell(_page, i, slotsPerPage);
                var data = JsonSaveStore.Load(slot);
                _slots[i].Bind(slot, data, data != null ? LoadThumbnail(data, slot) : null, OnSlotSelected, OnSlotRenamed);
            }
            if (pageNumberText != null) pageNumberText.text = $"{_page + 1} / {pageCount}";

            bool atFirst = _page <= 0, atLast = _page >= pageCount - 1;
            SetArrow(_prevSwap, prevButton, !atFirst);
            SetArrow(_nextSwap, nextButton, !atLast);
        }

        static void SetArrow(ButtonSpriteSwap swap, Button b, bool on)
        {
            if (swap != null) swap.SetInteractable(on);
            else if (b != null) b.interactable = on;
        }

        void OnSlotSelected(int slot)
        {
            if (_mode == SaveLoadMode.Load)
            {
                if (!JsonSaveStore.Exists(slot)) return; // 빈 슬롯 로드 무시
                EventBus.Publish(new LoadGameCommand(slot));
                SetVisible(false);
            }
            else // Save
            {
                if (slot == JsonSaveStore.AutoSaveSlot)
                {
                    // 자동저장 슬롯은 수동 덮어쓰기 금지 — 안내 모달만(ModalView가 표시, 닫기 외 동작 없음).
                    EventBus.Publish(new ShowModalCommand(
                        autoSaveModalTitle, autoSaveModalMessage,
                        new[] { new ModalButton(autoSaveModalConfirm, ModalButtonKind.Close) },
                        new ModalRequest()));
                    return;
                }
                // 빈 슬롯=저장 확인 / 데이터 있음=덮어쓰기 확인 → "예"(index 1)일 때만 저장.
                bool hasData = JsonSaveStore.Exists(slot);
                EventBus.Publish(new ShowModalCommand(
                    hasData ? overwriteModalTitle : newSaveModalTitle,
                    hasData ? overwriteModalMessage : newSaveModalMessage,
                    new[]
                    {
                        new ModalButton(saveModalCancel, ModalButtonKind.No),
                        new ModalButton(saveModalConfirm, ModalButtonKind.Yes),
                    },
                    new ModalRequest(i => { if (i == 1) ConfirmSave(slot); })));
            }
        }

        // 제목 인라인 편집 확정 → 슬롯 파일의 표시 이름(chapterLabel)만 교체해 재저장(게임 상태/썸네일 불변).
        // 자동저장 슬롯·빈 슬롯은 대상 아님(슬롯 쪽에서도 차단하지만 이중 가드).
        void OnSlotRenamed(int slot, string newName)
        {
            if (slot == JsonSaveStore.AutoSaveSlot) return;
            var data = JsonSaveStore.Load(slot);
            if (data == null) return;
            data.chapterLabel = newName;
            JsonSaveStore.Save(slot, data);
            RefreshPage();
        }

        // 확인 모달에서 "예" 선택 시 실제 저장(SaveManager 구독). 썸네일은 비동기 — ThumbnailSavedEvent 수신 시 반영.
        void ConfirmSave(int slot)
        {
            EventBus.Publish(new SaveRequestedEvent(slot, "manual"));
            RefreshPage();
        }

        // 저장 직후 비동기 캡처(프레임 종료) 완료 통지 — 표시 중이고 그 슬롯이 현재 페이지면 갱신해 썸네일 즉시 표시.
        void OnThumbnailSaved(ThumbnailSavedEvent e)
        {
            if (!_visible) return;
            int first = SlotForCell(_page, 0, slotsPerPage);
            if (e.Slot >= first && e.Slot < first + slotsPerPage) RefreshPage();
        }

        Sprite LoadThumbnail(SaveData data, int slot)
        {
            string file = !string.IsNullOrEmpty(data.thumbnailFile)
                ? data.thumbnailFile : JsonSaveStore.ThumbnailFileFor(slot);
            string path = JsonSaveStore.ThumbnailPath(file);
            if (!File.Exists(path)) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!tex.LoadImage(File.ReadAllBytes(path))) { Destroy(tex); return null; }
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                _thumbnails.Add(tex);
                _thumbnails.Add(sprite);
                return sprite;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveLoadView] 슬롯 {slot} 썸네일 로드 실패: {e}");
                return null;
            }
        }

        void ClearThumbnails()
        {
            foreach (var o in _thumbnails) if (o != null) Destroy(o);
            _thumbnails.Clear();
        }

        void SetVisible(bool v)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = v ? 1f : 0f;
            canvasGroup.interactable = v;
            canvasGroup.blocksRaycasts = v;
            if (v)
            {
                // 재표시 포함 항상 시각·논리 최상단 동기화 — 형제 팝업 위로(SetAsLastSibling) +
                // 게이트 스택 맨 위로 재푸시(뒤로가기 CloseTop 대상 = 눈에 보이는 최상단 보장).
                transform.SetAsLastSibling();
                _gate?.Dispose();
                _gate = OverlayGate.Push(() => SetVisible(false));
                _visible = true;
            }
            else if (_visible)
            {
                _gate?.Dispose();
                _gate = null;
                _visible = false;
                ClearThumbnails();
            }
        }
    }
}
