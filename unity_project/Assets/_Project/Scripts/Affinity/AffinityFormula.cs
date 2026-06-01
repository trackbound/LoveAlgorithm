using LoveAlgo.Core;

namespace LoveAlgo.Affinity
{
    /// <summary>포인트 카테고리 (이벤트/대화/선물/미니게임). 카테고리 상한은 콘텐츠가 강제. (인벤토리 §4)</summary>
    public enum PointCategory { Event, Dialogue, Gift, MiniGame }

    /// <summary>호감도 티어 (실시간 표시용). (인벤토리 §4)</summary>
    public enum AffinityTier { Stranger, Acquaintance, Friend, CloseFriend, Love }

    /// <summary>
    /// 호감도 계산 엔진 (REWRITE_FEATURE_INVENTORY §4 그대로 재현).
    ///
    /// 구 <c>AffinityCalculator</c>(static + GameState.Instance 의존)와 <c>HeroinePointTracker</c>(static dict)를
    /// 대체한다. 상태는 전부 <see cref="GameStateSO"/>(Data=세이브 직렬화)에 두고, 이 클래스는
    /// 상태를 인자로 받는 순수 함수만 제공한다 (Service Locator/싱글톤 없음, ADR-007).
    ///
    /// 총점 = 기본점수(Event+Dialogue+Gift+MiniGame) + 보너스.
    ///   - 로아: 피로 보너스(70~79:+3 / 80~89:+6 / 90~100:+10).
    ///   - 그 외: 선호 스탯 보너스(단독1등:+3 / 공동1등:+1 / 2등이하:0).
    ///
    /// 임계치/선호스탯은 1차 슬라이스에서 검증된 폴백 상수표를 내장한다.
    /// 2차에서 GameBalance.asset(Definition)을 연결하면 이 표를 대체한다.
    /// </summary>
    public static class AffinityFormula
    {
        public readonly struct HeroineDef
        {
            public readonly string Id;
            public readonly int Threshold;
            public readonly string PreferredStat;
            public HeroineDef(string id, int threshold, string preferredStat)
            {
                Id = id; Threshold = threshold; PreferredStat = preferredStat;
            }
        }

        // 인덱스 0 = 로아(히든 루트). 순서는 구 GameConstants와 동일.
        // 검증된 폴백 상수표(인벤토리 §4). GameBalance.asset이 없거나 비었을 때(헤드리스/테스트) 사용.
        static readonly HeroineDef[] FallbackHeroines =
        {
            new("Roa",      46, "Fatigue"),
            new("HaYeEun",  32, "Str"),
            new("SeoDaEun", 35, "Int"),
            new("LeeBom",   39, "Soc"),
            new("DoHeewon", 43, "Per"),
        };

        // 활성 정의표. 부팅 시 Configure(GameBalanceSO)로 교체, 미설정 시 폴백.
        static HeroineDef[] Heroines = FallbackHeroines;

        // 스탯 보너스 산정 대상(피로 제외 4대 스탯).
        static readonly string[] CombatStats = { "Str", "Int", "Soc", "Per" };

        const string RoaId = "Roa";

        // ── 정의 주입 (Definition 소스 연결) ───────────────────

        /// <summary>
        /// GameBalance.asset(Definition)으로 히로인 정의표를 교체한다.
        /// 부팅 시 1회 호출(호출 주체=매니저, 후속 마일스톤). 순수 함수 원칙 유지를 위해
        /// 이 메서드만 SO를 읽고(Resources.Load는 호출자 몫), 채점 함수는 GameStateSO만 받는다.
        /// 인자가 null이거나 히로인 항목이 없으면 폴백 상수표로 되돌린다.
        /// </summary>
        public static void Configure(GameBalanceSO balance)
        {
            if (balance == null || balance.Heroines.Count == 0)
            {
                Heroines = FallbackHeroines;
                return;
            }

            var defs = new HeroineDef[balance.Heroines.Count];
            for (int i = 0; i < balance.Heroines.Count; i++)
            {
                var h = balance.Heroines[i];
                defs[i] = new HeroineDef(h.id, h.endingThreshold, h.preferredStat);
            }
            Heroines = defs;
        }

        /// <summary>정의표를 검증된 폴백 상수표로 되돌린다(테스트 격리/부팅 리셋용).</summary>
        public static void ResetToFallback() => Heroines = FallbackHeroines;

        // ── 정의 조회 ──────────────────────────────────────────

        public static int Count => Heroines.Length;

