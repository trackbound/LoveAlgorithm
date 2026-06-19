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
    /// (있으면) 제목·저장시각·썸네일을 표시한다. 클릭 시 바인딩된 슬롯 번호를 콜백으로 통지
    /// (상태 변경 없음 — ADR-007, ScheduleSlot 패턴). 표시 데이터는 호출부(SaveLoadView)가 주입.
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

        int _slot;
        Action<int> _onSelected;

        public Button Button { get => button; set => button = value; }
        public GameObject EmptyRoot { get => emptyRoot; set => emptyRoot = value; }
        public GameObject HasDataRoot { get => hasDataRoot; set => hasDataRoot = value; }
        public TMP_Text TitleText { get => titleText; set => titleText = value; }
        public TMP_Text DateText { get => dateText; set => dateText = value; }
        public Image ThumbnailImage { get => thumbnailImage; set => thumbnailImage = value; }

        void Awake()
        {
            if (button != null) button.onClick.AddListener(HandleClick);
        }

        /// <summary>슬롯을 바인딩한다. 일반 슬롯: data null=빈 슬롯 외형. 자동저장 슬롯(슬롯0): 비어 있어도
        /// 'has data' 외형으로 항상 노출하고 이름을 <see cref="autoSaveSlotName"/>으로 고정(빈/Day 라벨 안 씀).</summary>
        public void Bind(int slot, SaveData data, Sprite thumbnail, Action<int> onSelected)
        {
            _slot = slot;
            _onSelected = onSelected;
            bool has = data != null;
            bool isAuto = slot == JsonSaveStore.AutoSaveSlot;

            if (emptyRoot != null) emptyRoot.SetActive(!has && !isAuto);
            if (hasDataRoot != null) hasDataRoot.SetActive(has || isAuto);

            if (titleText != null)
                titleText.text = isAuto
                    ? autoSaveSlotName // 자동저장 슬롯 이름 고정
                    : (has ? (string.IsNullOrEmpty(data.chapterLabel) ? $"Slot {slot}" : data.chapterLabel) : "");

            // 날짜/썸네일은 데이터가 있을 때만(자동저장이라도 비어 있으면 빈 칸 + 프리팹 기본 썸네일).
            if (dateText != null) dateText.text = has ? FormatDate(data.savedAtUtc) : "";
            if (thumbnailImage != null && thumbnail != null) thumbnailImage.sprite = thumbnail;
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
        }
    }
}
