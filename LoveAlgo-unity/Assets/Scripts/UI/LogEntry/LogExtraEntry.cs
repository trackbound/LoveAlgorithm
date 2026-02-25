using UnityEngine;
using TMPro;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 엑스트라 로그 엔트리 — 이름만 (초상화 없음) + 버블
    /// </summary>
    public class LogExtraEntry : LogEntryBase
    {
        [Header("헤더 (프리팹에 직접 배치)")]
        [SerializeField] TMP_Text nameText;

        public override void Init(string speaker, Sprite portrait)
        {
            if (nameText != null)
                nameText.text = speaker;
        }
    }
}
