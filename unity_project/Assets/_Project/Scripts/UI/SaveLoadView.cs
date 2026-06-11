using System;
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
    /// SetVisible/OverlayGate/누수가드 = SettingsView 1:1(게임플레이 입력 차단). 슬롯0(자동저장)은 Continue로 분리 — 1+만 노출.
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
        [Tooltip("총 페이지 수. 슬롯 = 1 ~ slotsPerPage*pageCount.")]
        [SerializeField] int pageCount = 3;

        readonly List<SaveLoadSlot> _slots = new();
        readonly List<UnityEngine.Object> _thumbnails = new(); // 페이지별 생성 텍스처/스프라이트(누수 방지)
        SaveLoadMode _mode = SaveLoadMode.Load;
        int _page;
        bool _visible;
        IDisposable _showSub;
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

        void OnEnable() => _showSub = EventBus.Subscribe<ShowSaveLoadCommand>(e => Show(e.Mode));
        void OnDisable()
        {
            _showSub?.Dispose();
            _showSub = null;
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

        /// <summary>모드로 팝업을 열고 첫 페이지를 표시한다.</summary>
        public void Show(SaveLoadMode mode)
        {
            _mode = mode;
            _page = 0;
            EnsureSlots();
            RefreshPage();
            SetVisible(true);
        }

        void GoToPage(int page)
        {
            _page = Mathf.Clamp(page, 0, Mathf.Max(0, pageCount - 1));
            RefreshPage();
        }

        /// <summary>페이지·셀 인덱스 → 글로벌 슬롯 번호(1-base, 슬롯0=자동저장 분리). 순수 — 테스트 가능.</summary>
        public static int SlotForCell(int page, int cellIndex, int slotsPerPage) => page * slotsPerPage + cellIndex + 1;

        void RefreshPage()
        {
            ClearThumbnails();
            for (int i = 0; i < _slots.Count; i++)
            {
                int slot = SlotForCell(_page, i, slotsPerPage);
                var data = JsonSaveStore.Load(slot);
                _slots[i].Bind(slot, data, data != null ? LoadThumbnail(data, slot) : null, OnSlotSelected);
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
            else // Save: 현재 상태를 슬롯에 저장(SaveManager 구독). 썸네일은 비동기 — 다음 열람 시 표시.
            {
                EventBus.Publish(new SaveRequestedEvent(slot, "manual"));
                RefreshPage();
            }
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
