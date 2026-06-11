using UnityEngine;

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 메신저 연출 수치(Definition, ADR-012). 기획서 동결값 = 진동 2초(새 메시지 도착 시),
    /// 나머지는 시작값 — 감독 튜닝 영역. 에셋: Resources/Data/MessengerTuning.asset.
    /// </summary>
    [CreateAssetMenu(fileName = "MessengerTuning", menuName = "LoveAlgo/Messenger Tuning")]
    public class MessengerTuningSO : ScriptableObject
    {
        [Header("폰 버튼 호버 슬라이드 (기획서: 평소 작게 → 호버 시 왼쪽으로 열림)")]
        [Min(0f)] public float slideDistance = 150f;
        [Min(0f)] public float slideDuration = 0.15f;

        [Header("새 메시지 진동 (기획서 동결: 2초)")]
        [Min(0f)] public float vibrateDuration = 2f;
        [Tooltip("흔들림 최대 각도(도) — 감쇠 사인 회전.")]
        [Min(0f)] public float vibrateAngle = 8f;
        [Tooltip("초당 진동 횟수.")]
        [Min(0f)] public float vibrateFrequency = 18f;
    }
}
