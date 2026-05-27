using System.Collections.Generic;
using LoveAlgo.Story.StoryEngine;
using TMPro;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 대사 텍스트의 캐릭터별 vertex 효과 렌더러 (Phase D9 + D11).
    /// DialogueEffectsParser가 추출한 EffectRange를 받아 매 프레임 TMP 메시를 변형.
    ///
    /// 지원:
    ///   - Shake: Perlin noise 기반 x/y 오프셋 (지속).
    ///   - Wave:  sin(t + char*phase) Y 오프셋 (지속).
    ///   - Emph:  per-char one-shot punch — char가 visible로 바뀐 순간부터 EmphDuration 동안
    ///            scale-around-center 펄스 + 작은 Y lift. 끝나면 정상 상태로.
    ///
    /// 사용:
    ///   var renderer = dialogueText.gameObject.AddComponent&lt;DialogueEffectsRenderer&gt;();
    ///   renderer.SetEffects(parsedDialogue.Effects);
    /// 텍스트가 바뀌면 SetEffects를 다시 호출. 비활성화하려면 SetEffects(null).
    ///
    /// Emph 상태 추적:
    ///   매 LateUpdate에서 _text.maxVisibleCharacters 변화를 감지.
    ///   타이핑 진행으로 새로 visible된 char가 Emph range 안이면 _emphStartTime[i] = now 기록.
    ///   다음 프레임부터 ComputeEmphPunch(elapsed)로 펄스 적용. 끝나면 dict에서 제거 (메모리 정리).
    ///   SetEffects 호출 시 모든 emph 상태 리셋 (새 대사 시작).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class DialogueEffectsRenderer : MonoBehaviour
    {
        [Header("Shake")]
        [SerializeField] float shakeAmplitudePx = 1.5f;
        [SerializeField] float shakeFreqHz = 30f;

        [Header("Wave")]
        [SerializeField] float waveAmplitudePx = 4.0f;
        [SerializeField] float waveFreqHz = 2.5f;
        [SerializeField] float wavePhasePerChar = 0.55f;

        [Header("Emph (D11)")]
        [Tooltip("Emph 펀치 지속 시간(초). 이만큼 지나면 char가 정상 크기로 복귀.")]
        [SerializeField] float emphDurationSec = 0.28f;

        TMP_Text _text;
        IReadOnlyList<DialogueEffectRange> _effects;
        bool _hasEffects;

        // D11: emph 상태 — char index → 시작시각. 펄스 완료 시 제거.
        readonly Dictionary<int, float> _emphStartTime = new();
        int _lastMaxVisible;
        // 매 프레임 expired entry 제거용 재사용 버퍼 — 새 List를 할당하지 않음
        readonly List<int> _emphExpiredScratch = new();

        void Awake() => _text = GetComponent<TMP_Text>();

        void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        }

        void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        }

        public void SetEffects(IReadOnlyList<DialogueEffectRange> effects)
        {
            _effects = effects;
            _hasEffects = effects != null && effects.Count > 0;
            // D11: 새 대사 시작 → emph 상태 전부 리셋
            _emphStartTime.Clear();
            _lastMaxVisible = 0;
            if (!_hasEffects && _text != null) _text.ForceMeshUpdate();
        }

        void OnTextChanged(Object obj)
        {
            if (obj != _text) return;
            ApplyEffectsToMesh();
        }

        void LateUpdate()
        {
            if (!_hasEffects || _text == null) return;
            ApplyEffectsToMesh();
        }

        void ApplyEffectsToMesh()
        {
            if (!_hasEffects || _text == null) return;

            var info = _text.textInfo;
            if (info == null || info.characterCount == 0) return;

            // base 리셋 (누적 방지) → 우리 변형 덮어쓰기
            _text.ForceMeshUpdate();
            info = _text.textInfo;

            int maxVisible = _text.maxVisibleCharacters;
            float t = Time.time;
            float shakeT = t * shakeFreqHz;
            float waveT = t * waveFreqHz * Mathf.PI * 2f;

            // D11: 새로 visible된 char가 Emph range 안이면 시작시각 기록
            if (maxVisible > _lastMaxVisible)
            {
                int hi = Mathf.Min(maxVisible, info.characterCount);
                for (int i = _lastMaxVisible; i < hi; i++)
                {
                    if (!_emphStartTime.ContainsKey(i) && IsInAnyEmphRange(i))
                        _emphStartTime[i] = t;
                }
            }
            _lastMaxVisible = maxVisible;

            for (int e = 0; e < _effects.Count; e++)
            {
                var range = _effects[e];
                int hi = Mathf.Min(range.End, info.characterCount);
                int hiVisible = Mathf.Min(hi, maxVisible);
                for (int i = range.Start; i < hiVisible; i++)
                {
                    var ci = info.characterInfo[i];
                    if (!ci.isVisible) continue;

                    int matIdx = ci.materialReferenceIndex;
                    if (matIdx < 0 || matIdx >= info.meshInfo.Length) continue;
                    var verts = info.meshInfo[matIdx].vertices;
                    int v = ci.vertexIndex;
                    if (v + 3 >= verts.Length) continue;

                    if (range.Kind == DialogueEffectKind.Emph)
                    {
                        ApplyEmphToVerts(verts, v, i, t, range.Intensity);
                    }
                    else
                    {
                        Vector3 offset = ComputeOffset(range, i, shakeT, waveT);
                        verts[v + 0] += offset;
                        verts[v + 1] += offset;
                        verts[v + 2] += offset;
                        verts[v + 3] += offset;
                    }
                }
            }

            // D11: 만료된 emph 항목 제거 (메모리 정리)
            if (_emphExpiredScratch.Count > 0)
            {
                for (int k = 0; k < _emphExpiredScratch.Count; k++)
                    _emphStartTime.Remove(_emphExpiredScratch[k]);
                _emphExpiredScratch.Clear();
            }

            // 변형된 vertices 푸시
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                var mi = info.meshInfo[m];
                if (mi.mesh == null) continue;
                mi.mesh.vertices = mi.vertices;
                _text.UpdateGeometry(mi.mesh, m);
            }
        }

        bool IsInAnyEmphRange(int charIdx)
        {
            for (int e = 0; e < _effects.Count; e++)
            {
                var r = _effects[e];
                if (r.Kind == DialogueEffectKind.Emph && charIdx >= r.Start && charIdx < r.End)
                    return true;
            }
            return false;
        }

        void ApplyEmphToVerts(Vector3[] verts, int v, int charIdx, float now, float intensity)
        {
            if (!_emphStartTime.TryGetValue(charIdx, out float startTime)) return;
            float elapsed = now - startTime;
            var (scale, yOff) = ComputeEmphPunch(elapsed, emphDurationSec, intensity > 0 ? intensity : 1f);

            // 만료 → 제거 후 변형 없음 (정상 크기)
            if (scale == 1f && yOff == 0f && elapsed >= emphDurationSec)
            {
                _emphExpiredScratch.Add(charIdx);
                return;
            }

            // scale-around-center: char의 4 vertex (BL, TL, TR, BR) 중심을 기준
            Vector3 center = (verts[v + 0] + verts[v + 2]) * 0.5f;
            Vector3 yV = new Vector3(0f, yOff, 0f);
            verts[v + 0] = center + (verts[v + 0] - center) * scale + yV;
            verts[v + 1] = center + (verts[v + 1] - center) * scale + yV;
            verts[v + 2] = center + (verts[v + 2] - center) * scale + yV;
            verts[v + 3] = center + (verts[v + 3] - center) * scale + yV;
        }

        /// <summary>
        /// D11 Emph 펀치 곡선 — 순수 함수, EditMode 테스트 가능.
        /// 반환:
        ///   - scale: 1.0 기준의 곱셈 인자. 피크 ~1.18 (intensity=1).
        ///   - yOffset: 위로 살짝 들리는 픽셀.
        /// 펀치 곡선: sin(πt) — t=0/1에서 0, t=0.5에서 1 (대칭 피크).
        /// elapsed ≥ duration 또는 음수면 (1, 0) — no-op.
        /// </summary>
        public static (float scale, float yOffset) ComputeEmphPunch(float elapsed, float duration, float intensity)
        {
            if (duration <= 0f || elapsed < 0f || elapsed >= duration)
                return (1f, 0f);

            float t = elapsed / duration;
            float envelope = Mathf.Sin(t * Mathf.PI);
            float i = intensity > 0f ? intensity : 1f;
            float scale = 1f + 0.18f * i * envelope;
            float yOffset = 2.5f * i * envelope;
            return (scale, yOffset);
        }

        Vector3 ComputeOffset(DialogueEffectRange range, int charIndex, float shakeT, float waveT)
        {
            float intensity = range.Intensity > 0 ? range.Intensity : 1f;
            switch (range.Kind)
            {
                case DialogueEffectKind.Shake:
                {
                    float seed = charIndex * 17.13f;
                    float x = (Mathf.PerlinNoise(seed, shakeT) - 0.5f) * 2f * shakeAmplitudePx * intensity;
                    float y = (Mathf.PerlinNoise(seed + 100f, shakeT) - 0.5f) * 2f * shakeAmplitudePx * intensity;
                    return new Vector3(x, y, 0);
                }
                case DialogueEffectKind.Wave:
                {
                    float y = Mathf.Sin(waveT + charIndex * wavePhasePerChar) * waveAmplitudePx * intensity;
                    return new Vector3(0, y, 0);
                }
            }
            return Vector3.zero;
        }
    }
}
