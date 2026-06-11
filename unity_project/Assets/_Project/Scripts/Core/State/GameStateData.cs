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
        public string password = ""; // 잠금화면 비밀번호(LockScreen FirstSetup에서 설정). 빈=미설정. (ADR-013 Overlay)
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

        // 현재 화면 페이즈(ADR-013). 런타임 전용 — 부팅 리셋(새 GameStateData 시 기본 Schedule), 세이브 비직렬화
        // ([NonSerialized]). 화면 상태는 부팅/로드 시 항상 리셋되므로 세이브 스키마 무변.
        [NonSerialized] public ScreenPhase phase = ScreenPhase.Schedule;

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

        // ── 메신저 (기획서 2026-06-11, REWRITE_FEATURE_INVENTORY §7 MessengerSaveData 예약 이행) ──
        // 시퀀스 도착/읽음/선택 인덱스만 영속하고 대화 이력 전문은 저장하지 않는다 — 시퀀스 CSV 정의에서
        // 재구성하므로 작가가 텍스트를 고쳐도 세이브가 호환된다(감독 결정 2026-06-11). 가산적 확장이라
        // 구버전 세이브 로드 시 기본값(빈 목록/0)으로 채워져 마이그레이션 무해.
        public List<MessengerSeqRecord> messengerSeqs = new();

        // 플레이어 메신저 프로필(기획서: 프로필/배경 4~5종 중 선택 + 상태메시지 직접 입력. 빈 상메=기본 문구 표시).
        public int messengerProfileImage;
        public int messengerProfileBg;
        public string messengerStatusMessage = "";

        [Serializable] public struct IntEntry { public string key; public int value; }
        [Serializable] public struct BoolEntry { public string key; public bool value; }
        [Serializable] public struct StringEntry { public string key; public string value; }

        /// <summary>
        /// 메신저 시퀀스 1건의 영속 기록. roomId는 카탈로그 정의의 비정규화 사본(세이브 자족 — 방별 조회용).
        /// choices = 시퀀스 내 선택지 그룹 등장 순서대로 고른 옵션 인덱스(이력 재구성 입력).
        /// </summary>
        [Serializable]
        public class MessengerSeqRecord
        {
            public string seqId = "";
            public string roomId = "";
            public int deliveredDay;
            public bool read;
            public List<int> choices = new();
        }

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
