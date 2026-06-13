using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로그 1박스(리스트 항목 — *Slot). 프리팹 3변형(캐릭터/플레이어/나레이션)이 이 스크립트를 공유하고
    /// 변형마다 필요한 필드만 바인딩한다(전부 null 허용): 캐릭터=초상(히로인만)+이름박스+대사박스,
    /// 플레이어=이름박스+대사박스(전용 아트), 나레이션=본문 텍스트만. 표시 데이터는 DialogueLogView가 주입.
    /// 글자색·박스 아트는 프리팹에 굽는다(기획: 캐릭터 본문 검정/플레이어 본문 흰색/이름 흰색/나레이션 흰색).
    /// </summary>
    public class DialogueLogEntrySlot : MonoBehaviour
    {
        [Tooltip("초상 루트(히로인 전용 — 미히로인이면 끔). 나레이션/플레이어 프리팹은 미바인딩.")]
        [SerializeField] GameObject portraitRoot;
        [SerializeField] Image portraitImage;
        [Tooltip("이름박스 텍스트. 나레이션 프리팹은 미바인딩.")]
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text bodyText;

        public GameObject PortraitRoot { get => portraitRoot; set => portraitRoot = value; }
        public Image PortraitImage { get => portraitImage; set => portraitImage = value; }
        public TMP_Text NameText { get => nameText; set => nameText = value; }
        public TMP_Text BodyText { get => bodyText; set => bodyText = value; }

        /// <summary>박스 바인딩 — 줄들은 개행으로 누적 표시(목업: 같은 박스에 여러 줄).</summary>
        public void Bind(DialogueLogEntry entry, Sprite portrait)
        {
            if (nameText != null) nameText.text = entry.Speaker;
            if (bodyText != null) bodyText.text = string.Join("\n", entry.Lines);
            if (portraitImage != null && portrait != null) portraitImage.sprite = portrait;
            if (portraitRoot != null) portraitRoot.SetActive(portrait != null); // 엑스트라 = 초상 없음
        }
    }
}
