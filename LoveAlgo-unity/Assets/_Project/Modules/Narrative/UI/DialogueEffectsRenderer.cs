using System.Collections.Generic;
using LoveAlgo.Story.StoryEngine;
using TMPro;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 대사 텍스트의 캐릭터별 vertex 효과 렌더러 (Phase D9).
    /// DialogueEffectsParser가 추출한 EffectRange를 받아 매 프레임 TMP 메시를 변형.
    ///
    /// 지원: Shake (랜덤 진동) / Wave (사인파). Emph는 향후 라운드 (per-char one-shot 상태 관리).
    ///
    /// 사용:
    ///   var renderer = dialogueText.gameObject.AddComponent&lt;DialogueEffectsRenderer&gt;();
    ///   renderer.SetEffects(parsedDialogue.Effects);
    /// 텍스트가 바뀌면 SetEffects를 다시 호출. 비활성화하려면 SetEffects(null).
    ///
    /// TMP 메시 변형 패턴 (Unity 6 / TMP 4.x 호환):
    ///   1) TMP가 자동 ForceMeshUpdate 후 textInfo 채워짐
    ///   2) LateUpdate에서 vertices 직접 변형
    ///   3) mesh.vertices에 다시 할당 → UpdateGeometry
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class DialogueEffectsRenderer : MonoBehaviour
    {
        [Header("Shake")]
        [SerializeField] float shakeAmplitudePx = 1.5f;     // px (vertex space는 px와 비례, scale 보정 LateUpdate에서)
        [SerializeField] float shakeFreqHz = 30f;

        [Header("Wave")]
        [SerializeField] float waveAmplitudePx = 4.0f;
        [SerializeField] float waveFreqHz = 2.5f;
        [SerializeField] float wavePhasePerChar = 0.55f;     // 인접 char 간 위상차 (rad)

        TMP_Text _text;
        IReadOnlyList<DialogueEffectRange> _effects;
        bool _hasEffects;

        // 매 프레임 새 dirty 표시 — TMP의 vertex regen 후 우리가 다시 덮어쓰는 안전 패턴
        void Awake() => _text = GetComponent<TMP_Text>();

        void OnEnable()
        {
            // TMP가 메시를 재생성한 직후를 노려서 우리 변형이 살아남도록 콜백 등록
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
            // 비활성화 시 즉시 본래 vertex로 복원 — ForceMeshUpdate가 깨끗한 메시 그림
            if (!_hasEffects && _text != null) _text.ForceMeshUpdate();
        }

        void OnTextChanged(Object obj)
        {
            if (obj != _text) return;
            ApplyEffectsToMesh();
        }

        void LateUpdate()
        {
            // Shake/Wave는 시간 기반 → 매 프레임 갱신 필요. ForceMeshUpdate 호출 비용 비싸므로
            // TMP가 따로 변경이 없으면 우리만 vertex 재계산.
            if (!_hasEffects || _text == null) return;
            ApplyEffectsToMesh();
        }

        void ApplyEffectsToMesh()
        {
            if (!_hasEffects || _text == null) return;

            var info = _text.textInfo;
            if (info == null || info.characterCount == 0) return;

            // mesh 캐시. TMP는 ApplyVertexData 전까지 vertices 배열을 textInfo가 보유.
            // 매 프레임 textInfo 기반 변형은 깨끗한 base에 덧칠하는 게 아니라 누적될 수 있음 →
            // ForceMeshUpdate로 base 리셋 후 변형 적용.
            _text.ForceMeshUpdate();
            info = _text.textInfo;

            int maxVisible = _text.maxVisibleCharacters;
            float t = Time.time;
            float shakeT = t * shakeFreqHz;
            float waveT = t * waveFreqHz * Mathf.PI * 2f;

            for (int e = 0; e < _effects.Count; e++)
            {
                var range = _effects[e];
                int hi = Mathf.Min(range.End, info.characterCount);
                int hiVisible = Mathf.Min(hi, maxVisible); // 아직 안 드러난 글자는 건너뜀
                for (int i = range.Start; i < hiVisible; i++)
                {
                    var ci = info.characterInfo[i];
                    if (!ci.isVisible) continue;

                    int matIdx = ci.materialReferenceIndex;
                    if (matIdx < 0 || matIdx >= info.meshInfo.Length) continue;
                    var verts = info.meshInfo[matIdx].vertices;
                    int v = ci.vertexIndex;
                    if (v + 3 >= verts.Length) continue;

                    Vector3 offset = ComputeOffset(range, i, shakeT, waveT);
                    verts[v + 0] += offset;
                    verts[v + 1] += offset;
                    verts[v + 2] += offset;
                    verts[v + 3] += offset;
                }
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

        Vector3 ComputeOffset(DialogueEffectRange range, int charIndex, float shakeT, float waveT)
        {
            float intensity = range.Intensity > 0 ? range.Intensity : 1f;
            switch (range.Kind)
            {
                case DialogueEffectKind.Shake:
                {
                    // char별 위상 다르게 — 서로 다른 픽셀로 흔들림
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
                case DialogueEffectKind.Emph:
                {
                    // 향후 라운드 — 현재 라운드는 no-op
                    return Vector3.zero;
                }
            }
            return Vector3.zero;
        }
    }
}
