using NUnit.Framework;
using LoveAlgo.Core; // PlayerNameFormat.PlayerSpeakerId
using LoveAlgo.UI;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// 로그 그룹핑 규칙(목업 주석 동결) 단위테스트: 같은 스크립트+연속 동일 화자 = 한 박스 누적,
    /// 화자/스크립트가 바뀌면 새 박스, 독백은 화자 무관 연속 병합, 주인공은 예약 ID로 별도 종류.
    /// </summary>
    public class DialogueLogStoreTests
    {
        [SetUp] public void SetUp() => DialogueLogStore.Reset();
        [TearDown] public void TearDown() => DialogueLogStore.Reset();

        [Test]
        public void SameSpeakerSameScript_MergesIntoOneBox()
        {
            DialogueLogStore.Append("pro", "로아", "c01", "이야기는 내일이야!");
            DialogueLogStore.Append("pro", "로아", "c01", "같은 신이면 같은 박스에 들어갑니다.");

            Assert.AreEqual(1, DialogueLogStore.Count, "연속 동일 화자 = 한 박스");
            Assert.AreEqual(2, DialogueLogStore.Entries[0].Lines.Count);
            Assert.AreEqual(DialogueLogKind.Character, DialogueLogStore.Entries[0].Kind);
        }

        [Test]
        public void SpeakerChange_OpensNewBox()
        {
            DialogueLogStore.Append("pro", "로아", "c01", "안녕!");
            DialogueLogStore.Append("pro", "교수님", null, "수업 시작하지.");
            DialogueLogStore.Append("pro", "로아", "c01", "다시 나!");

            Assert.AreEqual(3, DialogueLogStore.Count, "화자 바뀔 때마다 새 박스(되돌아와도 새 박스)");
        }

        [Test]
        public void ScriptBoundary_OpensNewBox_EvenSameSpeaker()
        {
            DialogueLogStore.Append("pro", "로아", "c01", "프롤로그 끝!");
            DialogueLogStore.Append("event1", "로아", "c01", "다음 스크립트에서 계속 얘기하면 새로운 박스가 생깁니다.");

            Assert.AreEqual(2, DialogueLogStore.Count, "스크립트 경계 = 새 박스(목업 규칙)");
        }

        [Test]
        public void Narration_MergesRegardlessOfEmptySpeaker_AndPlayerIsSeparateKind()
        {
            DialogueLogStore.Append("pro", "", null, "데이터 로드가 끝나자마자 하는 첫 마디가 잔소리라니...");
            DialogueLogStore.Append("pro", null, null, "속으로 생각하는 나레이션도 병합된다.");
            DialogueLogStore.Append("pro", "철수", PlayerNameFormat.PlayerSpeakerId, "안 졸려. 잠깐 이야기하자.");

            Assert.AreEqual(2, DialogueLogStore.Count);
            Assert.AreEqual(DialogueLogKind.Narration, DialogueLogStore.Entries[0].Kind);
            Assert.AreEqual(2, DialogueLogStore.Entries[0].Lines.Count, "연속 독백 병합(감독 승인)");
            Assert.AreEqual(DialogueLogKind.Player, DialogueLogStore.Entries[1].Kind, "주인공 = 예약 ID로 판별");
            Assert.AreEqual("철수", DialogueLogStore.Entries[1].Speaker, "치환된 입력 이름이 표시명");
        }

        [Test]
        public void Reset_ClearsAll()
        {
            DialogueLogStore.Append("pro", "로아", "c01", "안녕!");
            DialogueLogStore.Reset();
            Assert.AreEqual(0, DialogueLogStore.Count);
        }
    }
}
