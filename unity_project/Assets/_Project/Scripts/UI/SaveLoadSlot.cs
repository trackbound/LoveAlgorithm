using System;
using System.Globalization;
using LoveAlgo.Core; // SaveData
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 세이브/로드 1칸. 데이터 유무로 <see cref="emptyRoot"/>/<see cref="hasDataRoot"/> GO를 스왑하고
    /// (있으면) 제목·저장시각·썸네일을 표시한다. 슬롯 클릭 시 슬롯 번호를 콜백 통지(상태 변경 없음 — ADR-007).
    /// 제목(<see cref="titleButton"/>) 클릭 시 인라인 <see cref="nameInput"/>으로 이름 편집 → 확정 시
    /// <see cref="_onRenamed"/> 통지(자동저장 슬롯은 편집 불가). 표시 데이터/콜백은 호출부(SaveLoadView)가 주입.
    /// </summary>
    public class SaveLoadSlot : MonoBehaviour
    {
        [SerializeField] Button button;
        [Tooltip("데이터 없을 때 표시(빈 슬롯).")]
        [SerializeField] GameObject emptyRoot;
        [Tooltip("데이터 있을 때 표시(제목/날짜/썸네일).")]
        [SerializeField] GameObject hasDataRoot;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text dateText;
        [SerializeField] Image thumbnailImage;
        [Tooltip("자동저장 슬롯(슬롯0)에 고정 표시할 이름(빈 슬롯/Day 라벨로 바뀌지 않음).")]
        [SerializeField] string autoSaveSlotName = "자동저장";

        [Header("이름 변경(제목 클릭 → 인라인 편집)")]
        [Tooltip("제목 영역 클릭 → 이름 편집 시작. 미바인딩이면 이름 변경 비활성.")]
        [SerializeField] Button titleButton;
        [Tooltip("이름 편집용 입력 필드(평소 비활성, 편집 중에만 노출). 미바인딩이면 이름 변경 비활성.")]
        [SerializeField] TMP_InputField nameInput;

        int _slot;
        Action<int> _onSelected;
        Action<int, string> _onRenamed;
        bool _suppressEndEdit; // EndRenameUi의 SetActive가 onEndEdit를 재귀 호출하는 것 방지

        public Button Button { get => button; set => button = value; }
        public GameObject EmptyRoot { get => emptyRoot; set => emptyRoot = value; }
        public GameObject HasDataRoot { get => hasDataRoot; set => hasDataRoot = value; }
        public TMP_Text TitleText { get => titleText; set => titleText = value; }
        public TMP_Text DateText { get => dateText; set => dateText = value; }
        public Image ThumbnailImage { get => thumbnailImage; set => thumbnailImage = value; }
        public Button TitleButton { get => titleButton; set => titleButton = value; }
        public TMP_InputField NameInput { get => nameInput; set => nameInput = value; }

        void Awake()
        {
            if (button != null) button.onClick.AddListener(HandleClick);
            if (titleButton != null) titleButton.onClick.AddListener(BeginRename);
            if (nameInput != null)
            {
                nameInput.onEndEdit.AddListener(CommitRename);
                nameInput.gameObject.SetActive(false);
            }
        }

        /// <summary>슬롯을 바인딩한다. 일반 슬롯: data null=빈 슬롯 외형. 자동저장 슬롯(슬롯0): 비어 있어도
        /// 'has data' 외형으로 항상 노출하고 이름을 <see cref="autoSaveSlotName"/>으로 고정(빈/Day 라벨 안 씀).
        /// <paramref name="onRenamed"/>=제목 편집 확정 콜백(슬롯,새이름).</summary>
        public void Bind(int slot, SaveData data, Sprite thumbnail, Action<int> onSelected, Action<int, string> onRenamed)
        {
            _slot = slot;
            _onSelected = onSelected;
            _onRenamed = onRenamed;
            EndRenameUi(); // 페이지 재바인딩 시 편집 상태 초기화
            bool has = data != null;
            bool isAuto = slot == JsonSaveStore.AutoSaveSlot;

            if (emptyRoot != null) emptyRoot.SetActive(!has && !isAuto);
            if (hasDataRoot != null) hasDataRoot.SetActive(has || isAuto);

            if (titleText != null)
                titleText.text = isAuto
                    ? autoSaveSlotName // 자동저장 슬롯 이름 고정
                    : (has ? (string.IsNullOrEmpty(data.chapterLabel) ? $"Slot {slot}" : data.chapterLabel) : "");

            // 이름 변경은 데이터가 있는 일반 슬롯만(자동저장·빈 슬롯 불가).
            if (titleButton != null) titleButton.interactable = has && !isAuto;

            // 날짜/썸네일은 데이터가 있을 때만(자동저장이라도 비어 있으면 빈 칸 + 프리팹 기본 썸네일).
            if (dateText != null) dateText.text = has ? FormatDate(data.savedAtUtc) : "";
            if (thumbnailImage != null && thumbnail != null) thumbnailImage.sprite = thumbnail;
        }

        // 제목 클릭 → 인라인 편집 시작(제목 숨기고 입력 필드 노출·포커스). 자동저장 슬롯은 진입 차단(이중 가드).
        void BeginRename()
        {
            if (nameInput == null || titleText == null || _slot == JsonSaveStore.AutoSaveSlot) return;
            nameInput.text = titleText.text;
            titleText.gameObject.SetActive(false);
            nameInput.gameObject.SetActive(true);
            nameInput.ActivateInputField();
            nameInput.Select();
        }

        // 편집 확정(Enter/포커스 해제). UI 원복 후 공백 아니면 이름 통지(빈 이름=취소).
        void CommitRename(string value)
        {
            if (_suppressEndEdit) return;
            EndRenameUi();
            string name = (value ?? "").Trim();
            if (name.Length == 0) return;
            if (titleText != null) titleText.text = name;
            _onRenamed?.Invoke(_slot, name);
        }

        // 입력 필드 숨기고 제목 표시로 원복. SetActive가 onEndEdit를 재귀 호출하지 않도록 가드.
        void EndRenameUi()
        {
            _suppressEndEdit = true;
            if (nameInput != null) nameInput.gameObject.SetActive(false);
            if (titleText != null) titleText.gameObject.SetActive(true);
            _suppressEndEdit = false;
        }

        /// <summary>ISO8601(round-trip) UTC 문자열을 로컬 "yyyy/MM/dd HH:mm"으로. 파싱 실패 시 원문(순수 — 테스트 가능).</summary>
        public static string FormatDate(string savedAtUtc)
        {
            if (string.IsNullOrEmpty(savedAtUtc)) return "";
            if (DateTime.TryParse(savedAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
            return savedAtUtc;
        }

        void HandleClick() => _onSelected?.Invoke(_slot);

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
            if (titleButton != null) titleButton.onClick.RemoveListener(BeginRename);
            if (nameInput != null) nameInput.onEndEdit.RemoveListener(CommitRename);
        }
    }
}
