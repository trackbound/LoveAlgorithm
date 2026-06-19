using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로그 요소 1개(*Slot) — run 컨테이너의 화자 헤더 또는 대사 버블로 양쪽에 재사용된다(DialogueLogView가 조립).
    /// 필요한 필드만 배선(전부 null 허용): 헤더=초상(히로인만)+이름박스, 버블=본문 텍스트만.
    /// 글자색·박스 아트는 프리팹에 굽는다(기획: 캐릭터 본문 검정/플레이어 본문 흰색/이름 흰색/독백 흰색+분홍 번짐).
    /// </summary>
    public class DialogueLogEntrySlot : MonoBehaviour
    {
        [Tooltip("초상 루트(히로인 전용 — 미히로인/플레이어면 끔). 버블 프리팹은 미바인딩.")]
        [SerializeField] GameObject portraitRoot;
        [SerializeField] Image portraitImage;
        [Tooltip("이름박스 텍스트(헤더 전용). 버블 프리팹은 미바인딩.")]
        [SerializeField] TMP_Text nameText;
        [Tooltip("본문 텍스트(버블 전용). 헤더 프리팹은 미바인딩.")]
        [SerializeField] TMP_Text bodyText;

        public GameObject PortraitRoot { get => portraitRoot; set => portraitRoot = value; }
        public Image PortraitImage { get => portraitImage; set => portraitImage = value; }
        public TMP_Text NameText { get => nameText; set => nameText = value; }
        public TMP_Text BodyText { get => bodyText; set => bodyText = value; }

        /// <summary>요소 바인딩 — 배선된 필드만 채운다(헤더=이름+초상, 버블=본문). 본문 내부 \n은 한 버블 안 여러 줄.
        /// 초상 미전달(엑스트라/플레이어)이면 초상 루트를 끈다.</summary>
        public void Bind(DialogueLogEntry entry, Sprite portrait)
        {
            if (bodyText != null) bodyText.text = entry.Text;
            if (nameText != null) nameText.text = entry.Speaker;
            if (portraitImage != null && portrait != null) portraitImage.sprite = portrait;
            if (portraitRoot != null) portraitRoot.SetActive(portrait != null);
        }
    }
}
