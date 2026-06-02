using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 색 틴트 연출 동결 수치 Definition SO(ADR-012: 코드 매직넘버 금지). 프리셋별 색(RGB)과 기본 알파·지속(초).
    /// 엔진(NarrativeController)이 참조해 프리셋→색, CSV 미지정 알파/지속을 동결값으로 해석한다. 런타임 읽기 전용.
    /// 값 출처 = 구 ScreenFX.ParseTintColor + docs/REWRITE_TUNING_VALUES.csv(틴트 알파 0.25·지속 0.5s).
    /// </summary>
    [CreateAssetMenu(fileName = "ColorTintTuning", menuName = "LoveAlgo/Color Tint Tuning")]
    public class ColorTintTuningSO : ScriptableObject
    {
        [SerializeField] Color sepia = new Color(0.44f, 0.26f, 0.08f);  // 따뜻한 갈색
        [SerializeField] Color blue = new Color(0.1f, 0.15f, 0.4f);     // 차가운 파랑(꿈/회상)
        [SerializeField] Color red = new Color(0.5f, 0.05f, 0.05f);     // 충격/위기
        [SerializeField] Color pink = new Color(0.6f, 0.2f, 0.35f);     // 로맨틱/설렘
        [SerializeField] Color green = new Color(0.1f, 0.3f, 0.1f);     // 자연/평화
        [SerializeField] Color sunset = new Color(0.6f, 0.25f, 0.1f);   // 석양/노을

        [Tooltip("CSV가 alpha를 생략한 경우 기본 알파. 동결값 0.25.")]
        [SerializeField] float defaultAlpha = 0.25f;
        [Tooltip("CSV가 duration을 생략한 경우 기본 지속. 동결값 0.5s.")]
        [SerializeField] float defaultDuration = 0.5f;

        public float DefaultAlpha => defaultAlpha;
        public float DefaultDuration => defaultDuration;

        public Color ColorFor(TintPreset preset)
        {
            switch (preset)
            {
                case TintPreset.Blue:   return blue;
                case TintPreset.Red:    return red;
                case TintPreset.Pink:   return pink;
                case TintPreset.Green:  return green;
                case TintPreset.Sunset: return sunset;
                default:                return sepia;
            }
        }
    }
}
