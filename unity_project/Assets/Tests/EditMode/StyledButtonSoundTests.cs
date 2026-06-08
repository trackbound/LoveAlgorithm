using NUnit.Framework;
using UnityEngine;
using LoveAlgo.UI; // StyledButton, UiSoundSO

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// step 2 검증: 순수 <see cref="StyledButton.ResolveSfx"/>. 역할(General/Choice/Silent)×호버/클릭이
    /// UiSound 테이블의 올바른 항목으로 매핑되는지, table/항목 부재 시 무음(null/빈값)인지.
    /// 발행·재생(EventBus·AudioManager)은 범위 밖 — 여기선 이름 해석만(순수층/얇은 어댑터 분리).
    /// </summary>
    [TestFixture]
    public class StyledButtonSoundTests
    {
        // private [SerializeField] 필드를 테스트에서 채운다(JsonUtility는 직렬화 필드명으로 주입 — 프로덕션 setter 불필요).
        static UiSoundSO MakeTable()
        {
            var t = ScriptableObject.CreateInstance<UiSoundSO>();
            JsonUtility.FromJsonOverwrite(
                "{\"buttonHover\":\"bH\",\"buttonClick\":\"bC\",\"choiceHover\":\"cH\",\"choiceClick\":\"cC\"}", t);
            return t;
        }

        [Test]
        public void General_Maps_To_Button_Entries()
        {
            var t = MakeTable();
            Assert.AreEqual("bH", StyledButton.ResolveSfx(StyledButton.UiSoundRole.General, true, t));
            Assert.AreEqual("bC", StyledButton.ResolveSfx(StyledButton.UiSoundRole.General, false, t));
            Object.DestroyImmediate(t);
        }

        [Test]
        public void Choice_Maps_To_Choice_Entries()
        {
            var t = MakeTable();
            Assert.AreEqual("cH", StyledButton.ResolveSfx(StyledButton.UiSoundRole.Choice, true, t));
            Assert.AreEqual("cC", StyledButton.ResolveSfx(StyledButton.UiSoundRole.Choice, false, t));
            Object.DestroyImmediate(t);
        }

        [Test]
        public void Silent_Always_Null()
        {
            var t = MakeTable();
            Assert.IsNull(StyledButton.ResolveSfx(StyledButton.UiSoundRole.Silent, true, t));
            Assert.IsNull(StyledButton.ResolveSfx(StyledButton.UiSoundRole.Silent, false, t));
            Object.DestroyImmediate(t);
        }

        [Test]
        public void Null_Table_Returns_Null()
        {
            Assert.IsNull(StyledButton.ResolveSfx(StyledButton.UiSoundRole.General, true, null));
            Assert.IsNull(StyledButton.ResolveSfx(StyledButton.UiSoundRole.Choice, false, null));
        }

        [Test]
        public void Empty_Entries_Are_Silent_For_Caller_Guard()
        {
            // 기본(빈) 테이블 → ResolveSfx는 빈 문자열, 발행은 호출 측 IsNullOrEmpty 가드가 막는다.
            var t = ScriptableObject.CreateInstance<UiSoundSO>();
            Assert.IsTrue(string.IsNullOrEmpty(StyledButton.ResolveSfx(StyledButton.UiSoundRole.General, true, t)));
            Object.DestroyImmediate(t);
        }
    }
}
