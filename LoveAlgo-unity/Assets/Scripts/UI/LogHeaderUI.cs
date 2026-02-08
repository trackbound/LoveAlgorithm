using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로그 헤더 프리팹 컴포넌트 — 프로필(선택) + 이름 표시
    /// 고정 크기 프리팹이므로 LayoutGroup 불필요, RectTransform 앵커로 배치
    /// </summary>
    public class LogHeaderUI : MonoBehaviour
    {
        [SerializeField] Image portraitImage;    // 케이스1만 사용 (nullable)
        [SerializeField] TMP_Text nameText;      // 화자 이름

        public void SetPortrait(Sprite sprite)
        {
            if (portraitImage != null)
                portraitImage.sprite = sprite;
        }

        public void SetName(string name)
        {
            if (nameText != null)
                nameText.text = name;
        }
    }
}
