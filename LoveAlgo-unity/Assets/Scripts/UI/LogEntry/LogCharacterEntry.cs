using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 캐릭터 로그 엔트리 — 초상화 + 이름 + 버블
    /// 프리팹에 초상화 Image와 이름 TMP가 이미 배치되어 있음
    /// </summary>
    public class LogCharacterEntry : LogEntryBase
    {
        [Header("헤더 (프리팹에 직접 배치)")]
        [SerializeField] Image portraitImage;
        [SerializeField] TMP_Text nameText;

        public override void Init(string speaker, Sprite portrait)
        {
            if (nameText != null)
                nameText.text = speaker;
            if (portraitImage != null && portrait != null)
                portraitImage.sprite = portrait;
        }
    }
}
