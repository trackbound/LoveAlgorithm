using System;
using System.Collections.Generic;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 런타임에 변하는 게임 상태(가변 Instance). 세이브 직렬화 대상.
    /// SO 에셋에 영구 저장 금지 — <see cref="GameStateSO"/>가 런타임으로만 보유(부팅 리셋). (ADR-012, dev_guide §4-1a)
    /// JsonUtility 호환을 위해 Dictionary 대신 직렬화 가능한 엔트리 리스트를 사용한다.
    /// </summary>
    [Serializable]
    public class GameStateData
    {
        public string playerName = "";
        public long money;

        // 플레이어 스탯 (REWRITE_FEATURE_INVENTORY §5: Str/Int/Soc/Per/Fatigue, 0~100)
        public int str;
        public int intel;
        public int soc;
        public int per;
        public int fatigue;

        public int day = 1;

        // 호감도(히로인id→점수) / 플래그(이름→bool). dict 대용 엔트리 리스트.
        public List<IntEntry> lovePoints = new();
        public List<BoolEntry> flags = new();

        // 호감도 카테고리 포인트 추적 (Affinity 공식 입력). 구 HeroinePointTracker의 static dict를
        // 대체 — 런타임 상태이므로 세이브에 직렬화. (REWRITE_FEATURE_INVENTORY §4)
        public List<HeroinePoints> heroinePoints = new();
        // 이벤트별 선택 히로인(eventTag→heroineId). Event3 재선택 +2 보정 판정용.
        public List<StringEntry> eventChoices = new();

        [Serializable] public struct IntEntry { public string key; public int value; }
        [Serializable] public struct BoolEntry { public string key; public bool value; }
        [Serializable] public struct StringEntry { public string key; public string value; }

        /// <summary>히로인 1명의 카테고리별 누적 포인트 + 이벤트 선택 횟수.</summary>
        [Serializable]
        public class HeroinePoints
        {
            public string heroineId;
            public int eventPt;
            public int dialoguePt;
            public int giftPt;
            public int miniGamePt;
            public int eventSelections;

            public int Total => eventPt + dialoguePt + giftPt + miniGamePt;
        }
    }
}
