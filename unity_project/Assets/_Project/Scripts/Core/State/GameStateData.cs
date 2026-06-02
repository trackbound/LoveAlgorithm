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
        // 그날 남은 자유행동 횟수(낮/밤=ActionsPerDay). 데이루프 진행 상태이므로 세이브 직렬화(§7).
        // 기본 0 — 새 게임/하루 시작 시 DayLoop.BeginRun/AdvanceDay가 ActionsPerDay로 채운다
        // (ActionsPerDay는 Data 레이어 GameConstants 소관이라 Core인 이 모델에서 기본값으로 못 박지 않는다).
        public int remainingActions;

        // 오늘 수행한 '1일 1회 제한' 스케줄 id 집합(ScheduleType.ToString()). 하루 전환 시 비워진다.
        // 도메인 규칙(상하차 등 isLimited 재수행 차단)이므로 상태에 두고 세이브에 직렬화 — 하루 중 세이브/재로드
        // 시에도 제한이 정확히 유지된다(§5 Loading 1일1회). Schedule 타입을 모르는 Core라 문자열 id로 보관.
        public List<string> usedLimitedToday = new();

        // 호감도(히로인id→점수) / 플래그(이름→bool). dict 대용 엔트리 리스트.
        public List<IntEntry> lovePoints = new();
        public List<BoolEntry> flags = new();

        // 호감도 카테고리 포인트 추적 (Affinity 공식 입력). 구 HeroinePointTracker의 static dict를
        // 대체 — 런타임 상태이므로 세이브에 직렬화. (REWRITE_FEATURE_INVENTORY §4)
        public List<HeroinePoints> heroinePoints = new();
        // 이벤트별 선택 히로인(eventTag→heroineId). Event3 재선택 +2 보정 판정용.
        public List<StringEntry> eventChoices = new();

        // 같은 날 중복 구매 페널티 추적(아이템 DuplicateTag→그날 사용 횟수). 2회차부터 효과 반감(Shop §5).
        // 런타임 상태이므로 세이브 직렬화 — 하루 중 세이브/재로드 시에도 페널티가 정확히 유지된다. 날짜 바뀌면 비워진다.
        public List<IntEntry> dailyDuplicateUsage = new();
        public int lastDuplicateDay;

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
