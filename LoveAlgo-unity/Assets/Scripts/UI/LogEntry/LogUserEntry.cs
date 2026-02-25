using UnityEngine;
using TMPro;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 유저 로그 엔트리 — 이름 + 버블 (우측 정렬 등 유저 전용 레이아웃)
    /// </summary>
    public class LogUserEntry : LogEntryBase
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
