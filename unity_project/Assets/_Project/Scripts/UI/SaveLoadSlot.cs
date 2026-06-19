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

        /// <summary>슬롯을 바인딩한다. data null=빈 슬롯 표시, 있으면 제목/날짜/썸네일 표시.</summary>
        public void Bind(int slot, SaveData data, Sprite thumbnail, Action<int> onSelected)
        {
            _slot = slot;
            _onSelected = onSelected;
            bool has = data != null;
            if (emptyRoot != null) emptyRoot.SetActive(!has);
            if (hasDataRoot != null) hasDataRoot.SetActive(has);
            if (!has) return;

            if (titleText != null)
            {
                string label = string.IsNullOrEmpty(data.chapterLabel) ? $"Slot {slot}" : data.chapterLabel;
                // 슬롯0=자동저장: 첫 칸임을 한눈에 알도록 라벨 앞에 표식(빈 슬롯 표식은 프리팹 영역 — 🟢 후속).
                titleText.text = slot == JsonSaveStore.AutoSaveSlot ? $"자동저장 · {label}" : label;
            }
            if (dateText != null) dateText.text = FormatDate(data.savedAtUtc);
            // 썸네일 PNG가 있을 때만 교체(없으면 프리팹 기본 플레이스홀더 유지).
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
