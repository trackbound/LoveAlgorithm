using UnityEngine;

namespace LoveAlgo.LockScreen.Data
{
    /// <summary>
    /// PC잠금 화면 표시 콘텐츠 — 로아 메시지 4개 (인덱스별).
    /// 잠금화면 진입/실패/성공 등 상태별로 노출.
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/LockScreen/Content", fileName = "LockScreenContent")]
    public class LockScreenContentSO : ScriptableObject
    {
        [Tooltip("로아 메시지 4개 (기획 순서대로)")]
        [TextArea(2, 4)]
        public string[] roaMessages = new string[4];
    }
}