        public static int IndexOf(string heroineId)
        {
            for (int i = 0; i < Heroines.Length; i++)
                if (Heroines[i].Id == heroineId) return i;
            return -1;
        }

        public static int ThresholdOf(string heroineId)
        {
            int idx = IndexOf(heroineId);
            return idx < 0 ? 0 : Heroines[idx].Threshold;
        }

        public static string HeroineIdAt(int index) =>
            index >= 0 && index < Heroines.Length ? Heroines[index].Id : null;

        // ── 보너스 계산 ────────────────────────────────────────

        /// <summary>선호 스탯 보너스(로아 제외): 단독 1등 +3 / 공동 1등 +1 / 2등 이하 0.</summary>
        public static int StatBonus(GameStateSO gs, int heroineIndex)
        {
            if (gs == null || heroineIndex < 0 || heroineIndex >= Heroines.Length) return 0;

            string preferred = Heroines[heroineIndex].PreferredStat;
            int preferredValue = gs.GetStat(preferred);
            if (preferredValue <= 0) return 0;

            int maxValue = 0, maxCount = 0;
            foreach (var statId in CombatStats)
            {
                int val = gs.GetStat(statId);
                if (val > maxValue) { maxValue = val; maxCount = 1; }
                else if (val == maxValue && val > 0) { maxCount++; }
            }

            if (preferredValue < maxValue) return 0; // 2등 이하
            return maxCount == 1 ? 3 : 1;            // 단독 1등 / 공동 1등
        }

        /// <summary>로아 피로 보너스: 70~79 +3 / 80~89 +6 / 90~100 +10.</summary>
        public static int RoaFatigueBonus(GameStateSO gs)
        {
            if (gs == null) return 0;
            int f = gs.GetStat("Fatigue");
            if (f >= 90) return 10;
            if (f >= 80) return 6;
            if (f >= 70) return 3;
            return 0;
        }

        // ── 점수 / 티어 ────────────────────────────────────────

        /// <summary>카테고리 합계(보너스 제외).</summary>
        public static int BasePoints(GameStateSO gs, string heroineId)
        {
            var p = FindPoints(gs, heroineId);
            return p?.Total ?? 0;
        }

        /// <summary>최종 총점 = 기본점수 + 보너스(로아=피로, 그 외=스탯).</summary>
        public static int TotalScore(GameStateSO gs, string heroineId)
        {
            int idx = IndexOf(heroineId);
            if (idx < 0 || gs == null) return 0;
            int bonus = heroineId == RoaId ? RoaFatigueBonus(gs) : StatBonus(gs, idx);
            return BasePoints(gs, heroineId) + bonus;
        }

        public static int EventSelections(GameStateSO gs, string heroineId)
        {
            var p = FindPoints(gs, heroineId);
            return p?.eventSelections ?? 0;
        }

        public static AffinityTier Tier(int score)
        {
            if (score >= 40) return AffinityTier.Love;
            if (score >= 30) return AffinityTier.CloseFriend;
            if (score >= 20) return AffinityTier.Friend;
            if (score >= 10) return AffinityTier.Acquaintance;
            return AffinityTier.Stranger;
        }

        /// <summary>엔딩 자격: 총점 ≥ 임계치 AND 이벤트 선택 ≥ 1회.</summary>
        public static bool IsEndingEligible(GameStateSO gs, string heroineId)
        {
            int idx = IndexOf(heroineId);
            if (idx < 0) return false;
            return TotalScore(gs, heroineId) >= Heroines[idx].Threshold
                   && EventSelections(gs, heroineId) >= 1;
        }

        // ── 엔딩 판정 ──────────────────────────────────────────

        /// <summary>
        /// 엔딩 히로인 결정 (인벤토리 §4 판정):
        ///   1. 로아 히든 우선: 피로 ≥70 AND 총점 ≥46 → Roa
        ///   2. 그 외: 이벤트 선택 ≥1 + 총점 ≥임계치 중 (총점-임계치) 마진 최대
        ///   3. 해당 없음 → null(노컨페션 노멀)
        /// </summary>
        public static string DetermineEndingHeroine(GameStateSO gs)
        {
            if (gs == null) return null;

            // 1. 로아 히든 루트 (우선)
            if (gs.GetStat("Fatigue") >= 70 && TotalScore(gs, RoaId) >= Heroines[0].Threshold)
                return RoaId;

            // 2. 나머지 히로인 중 마진 최대
            string best = null;
            int bestMargin = -1;
            for (int i = 1; i < Heroines.Length; i++)
            {
                string id = Heroines[i].Id;
                if (EventSelections(gs, id) < 1) continue;

                int total = TotalScore(gs, id);
                if (total >= Heroines[i].Threshold)
                {
                    int margin = total - Heroines[i].Threshold;
                    if (margin > bestMargin) { bestMargin = margin; best = id; }
                }
            }
            return best; // null → 노멀(고백 없음)
        }

