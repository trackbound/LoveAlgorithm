using NUnit.Framework;
using LoveAlgo.Core; // PlayerNameFormat.PlayerSpeakerId
using LoveAlgo.UI;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// 로그 그룹핑 규칙(목업 동결) 단위테스트: 진행(대사 한 줄) 단위로 박스가 나뉜다 — 같은 화자가 연속으로
    /// 말해도 다음 진행이면 새 박스(목업 박스2). 한 박스의 여러 줄은 한 행 본문의 \n으로 표현(목업 박스1).
    /// 종류 판별(독백/주인공/캐릭터)은 화자·예약 ID로.
    /// </summary>
    public class DialogueLogStoreTests
    {
        [SetUp] public void SetUp() => DialogueLogStore.Reset();
        [TearDown] public void TearDown() => DialogueLogStore.Reset();

        [Test]
        public void SameSpeakerConsecutive_SplitsPerLine()
        {
            DialogueLogStore.Append("로아", "c01", "이야기는 내일이야!");
            DialogueLogStore.Append("로아", "c01", "다음 진행이면 새 박스가 생깁니다.");

            Assert.AreEqual(2, DialogueLogStore.Count, "같은 화자라도 진행마다 새 박스(목업 박스2)");
            Assert.AreEqual(DialogueLogKind.Character, DialogueLogStore.Entries[0].Kind);
        }

        [Test]
        public void MultiLineWithinOneAdvance_StaysOneBox()
        {
            // 목업 박스1: 한 CSV 행 본문이 \n으로 두 줄을 담으면 한 박스(행 병합이 아님).
            DialogueLogStore.Append("로아", "c01", "이야기는 내일이야!\n같은 신이면 같은 박스에 들어갑니다.");

            Assert.AreEqual(1, DialogueLogStore.Count);
            Assert.AreEqual(2, DialogueLogStore.Entries[0].Text.Split('\n').Length);
        }

        [Test]
        public void SpeakerChange_OpensNewBox()
        {
            DialogueLogStore.Append("로아", "c01", "안녕!");
            DialogueLogStore.Append("교수님", null, "수업 시작하지.");
            DialogueLogStore.Append("로아", "c01", "다시 나!");

            Assert.AreEqual(3, DialogueLogStore.Count, "화자 바뀔 때마다 새 박스(되돌아와도 새 박스)");
        }

        [Test]
        public void ConsecutiveNarration_SplitsPerLine_AndPlayerIsSeparateKind()
        {
            DialogueLogStore.Append("", null, "데이터 로드가 끝나자마자 하는 첫 마디가 잔소리라니...");
            DialogueLogStore.Append(null, null, "주인공이 속으로 생각하는 나레이션입니다.");
            DialogueLogStore.Append("철수", PlayerNameFormat.PlayerSpeakerId, "안 졸려. 잠깐 이야기하자.");

            Assert.AreEqual(3, DialogueLogStore.Count, "독백도 진행 단위로 분리");
            Assert.AreEqual(DialogueLogKind.Narration, DialogueLogStore.Entries[0].Kind);
            Assert.AreEqual(DialogueLogKind.Narration, DialogueLogStore.Entries[1].Kind);
            Assert.AreEqual(DialogueLogKind.Player, DialogueLogStore.Entries[2].Kind, "주인공 = 예약 ID로 판별");
            Assert.AreEqual("철수", DialogueLogStore.Entries[2].Speaker, "치환된 입력 이름이 표시명");
        }

        [Test]
        public void Reset_ClearsAll()
        {
            DialogueLogStore.Append("로아", "c01", "안녕!");
            DialogueLogStore.Reset();
            Assert.AreEqual(0, DialogueLogStore.Count);
        }
    }
}
