using System;
using LoveAlgo.Core;
using LoveAlgo.Story;

namespace LoveAlgo.Modules.Affinity
{
    /// <summary>
    /// 호감도 등급 (실시간 조회용)
    /// </summary>
    public enum AffinityTier
    {
        Stranger,       // 0~9점: 모르는 사이
        Acquaintance,   // 10~19점: 아는 사이
        Friend,         // 20~29점: 친구
        CloseFriend,    // 30~39점: 가까운 친구
        Love            // 40점 이상: 연인 후보
    }

    /// <summary>
    /// 히로인별 호감도 요약 정보
    /// </summary>
    public readonly struct AffinityInfo
    {
        /// <summary>히로인 ID</summary>
        public readonly string HeroineId;

        /// <summary>기본 포인트 합계 (이벤트+대화+선물+미니게임)</summary>
        public readonly int BasePoints;

        /// <summary>스탯 보정값 (선호스탯 1등=+3, 공동1등=+1)</summary>
        public readonly int StatBonus;

        /// <summary>특수 보정값 (로아 피로보정 등)</summary>
        public readonly int SpecialBonus;

        /// <summary>최종 총점 (BasePoints + StatBonus + SpecialBonus)</summary>
        public readonly int TotalScore;

        /// <summary>해당 히로인의 엔딩 필요 임계치</summary>
        public readonly int Threshold;

        /// <summary>임계치까지 남은 포인트 (음수면 초과 달성)</summary>
        public readonly int Remaining;

        /// <summary>현재 호감도 등급</summary>
        public readonly AffinityTier Tier;

        /// <summary>이벤트 선택 횟수</summary>
        public readonly int EventSelections;

        /// <summary>엔딩 자격 충족 여부</summary>
        public readonly bool IsEndingEligible;

        public AffinityInfo(string heroineId, int basePoints, int statBonus, int specialBonus,
            int threshold, int eventSelections)
        {
            HeroineId = heroineId;
            BasePoints = basePoints;
            StatBonus = statBonus;
            SpecialBonus = specialBonus;
            TotalScore = basePoints + statBonus + specialBonus;
            Threshold = threshold;
            Remaining = threshold - TotalScore;
            Tier = ScoreToTier(TotalScore);
            EventSelections = eventSelections;

            // 로아: 피로≥70 필수 / 나머지: 이벤트 1회 이상
            IsEndingEligible = TotalScore >= threshold && eventSelections >= 1;
        }

        static AffinityTier ScoreToTier(int score)
        {
            if (score >= 40) return AffinityTier.Love;
            if (score >= 30) return AffinityTier.CloseFriend;
            if (score >= 20) return AffinityTier.Friend;
            if (score >= 10) return AffinityTier.Acquaintance;
            return AffinityTier.Stranger;
        }
    }

    /// <summary>
    /// 호감도 계산 통합 엔진
    /// 
    /// HeroinePointTracker(카테고리별 포인트)와 GameState(스탯)를 조합하여
    /// 최종 호감도 점수, 등급, 엔딩 판정을 일원화합니다.
    /// 
    /// 공식:
    ///   총점 = 이벤트(max27) + 대화(max15) + 선물(max8) + 미니게임(max5)
    ///        + 스탯보정(+3/+1) + 피로보정(로아: +3/+6/+10)
    /// 
    /// 스탯 보정:
    ///   - 히로인 선호 스탯이 플레이어 4대 스탯 중 단독 1등 → +3
    ///   - 공동 1등 → +1
    ///   - 2등 이하 → +0
    /// 
    /// 로아 피로 보정:
    ///   - 피로 70~79 → +3
    ///   - 피로 80~89 → +6
    ///   - 피로 90~100 → +10
    /// </summary>
    public static class AffinityCalculator
    {
        #region 호감도 조회

        /// <summary>
        /// 히로인의 전체 호감도 정보 계산
        /// </summary>
        public static AffinityInfo GetAffinity(string heroineId)
        {
            int idx = Array.IndexOf(GameConstants.HeroineIds, heroineId);
            if (idx < 0)
                return new AffinityInfo(heroineId, 0, 0, 0, 0, 0);

            var gs = GameState.Instance;
            int basePoints = HeroinePointTracker.GetTotalPoint(heroineId);
            int statBonus = 0;
            int specialBonus = 0;

            if (gs != null)
            {
                if (heroineId == "Roa")
                    specialBonus = CalcRoaFatigueBonus(gs);
                else
                    statBonus = CalcStatBonus(gs, idx);
            }

            int threshold = GameConstants.EndingThresholds[idx];
            int eventSelections = HeroinePointTracker.GetEventSelectionCount(heroineId);

            return new AffinityInfo(heroineId, basePoints, statBonus, specialBonus,
                threshold, eventSelections);
        }

        /// <summary>
        /// 모든 히로인의 호감도 정보 일괄 계산
        /// </summary>
        public static AffinityInfo[] GetAllAffinities()
        {
            var result = new AffinityInfo[GameConstants.HeroineCount];
            for (int i = 0; i < GameConstants.HeroineCount; i++)
                result[i] = GetAffinity(GameConstants.HeroineIds[i]);
            return result;
        }

        /// <summary>
        /// 히로인의 최종 총점 (간단 조회)
        /// </summary>
        public static int GetTotalScore(string heroineId)
        {
            return GetAffinity(heroineId).TotalScore;
        }

        /// <summary>
        /// 히로인의 현재 등급 (간단 조회)
        /// </summary>
        public static AffinityTier GetTier(string heroineId)
        {
            return GetAffinity(heroineId).Tier;
        }