        /// <summary>해피/새드 분기: 총점 ≥ 임계치 → Happy.</summary>
        public static bool IsHappyEnding(GameStateSO gs, string heroineId)
        {
            if (string.IsNullOrEmpty(heroineId)) return false;
            int idx = IndexOf(heroineId);
            if (idx < 0) return false;
            return TotalScore(gs, heroineId) >= Heroines[idx].Threshold;
        }

        // ── 포인트 변경 (상태 mutate) ──────────────────────────

        /// <summary>카테고리 포인트 가산 + lovePoints 동기화(CSV 조건 Love:Id&gt;=N 대비).</summary>
        public static void AddPoint(GameStateSO gs, string heroineId, PointCategory category, int value)
        {
            if (gs == null || IndexOf(heroineId) < 0) return;
            var p = GetOrCreate(gs, heroineId);
            switch (category)
            {
                case PointCategory.Event:    p.eventPt    += value; break;
                case PointCategory.Dialogue: p.dialoguePt += value; break;
                case PointCategory.Gift:     p.giftPt     += value; break;
                case PointCategory.MiniGame: p.miniGamePt += value; break;
            }
            SyncLove(gs, heroineId);
        }

        /// <summary>
        /// 이벤트에서 히로인 선택. 이벤트 포인트 부여 + 선택 횟수 증가 + 선택 기록.
        /// Event3에서 Event1/Event2와 같은 히로인 재선택 시 +2 (로아 제외). (인벤토리 §4)
        /// </summary>
        public static void RecordEventChoice(GameStateSO gs, string heroineId, string eventTag, int basePoints)
        {
            if (gs == null || IndexOf(heroineId) < 0) return;

            AddPoint(gs, heroineId, PointCategory.Event, basePoints);

            var p = GetOrCreate(gs, heroineId);
            p.eventSelections++;
            SetEventChoice(gs, eventTag, heroineId);

            if (eventTag == "Event3" && heroineId != RoaId)
            {
                if (GetEventChoice(gs, "Event1") == heroineId || GetEventChoice(gs, "Event2") == heroineId)
                    AddPoint(gs, heroineId, PointCategory.Event, 2);
            }
        }

        /// <summary>총점을 lovePoints에 반영(보너스 포함). 구 SyncToGameState 대응.</summary>
        public static void SyncLove(GameStateSO gs, string heroineId)
        {
            if (gs == null) return;
            gs.SetLove(heroineId, TotalScore(gs, heroineId));
        }

        public static void SyncAllLove(GameStateSO gs)
        {
            if (gs == null) return;
            foreach (var h in Heroines) SyncLove(gs, h.Id);
        }

        // ── 내부 헬퍼 (엔트리 리스트 ↔ dict 의미) ──────────────

        static GameStateData.HeroinePoints FindPoints(GameStateSO gs, string heroineId)
        {
            if (gs == null) return null;
            var list = gs.Data.heroinePoints;
            for (int i = 0; i < list.Count; i++)
                if (list[i].heroineId == heroineId) return list[i];
            return null;
        }

        static GameStateData.HeroinePoints GetOrCreate(GameStateSO gs, string heroineId)
        {
            var existing = FindPoints(gs, heroineId);
            if (existing != null) return existing;
            var created = new GameStateData.HeroinePoints { heroineId = heroineId };
            gs.Data.heroinePoints.Add(created);
            return created;
        }

        static string GetEventChoice(GameStateSO gs, string eventTag)
        {
            var list = gs.Data.eventChoices;
            for (int i = 0; i < list.Count; i++)
                if (list[i].key == eventTag) return list[i].value;
            return null;
        }

        static void SetEventChoice(GameStateSO gs, string eventTag, string heroineId)
        {
            var list = gs.Data.eventChoices;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].key == eventTag)
                {
                    list[i] = new GameStateData.StringEntry { key = eventTag, value = heroineId };
                    return;
                }
            }
            list.Add(new GameStateData.StringEntry { key = eventTag, value = heroineId });
        }
    }
}
