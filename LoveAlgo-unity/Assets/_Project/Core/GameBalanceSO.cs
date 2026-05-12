using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo
{
    /// <summary>
    /// 게임 밸런스 ScriptableObject
    /// 히로인 설정, 타임라인, 밸런스 수치를 인스펙터에서 편집할 수 있다
    /// </summary>
    [CreateAssetMenu(fileName = "GameBalance", menuName = "LoveAlgo/Game Balance")]
    public class GameBalanceSO : ScriptableObject
    {
        // ── 직렬화용 내부 클래스 ──

        [System.Serializable]
        public class HeroineEntry
        {
            public string id;
            public string displayName;
            public int endingThreshold;
            public string preferredStat;
        }

        [System.Serializable]
        public class DayEntry
        {
            public int day;
            public DayType type;
            public StoryArc arc;
            public string eventTag;
            public int eventPoints;
        }

        // ── 데이터 ──

        [Header("히로인 설정")]
        [SerializeField] List<HeroineEntry> heroines = new();

        [Header("타임라인 (30일)")]
        [SerializeField] List<DayEntry> timeline = new();

        [Header("밸런스")]
        [SerializeField] int actionsPerDay = 2;
        [SerializeField] int maxDay = 30;
        [SerializeField] int endingLoveThreshold = 30;
        [SerializeField] int minInvestMoney = 30000;

        // ── 접근자 ──

        public IReadOnlyList<HeroineEntry> Heroines => heroines;
        public IReadOnlyList<DayEntry> Timeline => timeline;
        public int ActionsPerDay => actionsPerDay;
        public int MaxDay => maxDay;
        public int EndingLoveThreshold => endingLoveThreshold;
        public int MinInvestMoney => minInvestMoney;
    }
}
