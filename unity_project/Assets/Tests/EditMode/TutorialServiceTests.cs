using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Tutorial;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 튜토리얼 순수 규칙 검증 — 클릭 진행/제한 게이트(기획 "그냥 넘어가기 안됨"),
    /// {{Player}} 치환(케이스 무시·폴백), 완료 플래그(PlayerPrefs, 키 격리), 시드 에셋 정합.
    /// </summary>
    public class TutorialServiceTests
    {
        [Test]
        public void Step_Without_Requirement_Advances_On_Any_Click()
        {
            var step = new TutorialSequenceSO.Step { requiredClickAnchor = "" };
            Assert.IsTrue(TutorialService.AdvancesOnAnyClick(step));
            Assert.IsFalse(TutorialService.IsRequiredClick(step, "ShopButton"));
        }

        [Test]
        public void Restricted_Step_Gates_To_Required_Anchor_Only()
        {
            var step = new TutorialSequenceSO.Step { requiredClickAnchor = "ShopButton" };
            Assert.IsFalse(TutorialService.AdvancesOnAnyClick(step), "제한 스텝은 아무 클릭으로 진행 불가");
            Assert.IsTrue(TutorialService.IsRequiredClick(step, "shopbutton"), "케이스 무시 매칭");
            Assert.IsTrue(TutorialService.AllowsClickThrough(step, "ShopButton"), "지정 버튼만 패스스루");
            Assert.IsFalse(TutorialService.AllowsClickThrough(step, "ShopBack"), "다른 앵커는 차단");
            Assert.IsFalse(TutorialService.IsRequiredClick(step, null));
        }

        [Test]
        public void ResolveText_Replaces_Player_Token_Case_Insensitive()
        {
            Assert.AreEqual("안녕, 감독! 감독의 하루", TutorialService.ResolveText("안녕, {{Player}}! {{player}}의 하루", "감독"));
            Assert.AreEqual("안녕, 플레이어!", TutorialService.ResolveText("안녕, {{Player}}!", ""), "이름 미설정 폴백(스토리 선례)");
            Assert.AreEqual("", TutorialService.ResolveText(null, "감독"));
        }

        [Test]
        public void Flag_Roundtrip_And_Empty_Key_Always_Replays()
        {
            const string key = "Tutorial_TestOnly_Tmp";
            try
            {
                TutorialFlag.Reset(key);
                Assert.IsFalse(TutorialFlag.IsDone(key));
                TutorialFlag.MarkDone(key);
                Assert.IsTrue(TutorialFlag.IsDone(key));
                Assert.IsFalse(TutorialFlag.IsDone(""), "빈 키 = 항상 재생(데브)");
            }
            finally { TutorialFlag.Reset(key); }
        }

        [Test]
        public void Seeded_Sequence_Matches_Plan_Contract()
        {
            var so = Resources.Load<TutorialSequenceSO>("Data/Tutorial_ScheduleIntro");
            Assert.IsNotNull(so, "시드 에셋 존재(Tools/Tutorial/Seed) — Resources/Data/Tutorial_ScheduleIntro");
            Assert.AreEqual(27, so.Steps.Count, "기획서 27스텝");
            Assert.AreEqual("Tutorial_ScheduleIntro", so.prefsKey);

            int required = 0;
            foreach (var step in so.Steps)
            {
                Assert.IsFalse(string.IsNullOrEmpty(step.text), "빈 대사 없음");
                if (!string.IsNullOrEmpty(step.requiredClickAnchor)) required++;
            }
            Assert.AreEqual(2, required, "강제 클릭 2곳(상점 진입/돌아가기 — 기획 '그냥 넘어가기 안됨')");
            Assert.Greater(so.Steps[0].appearDelay, 1.9f, "첫 스텝 진입 2초 후(기획 p8)");
            Assert.Greater(so.Steps[26].autoAdvanceSeconds, 3.9f, "마지막 스텝 4초 후 자동 종료(기획 p34)");
        }
    }
}
