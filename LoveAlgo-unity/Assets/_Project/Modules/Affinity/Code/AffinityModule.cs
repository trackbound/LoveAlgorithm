using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Story;
using UnityEngine;

namespace LoveAlgo.Modules.Affinity
{
    /// <summary>
    /// 호감도 모듈 진입점.
    /// 기존 정적 클래스 <see cref="AffinityCalculator"/>, <see cref="HeroinePointTracker"/>를
    /// IAffinity 인터페이스로 래핑하고, 변경 시 EventBus에 AffinityChangedEvent 발행.
    /// 씬 하이어라키: _Modules/AffinityModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class AffinityModule : MonoBehaviour, IAffinity
    {
        void Awake()
        {
            Services.Register<IAffinity>(this);
        }

        void OnDestroy()
        {
            if (Services.TryGet<IAffinity>() == (IAffinity)this)
                Services.Unregister<IAffinity>();
        }

        public AffinityInfo Get(string heroineId) => AffinityCalculator.GetAffinity(heroineId);
        public AffinityInfo[] GetAll() => AffinityCalculator.GetAllAffinities();

        public void AddPoint(string heroineId, PointCategory category, int amount)
        {
            if (amount == 0 || string.IsNullOrEmpty(heroineId)) return;
            HeroinePointTracker.AddPoint(heroineId, category, amount);
            AffinityCalculator.SyncToGameState(heroineId);
            int newTotal = AffinityCalculator.GetTotalScore(heroineId);
            EventBus.Publish(new AffinityChangedEvent(heroineId, category, amount, newTotal));
        }

        public void RecordEventChoice(string heroineId, string eventTag, int basePoints)
        {
            if (string.IsNullOrEmpty(heroineId)) return;
            HeroinePointTracker.RecordEventChoice(heroineId, eventTag, basePoints);
            AffinityCalculator.SyncToGameState(heroineId);
            int newTotal = AffinityCalculator.GetTotalScore(heroineId);
            EventBus.Publish(new AffinityChangedEvent(heroineId, PointCategory.Event, basePoints, newTotal));
        }

        public string DetermineEndingHeroine() => AffinityCalculator.DetermineEndingHeroine();
        public bool IsHappyEnding(string heroineId) => AffinityCalculator.IsHappyEnding(heroineId);

        public bool IsRoaConfessionUnlocked()
        {
            var gs = GameState.Instance;
            return gs != null && gs.GetStat("Fatigue") >= 70;
        }

        public EndingType GetEnding(string confessedHeroineId)
        {
            if (string.IsNullOrEmpty(confessedHeroineId))
                return EndingType.NoConfession;

            // 로아: 고백 게이트 충족 + 임계치 충족 시 메리배드. 미충족이면 NoConfession 처리.
            if (confessedHeroineId == "Roa")
            {
                if (IsRoaConfessionUnlocked() && AffinityCalculator.IsHappyEnding("Roa"))
                    return EndingType.RoaMeriBad;
                return EndingType.NoConfession;
            }

            bool happy = AffinityCalculator.IsHappyEnding(confessedHeroineId);
            return confessedHeroineId switch
            {
                "Yeun"   => happy ? EndingType.HappyYeun   : EndingType.SadYeun,
                "Daeun"  => happy ? EndingType.HappyDaeun  : EndingType.SadDaeun,
                "Bom"    => happy ? EndingType.HappyBom    : EndingType.SadBom,
                "Heewon" => happy ? EndingType.HappyHeewon : EndingType.SadHeewon,
                _ => EndingType.None,
            };
        }
    }
}
