using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowBackgroundCommand, ShowCharacterCommand, CompletionHandle, NarrativeFinishedEvent
using LoveAlgo.UI;     // StageView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 스테이지 슬라이스2 PlayMode 검증: StageView가 OnEnable에서 BG/Char 명령을 구독해 코루틴 전환을
    /// 수행하고 완료 핸들을 풀어주는지(실 Resources <c>BG/bg_00_00</c>·<c>Characters/Roa/기본</c>로 로드).
    /// 슬라이스1과 동일하게 명령 이벤트 + 완료 핸들로만 검증(뷰 직접 참조 없음).
    /// </summary>
    public class StageViewPlayModeTests
    {
        const string CharId = "Roa";           // Resources/Characters/Roa/기본 (캐릭터=폴더, 표정=한글 파일명)
        const string Emote = "기본";

        // BG 에셋은 감독의 명명 재작업(코드명↔한글명)과 무관하게 "Resources/BG/{name} 로딩 컨벤션"만
        // 검증하도록 실존하는 첫 스프라이트를 사용한다(특정 파일명 고정 시 리네임마다 적색).
        static string BgKey
        {
            get
            {
                var all = Resources.LoadAll<Sprite>("BG");
                Assert.IsTrue(all != null && all.Length > 0,
                    "Resources/BG에 Sprite가 1개 이상 있어야 한다(스테이지 BG 로딩 컨벤션 전제).");
                return all[0].name;
            }
        }

        static Image MakeImage(string name, out CanvasGroup group)
        {
            var go = new GameObject(name);
            var img = go.AddComponent<Image>();
            group = go.AddComponent<CanvasGroup>();
            return img;
        }

        static StageView MakeView(out GameObject root)
        {
            root = new GameObject("StageView_PlayTest");
            var view = root.AddComponent<StageView>(); // OnEnable → 구독

            var bgFront = MakeImage("bgFront", out var bgFrontGroup);
            var bgBack = MakeImage("bgBack", out var bgBackGroup);
            var cImg = MakeImage("slotC", out var cGroup);
            bgFront.transform.SetParent(root.transform);
            bgBack.transform.SetParent(root.transform);
            cImg.transform.SetParent(root.transform);

            view.BgFront = bgFront; view.BgFrontGroup = bgFrontGroup;
            view.BgBack = bgBack; view.BgBackGroup = bgBackGroup;
            view.SlotC = new StageView.SlotBinding { image = cImg, group = cGroup };
            return view;
        }

        static IEnumerator WaitDone(CompletionHandle req, float timeout = 2f)
        {
            float t = 0f;
            while (!req.IsComplete && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator Bg_Cut_Sets_Sprite_And_Completes()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new ShowBackgroundCommand(BgKey, BgTransition.Cut, 0f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete, "Cut은 즉시 완료되어야 한다.");
                Assert.IsNotNull(view.BgFront.sprite, "BG 스프라이트가 설정되어야 한다.");
                Assert.IsTrue(view.BgFront.enabled);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Bg_Cross_Animates_And_Completes()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new ShowBackgroundCommand(BgKey, BgTransition.Cross, 0.05f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete, "Cross 전환이 핸들을 완료해야 한다.");
                Assert.IsNotNull(view.BgFront.sprite);
                Assert.AreEqual(1f, view.BgFrontGroup.alpha, 1e-3f, "전환 후 front는 완전 노출.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Bg_MissingSprite_Still_Completes()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;
                LogAssert.ignoreFailingMessages = true; // 경고 로그 허용
                var req = new CompletionHandle();
                EventBus.Publish(new ShowBackgroundCommand("__nonexistent__", BgTransition.Cut, 0f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete, "스프라이트 없어도 핸들은 완료되어 엔진이 멈추지 않아야 한다.");
            }
            finally { LogAssert.ignoreFailingMessages = false; Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Char_Enter_Then_Exit_Toggles_Slot()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;

                var enterReq = new CompletionHandle();
                EventBus.Publish(new ShowCharacterCommand(CharSlot.C, CharAction.Enter, CharId, Emote, 0.05f, enterReq));
                yield return WaitDone(enterReq);

                Assert.IsTrue(enterReq.IsComplete);
                Assert.IsNotNull(view.SlotC.image.sprite, "캐릭터 스프라이트가 설정되어야 한다.");
                Assert.IsTrue(view.SlotC.image.enabled);
                Assert.AreEqual(1f, view.SlotC.group.alpha, 1e-3f);

                var exitReq = new CompletionHandle();
                EventBus.Publish(new ShowCharacterCommand(CharSlot.C, CharAction.Exit, null, "", 0.05f, exitReq));
                yield return WaitDone(exitReq);

                Assert.IsTrue(exitReq.IsComplete);
                Assert.IsFalse(view.SlotC.image.enabled, "Exit 후 슬롯은 비활성.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator NarrativeFinished_Clears_Stage()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new ShowCharacterCommand(CharSlot.C, CharAction.Enter, CharId, Emote, 0f, req));
                yield return WaitDone(req);
                Assert.IsTrue(view.SlotC.image.enabled);

                EventBus.Publish(new NarrativeFinishedEvent("test"));
                yield return null;

                Assert.IsFalse(view.SlotC.image.enabled, "내러티브 종료 시 스테이지가 정리되어야 한다.");
                Assert.IsNull(view.BgFront.sprite);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Char_Enter_AppliesHeroinePlacement_FromCatalog()
        {
            // StageView가 시드 CharacterStageCatalog를 로드해 Enter 시 슬롯 기본 위에 스케일·오프셋을 합성하는지.
            // 기대값은 자산에서 직접 읽어 비교(감독 튜닝값과 비결합) — 동시에 손수 작성한 .asset의 직렬화/바인딩도 검증.
            var view = MakeView(out var root);
            try
            {
                yield return null;

                var cat = Resources.Load<CharacterStageCatalogSO>("Data/CharacterStageCatalog");
                Assert.IsNotNull(cat, "시드 CharacterStageCatalog 로드(자산 직렬화·스크립트 바인딩 정상).");
                var expected = cat.Resolve(CharId);

                // 슬롯 authored 기본을 비항등으로 두어 합성을 관찰(첫 Enter 직전에 baseline 캡처됨).
                var rt = view.SlotC.image.rectTransform;
                rt.localScale = Vector3.one;
                rt.anchoredPosition = new Vector2(100f, 50f);
                var baseScale = rt.localScale;
                var basePos = rt.anchoredPosition;

                var req = new CompletionHandle();
                EventBus.Publish(new ShowCharacterCommand(CharSlot.C, CharAction.Enter, CharId, Emote, 0f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(baseScale * expected.Scale, rt.localScale, "히로인 스케일 배율이 슬롯 기본에 적용.");
                Assert.AreEqual(basePos + expected.Offset, rt.anchoredPosition, "히로인 오프셋이 슬롯 기본에 합성.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
