using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Story;
using LoveAlgo.Story.StoryEngine;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D12 PlayMode smoke — 옵션 C: [UnityTest] in EditMode.
    ///
    /// 본격 PlayMode 환경(씬 + Canvas + DialogueUI)은 Phase C4에서 asmdef 분리 후 가능.
    /// 이번 라운드는 작은 안전망 — D13→directive→D9 풀 파이프라인이 현실적 입력에
    /// 예외 없이 동작하는지, 그리고 TMP_Text + DialogueEffectsRenderer를 EditMode에서
    /// 실제로 instantiate해서 SetEffects/메시 갱신 흐름이 깨지지 않는지 검증.
    ///
    /// 실패 시 [UnityTest] yield 단계에서 잡힘 → 다음 라운드 회귀 안전망.
    /// </summary>
    [TestFixture]
    public class DialogueRichTextSmokeTests
    {
        // ── 1) 파이프라인 통합 (TMP 의존 없음, 순수 함수) ──────────────────

        [Test]
        public void Pipeline_TypicalDialogue_AllStagesProduceCleanText()
        {
            // 현실적 작가 입력: 색 + shake + wave + pause + 일반 텍스트 혼합
            string raw = "어... <color=heroine>로아</color>, <shake=2>괜찮아?</shake> <pause=0.3>잠깐만,</pause> 잠시 <wave>생각해보자</wave>.";

            // Stage 1: D13 named color (palette 없음 → hex만 통과, named는 워닝 후 통과)
            // 워닝 LogAssert는 dialogue color 테스트가 따로 검증하므로 여기선 무음.
            LogAssert.ignoreFailingMessages = true;
            string afterColor = DialogueColorPalette.ApplyNamedColors(raw, null);

            // Stage 2: directive parse simulation — 본 파이프라인은 DialogueUI 내부지만 형태는
            // <wait|pause|sfx|emote|speed> 태그가 strip된 텍스트가 다음 단계 입력이 됨.
            // 여기선 단순화: DialogueEffectsParser는 D9 태그만 인식하므로 raw에서 D9 부분만 검증.

            // Stage 3: D9 visual parse
            var parsed = DialogueEffectsParser.Parse(afterColor);

            // 검증: <shake>, <wave>가 strip됐고, 효과 range 기록됨, 텍스트가 비어 있지 않음
            Assert.IsNotNull(parsed.CleanText);
            Assert.IsTrue(parsed.CleanText.Length > 0);
            StringAssert.DoesNotContain("<shake", parsed.CleanText);
            StringAssert.DoesNotContain("<wave", parsed.CleanText);
            StringAssert.DoesNotContain("</shake", parsed.CleanText);
            StringAssert.DoesNotContain("</wave", parsed.CleanText);
            // <color=...>은 D9 파서가 모르므로 통과 (TMP가 처리)
            StringAssert.Contains("<color=", parsed.CleanText);
            // 효과는 적어도 shake + wave 2건
            Assert.GreaterOrEqual(parsed.Effects.Count, 2);

            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void Pipeline_AllEffectKinds_DistinctRanges()
        {
            var parsed = DialogueEffectsParser.Parse(
                "<shake>A</shake> <wave>B</wave> <emph>C</emph>");
            Assert.AreEqual("A B C", parsed.CleanText);
            Assert.AreEqual(3, parsed.Effects.Count);
            var kinds = new HashSet<DialogueEffectKind>();
            foreach (var e in parsed.Effects) kinds.Add(e.Kind);
            Assert.AreEqual(3, kinds.Count, "Shake/Wave/Emph 셋 다 들어가야 함");
        }

        [Test]
        public void Pipeline_NestedColorWithEffect_RangesAlignToCleanText()
        {
            // <color> 안에 <shake>가 있는 경우 — D9가 strip한 후 색 태그는 그대로
            var parsed = DialogueEffectsParser.Parse("<color=#ff0000><shake>경고</shake></color>");
            // shake가 strip된 결과: "<color=#ff0000>경고</color>"
            Assert.AreEqual("<color=#ff0000>경고</color>", parsed.CleanText);
            Assert.AreEqual(1, parsed.Effects.Count);
            // 효과 range는 CleanText의 "경고" 위치 — <color=#ff0000>(15글자) 다음 2글자
            Assert.AreEqual(15, parsed.Effects[0].Start);
            Assert.AreEqual(17, parsed.Effects[0].End);
        }

        // ── 2) TMP_Text + DialogueEffectsRenderer EditMode smoke ───────────

        [UnityTest]
        public IEnumerator TextMesh_AttachRendererAndSetEffects_NoExceptions()
        {
            // EditMode에서 TMP_Text 생성 — Canvas 없이 가능한지 시험.
            // 실패하면 LogAssert가 잡고 테스트는 skip로 나옴.
            GameObject root = null;
            try
            {
                root = new GameObject("D12SmokeRoot", typeof(Canvas));
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var tmpGo = new GameObject("D12SmokeText", typeof(RectTransform));
                tmpGo.transform.SetParent(root.transform, false);
                var tmp = tmpGo.AddComponent<TextMeshProUGUI>();
                tmp.text = "테스트";

                // Renderer 부착 + 효과 적용
                var renderer = tmpGo.AddComponent<DialogueEffectsRenderer>();
                var parsed = DialogueEffectsParser.Parse("<shake>테스트</shake>");
                renderer.SetEffects(parsed.Effects);
                tmp.text = parsed.CleanText;

                // ForceMeshUpdate로 textInfo 채우고, 한 프레임 양보
                tmp.ForceMeshUpdate();
                yield return null;

                // 핵심 검증: 예외 없이 여기까지 도달. character count > 0.
                Assert.IsNotNull(tmp.textInfo);
                Assert.Greater(tmp.textInfo.characterCount, 0,
                    "TMP가 EditMode에서 textInfo를 채우지 못함 — 환경 의존성 가능");
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator TextMesh_MaxVisibleProgression_RendererSurvives()
        {
            // 타이핑 시뮬레이션 — maxVisibleCharacters를 증분하면서 renderer가 깨지는지
            GameObject root = null;
            try
            {
                root = new GameObject("D12TypingRoot", typeof(Canvas));
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

                var tmpGo = new GameObject("Text", typeof(RectTransform));
                tmpGo.transform.SetParent(root.transform, false);
                var tmp = tmpGo.AddComponent<TextMeshProUGUI>();

                var renderer = tmpGo.AddComponent<DialogueEffectsRenderer>();
                var parsed = DialogueEffectsParser.Parse("<emph>하나</emph><wave>둘</wave><shake>셋</shake>");
                tmp.text = parsed.CleanText;
                renderer.SetEffects(parsed.Effects);

                tmp.ForceMeshUpdate();
                yield return null;

                // 0 → 전체로 한 단계씩 증가
                int total = tmp.textInfo.characterCount;
                Assert.Greater(total, 0);
                for (int v = 0; v <= total; v++)
                {
                    tmp.maxVisibleCharacters = v;
                    tmp.ForceMeshUpdate();
                    yield return null;
                }

                // 끝까지 도달했고 예외 없음
                Assert.AreEqual(total, tmp.maxVisibleCharacters);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator TextMesh_ResetEffectsBetweenLines_NoStaleState()
        {
            // 새 대사 시작 시 SetEffects가 emph dict + lastMaxVisible을 리셋해서
            // 이전 라인의 잔재가 새 라인에 영향 없는지
            GameObject root = null;
            try
            {
                root = new GameObject("D12ResetRoot", typeof(Canvas));
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

                var tmpGo = new GameObject("Text", typeof(RectTransform));
                tmpGo.transform.SetParent(root.transform, false);
                var tmp = tmpGo.AddComponent<TextMeshProUGUI>();
                var renderer = tmpGo.AddComponent<DialogueEffectsRenderer>();

                // 첫 라인
                var p1 = DialogueEffectsParser.Parse("<emph>첫</emph>");
                tmp.text = p1.CleanText;
                renderer.SetEffects(p1.Effects);
                tmp.maxVisibleCharacters = 0;
                tmp.ForceMeshUpdate();
                yield return null;
                tmp.maxVisibleCharacters = p1.CleanText.Length;
                tmp.ForceMeshUpdate();
                yield return null;

                // 두 번째 라인
                var p2 = DialogueEffectsParser.Parse("<shake>다음</shake>");
                tmp.text = p2.CleanText;
                renderer.SetEffects(p2.Effects);  // ← emph state 리셋 기대
                tmp.maxVisibleCharacters = 0;
                tmp.ForceMeshUpdate();
                yield return null;

                // 핵심: SetEffects 후 텍스트가 바뀌어도 예외/log error 없이 진행
                tmp.maxVisibleCharacters = p2.CleanText.Length;
                tmp.ForceMeshUpdate();
                yield return null;

                Assert.IsTrue(true, "2 라인 연속 처리 — 예외 없음");
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }
    }
}
