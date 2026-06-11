using NUnit.Framework;
using LoveAlgo.Messenger;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 메신저 선택지 효과 → Flow 명령 변환(순수) 검증 — Love는 Dialogue 카테고리 위임(정본 단일화),
    /// 그 외는 Flow 문법 그대로 통과.
    /// </summary>
    public class MessengerEffectMapperTests
    {
        [Test]
        public void Love_Delegates_To_Affinity_Dialogue_Category()
        {
            var commands = MessengerEffectMapper.ToFlowCommands(new[] { "Love:로아:1" });
            CollectionAssert.AreEqual(new[] { "Affinity:Point:로아:Dialogue:1" }, commands);
        }

        [Test]
        public void AddLove_Alias_And_Case_Insensitive()
        {
            var commands = MessengerEffectMapper.ToFlowCommands(new[] { "addlove:c01:2" });
            CollectionAssert.AreEqual(new[] { "Affinity:Point:c01:Dialogue:2" }, commands);
        }

        [Test]
        public void Non_Love_Effects_Pass_Through_Unchanged()
        {
            var commands = MessengerEffectMapper.ToFlowCommands(new[] { "Flag:Promised", "Love:roa:1" });
            CollectionAssert.AreEqual(new[] { "Flag:Promised", "Affinity:Point:roa:Dialogue:1" }, commands);
        }

        [Test]
        public void Null_And_Empty_Are_Ignored()
        {
            Assert.AreEqual(0, MessengerEffectMapper.ToFlowCommands(null).Count);
            Assert.AreEqual(0, MessengerEffectMapper.ToFlowCommands(new[] { "", null }).Count);
        }
    }
}
