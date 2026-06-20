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

        // 현재 화면 페이즈(ADR-013). 런타임 전용 — 부팅 리셋(새 GameStateData 시 기본 Story = 순수 선형 VN 진입),
        // 세이브 비직렬화([NonSerialized]). 화면 상태는 부팅/로드 시 항상 리셋되므로 세이브 스키마 무변.
        [NonSerialized] public ScreenPhase phase = ScreenPhase.Story;

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

        // 선택 시 기록된 마커 태그(순서 보존). 조건 원자 Chose:태그가 조회 — 작가의 과거-선택 분기.
        // (인벤토리 §7 SaveData의 ChoiceHistory 이행. eventChoices=Affinity Event3 보정과 별개.)
        // 가산적 확장이라 구버전 세이브는 빈 목록으로 로드 = 마이그레이션 무해.
        public List<string> choiceHistory = new();

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

        // ── 스토리 위치 (스토리 중 세이브 → 로드 시 그 장면 재개. 2026-06-13) ──
        // storyScriptId: "prologue" 또는 저녁 이벤트 CSV 상대경로(GameManager 씨임의 scriptName과 동일 규약).
        // 빈 문자열 = 스토리 중 아님(스케줄 재개 — 종전 동작). NarrativeController가 대기 라인(Text/Choice)에서
        // 기록하고 정상 종료 시 비운다. 가산적 확장 — 구버전 세이브는 기본값(빈/0)으로 로드돼 마이그레이션 무해.
        public string storyScriptId = "";
        public int storyLineIndex;

        // 무대 스냅샷(장면 정체성: BG/BGM/슬롯 캐릭터). 엔진이 명령 발행 시점에 "해석된 코드ID"로 미러 —
        // 별칭 카탈로그가 바뀌어도 복원이 흔들리지 않는다. 연출 순간값(틴트/흔들림)은 비저장.
        // CG는 storyCg로 저장(대사창이 CG 위에 떠 진행되는 동안 오토세이브가 잡힐 수 있어 복원 필요).
        public string storyBg = "";
        public string storyBgm = "";
        public List<StoryCharRecord> storyChars = new();

        // ── 연출 지속 상태(스테이지 상태 세이브, 2026-06-17) ──
        // 로드 시 장면 시각 동일 재현: BG/Char에 더해 화면 색 보정/눈꺼풀 닫힘/SD·Overlay·CG 레이어를 미러.
        // 발행 직전 해석된 최종값으로 기록 — 별칭/튜닝 변경 면역. 흔들림(순간값)만 비저장.
        // 가산적 확장이라 구버전 세이브는 기본값(0/false/빈)으로 로드 = 마이그레이션 무해.
        public float storyTintR;
        public float storyTintG;
        public float storyTintB;
        public float storyTintA; // > 0 이면 활성. Clear 발행값 = (0,0,0,0)
        public bool storyEyeClosed; // Close/CloseImmediate=true, Open=false (Blink는 순간이라 상태 불변)
        public string storySd = "";      // 현재 SD 레이어 이름(해석된 코드ID). 빈=없음
        public string storyOverlay = ""; // 현재 Overlay 레이어 이름(해석된 코드ID). 빈=없음
        public string storyCg = "";      // 현재 CG 레이어 이름(해석된 코드ID). 빈=없음. CG 표시 중 세이브(대사창 위 CG)→로드 복원용
        public string storyRoaDevice = ""; // 로아 디바이스(pc/모바일). 빈=미설정(컨트롤러 기본). 가산 확장

        /// <summary>스토리 무대 슬롯 1칸의 캐릭터 기록. slot = CharSlot enum 정수값(L=0/C=1/R=2).</summary>
        [Serializable]
        public class StoryCharRecord
        {
            public int slot;
            public string id = "";
            public string emote = "";
        }

        // ── 랜덤가챠 (기획서 2026-06-12 — 퍼즐 조각 수집형) ──
        // 보유 조각 인덱스 목록(조각 정의는 GachaTuningSO — 상태는 인덱스만, 가산적 확장이라 구세이브 무해).
        // 추첨은 미보유 풀 한정이라 중복 인덱스는 생기지 않는다(GachaPuzzleService가 강제).
        public List<int> gachaOwnedPieces = new();
        // 완성 후 추가 구매 누적(업적: +5 퍼즐 콜렉터 / +10 퍼즐 마스터 — 호칭은 flags에 영속).
        public int gachaBonusPurchases;

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