        /// <summary>
        /// 카테고리별 포인트 상세 조회
        /// </summary>
        public static (int eventPt, int dialoguePt, int giftPt, int miniGamePt) GetPointBreakdown(string heroineId)
        {
            return (
                HeroinePointTracker.GetPoint(heroineId, PointCategory.Event),
                HeroinePointTracker.GetPoint(heroineId, PointCategory.Dialogue),
                HeroinePointTracker.GetPoint(heroineId, PointCategory.Gift),
                HeroinePointTracker.GetPoint(heroineId, PointCategory.MiniGame)
            );
        }

        #endregion

        #region 엔딩 판정

        /// <summary>
        /// 엔딩 히로인 결정
        /// 
        /// 판정 우선순위:
        ///   1. 로아 히든 루트 (피로≥70 + 총점≥46)
        ///   2. 나머지 히로인 중 총점이 임계치 이상이면서 마진이 가장 높은 캐릭터
        ///   3. 해당 없으면 null (노멀 엔딩)
        /// 
        /// 필수 조건: 해당 히로인 이벤트 최소 1회 이상 참여
        /// </summary>
        public static string DetermineEndingHeroine()
        {
            var gs = GameState.Instance;
            if (gs == null) return null;

            // 히든 루트: 로아 (피로 ≥70 + 포인트 ≥ 46)
            var roaInfo = GetAffinity("Roa");
            if (gs.GetStat("Fatigue") >= 70 && roaInfo.TotalScore >= roaInfo.Threshold)
                return "Roa";

            // 나머지 히로인 (HaYeEun, SeoDaEun, LeeBom, DoHeewon)
            string best = null;
            int bestMargin = -1;

            for (int i = 1; i < GameConstants.HeroineCount; i++)
            {
                string id = GameConstants.HeroineIds[i];

                // 이벤트 최소 1회 이상 참여 필수
                if (HeroinePointTracker.GetEventSelectionCount(id) < 1)
                    continue;

                var info = GetAffinity(id);
                if (info.TotalScore >= info.Threshold)
                {
                    int margin = info.TotalScore - info.Threshold;
                    if (margin > bestMargin)
                    {
                        bestMargin = margin;
                        best = id;
                    }
                }
            }

            return best; // null → 노멀 엔딩
        }

        /// <summary>
        /// 해피/새드 엔딩 분기 판정
        /// </summary>
        public static bool IsHappyEnding(string heroineId)
        {
            if (string.IsNullOrEmpty(heroineId)) return false;

            var info = GetAffinity(heroineId);
            return info.TotalScore >= info.Threshold;
        }

        #endregion

        #region 보정 계산

        /// <summary>
        /// 스탯 보정 계산 (로아 제외)
        /// 선호스탯이 4대 스탯(Str/Int/Soc/Per) 중 최고이면 +3, 공동1등이면 +1
        /// </summary>
        public static int CalcStatBonus(GameState gs, int heroineIndex)
        {
            if (gs == null || heroineIndex < 0 || heroineIndex >= GameConstants.HeroineCount)
                return 0;

            string preferredStat = GameConstants.HeroinePreferredStat[heroineIndex];
            int preferredValue = gs.GetStat(preferredStat);
            if (preferredValue <= 0) return 0;

            // 전투 스탯(피로 제외) 중 최고값/최고 수 산출
            int maxValue = 0;
            int maxCount = 0;
            foreach (var statId in GameConstants.CombatStatIds)
            {
                int val = gs.GetStat(statId);
                if (val > maxValue)
                {
                    maxValue = val;
                    maxCount = 1;
                }
                else if (val == maxValue && val > 0)
                {
                    maxCount++;
                }
            }

            if (preferredValue < maxValue) return 0;   // 2등 이하
            if (maxCount == 1) return 3;                // 단독 1등
            return 1;                                    // 공동 1등
        }

        /// <summary>
        /// 스탯 보정 계산 (히로인 ID로 조회)
        /// </summary>
        public static int CalcStatBonus(string heroineId)
        {
            int idx = Array.IndexOf(GameConstants.HeroineIds, heroineId);
            if (idx < 0) return 0;
            if (heroineId == "Roa") return 0; // 로아는 스탯 보정 없음

            return CalcStatBonus(GameState.Instance, idx);
        }

        /// <summary>
        /// 로아 피로 보정 (70~79:+3 / 80~89:+6 / 90~100:+10)
        /// </summary>
        public static int CalcRoaFatigueBonus(GameState gs)
        {
            if (gs == null) return 0;
            int fatigue = gs.GetStat("Fatigue");
            if (fatigue >= 90) return 10;
            if (fatigue >= 80) return 6;
            if (fatigue >= 70) return 3;
            return 0;
        }

        #endregion

        #region GameState 동기화

        /// <summary>
        /// HeroinePointTracker의 총점을 GameState.lovePoints에 동기화
        /// CSV 스크립트의 조건 평가(Love:Roa>=30)가 정확히 작동하도록 보장
        /// </summary>
        public static void SyncToGameState(string heroineId)
        {
            var gs = GameState.Instance;
            if (gs == null) return;

            int totalScore = GetTotalScore(heroineId);
            gs.SetLove(heroineId, totalScore);
        }

        /// <summary>
        /// 전체 히로인의 포인트를 GameState에 동기화
        /// </summary>
        public static void SyncAllToGameState()
        {
            var gs = GameState.Instance;
            if (gs == null) return;

            foreach (var id in GameConstants.HeroineIds)
            {
                int totalScore = GetTotalScore(id);
                gs.SetLove(id, totalScore);
            }
        }

        #endregion
    }
}
