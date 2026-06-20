using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;  // GameStateData
using LoveAlgo.Story; // ScriptCursor, ScriptParser

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// 스토리 위치 세이브 스키마(🔴) 회귀: ① 신규 필드 JSON 왕복 ② 구버전 세이브(필드 부재) 로드 시
    /// 기본값(빈/0) = 마이그레이션 무해 ③ ScriptCursor 인덱스 점프(복원 재개 앵커) 경계.
    /// </summary>
    public class StoryPositionSchemaTests
    {
        [Test]
        public void StoryFields_JsonRoundTrip()
        {
            var d = new GameStateData
            {
                storyScriptId = "Event1.csv",
                storyLineIndex = 42,
                storyBg = "공대_강의실",
                storyBgm = "daily1",
            };
            d.storyChars.Add(new GameStateData.StoryCharRecord { slot = 1, id = "c01", emote = "41" });

            var back = JsonUtility.FromJson<GameStateData>(JsonUtility.ToJson(d));

            Assert.AreEqual("Event1.csv", back.storyScriptId);
            Assert.AreEqual(42, back.storyLineIndex);
            Assert.AreEqual("공대_강의실", back.storyBg);
            Assert.AreEqual("daily1", back.storyBgm);
            Assert.AreEqual(1, back.storyChars.Count);
            Assert.AreEqual(1, back.storyChars[0].slot);
            Assert.AreEqual("c01", back.storyChars[0].id);
            Assert.AreEqual("41", back.storyChars[0].emote);
        }

        [Test]
        public void StageStateFields_JsonRoundTrip()
        {
            var d = new GameStateData
            {
                storyTintR = 0.5f, storyTintG = 0.4f, storyTintB = 0.3f, storyTintA = 0.25f,
                storyEyeClosed = true,
                storySd = "sd_x",
                storyOverlay = "ov_y",
                storyCg = "cg_c01_01",
            };

            var back = JsonUtility.FromJson<GameStateData>(JsonUtility.ToJson(d));

            Assert.AreEqual(0.25f, back.storyTintA, 0.001f, "틴트 알파 왕복");
            Assert.AreEqual(0.5f, back.storyTintR, 0.001f);
            Assert.IsTrue(back.storyEyeClosed, "아이마스크 닫힘 왕복");
            Assert.AreEqual("sd_x", back.storySd);
            Assert.AreEqual("ov_y", back.storyOverlay);
            Assert.AreEqual("cg_c01_01", back.storyCg, "CG 레이어 왕복");
        }

        [Test]
        public void OldSave_WithoutStoryFields_LoadsAsDefaults()
        {
            // 구버전 세이브 모사 — 스토리 필드가 없는 JSON. JsonUtility는 부재 필드를 기본값으로 둔다.
            const string oldJson = "{\"playerName\":\"철수\",\"day\":3,\"money\":1000}";

            var d = JsonUtility.FromJson<GameStateData>(oldJson);

            Assert.AreEqual("철수", d.playerName, "기존 필드는 정상 로드");
            Assert.AreEqual("", d.storyScriptId, "부재 → 빈 = 스토리 밖(스케줄 재개, 종전 동작)");
            Assert.AreEqual(0, d.storyLineIndex);
            Assert.AreEqual("", d.storyBg);
            Assert.AreEqual("", d.storyBgm);
            Assert.IsNotNull(d.storyChars);
            Assert.AreEqual(0, d.storyChars.Count);
            Assert.AreEqual(0f, d.storyTintA, "부재 → 0 = 틴트 비활성");
            Assert.IsFalse(d.storyEyeClosed, "부재 → false = 눈 뜬 상태");
            Assert.AreEqual("", d.storySd);
            Assert.AreEqual("", d.storyOverlay);
        }

        [Test]
        public void ScriptCursor_JumpToIndex_BoundsAndMove()
        {
            var lines = ScriptParser.Parse(
                "LineID,Type,Speaker,Value,Next\n" +
                ",Text,로아,A,click\n" +
                ",Text,로아,B,click\n" +
                ",Flow,,End,>\n");
            var cursor = new ScriptCursor(lines);

            Assert.IsTrue(cursor.JumpToIndex(2), "유효 인덱스 점프");
            Assert.AreEqual(2, cursor.Index);

            Assert.IsFalse(cursor.JumpToIndex(-1), "음수 거부");
            Assert.IsFalse(cursor.JumpToIndex(lines.Count), "범위 밖 거부");
            Assert.AreEqual(2, cursor.Index, "실패 시 커서 불변");
        }
    }
}
