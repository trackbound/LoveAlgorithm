using System;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Contracts;
using LoveAlgo.Core;
using LoveAlgo.Story;

namespace LoveAlgo.Modules.Affinity
{
    // C4-Phase A: PointCategory는 LoveAlgo.Contracts.Data로 이동.

    /// <summary>
    /// 히로인별 포인트 추적기
    /// 
    /// 기획서 기준:
    ///   - 이벤트 기본점수: 1차+3, 축제+4, 2차+6, MT+5, 3차+9 (총 27점)
    ///   - 대화 포인트: 15개, 전부 +1 (감점 없음)
    ///   - 선물 포인트: 2차/3차에서 계층별(+1~+5), 합계 최대 +8
    ///   - 미니게임 보너스: 1차 최대+2, 2차 최대+3 (총 5점)
    ///   - 스탯 보정: 선호 스탯 1등 +3 / 공동1등 +1 (고백 시 계산)
    ///   - 로아 피로 보정: 70~79:+3 / 80~89:+6 / 90~100:+10
    ///   - 히로인별 임계치: Roa=46, HaYeEun=32, SeoDaEun=35, LeeBom=39, DoHeewon=43
    /// </summary>
    public static class HeroinePointTracker
    {
        /// <summary>
        /// 히로인별 카테고리별 포인트
        /// Key: heroineId, Value: 카테고리별 점수
        /// </summary>
        static readonly Dictionary<string, Dictionary<PointCategory, int>> points = new();

        /// <summary>
        /// 히로인별 이벤트 선택 횟수 (기획서: 최소 1회 이상 이벤트 참여 필요)
        /// </summary>
        static readonly Dictionary<string, int> eventSelectionCount = new();

        /// <summary>
        /// 3차 이벤트에서 같은 히로인 재선택 시 +2 보정용 기록
        /// Key: eventTag (Event1, Event2, Event3), Value: 선택한 heroineId
        /// </summary>
        static readonly Dictionary<string, string> eventChoices = new();

        static HeroinePointTracker()
        {
            Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DomainReload() => Reset();

        /// <summary>초기화 (새 게임 시)</summary>
        public static void Reset()
        {
            points.Clear();
            eventSelectionCount.Clear();
            eventChoices.Clear();

            foreach (var id in GameConstants.HeroineIds)
            {
                points[id] = new Dictionary<PointCategory, int>
                {
                    { PointCategory.Event, 0 },
                    { PointCategory.Dialogue, 0 },
                    { PointCategory.Gift, 0 },
                    { PointCategory.MiniGame, 0 }
                };
                eventSelectionCount[id] = 0;
            }
        }

        /// <summary>
        /// 포인트 추가
        /// GameState.lovePoints도 자동 동기화하여 CSV 조건 평가가 정확히 작동
        /// </summary>
        public static void AddPoint(string heroineId, PointCategory category, int value)
        {
            if (!points.ContainsKey(heroineId)) return;
            points[heroineId][category] += value;
            Debug.Log($"[PointTracker] {heroineId} {category} +{value} (현재: {points[heroineId][category]})");

            // GameState.lovePoints 동기화 (CSV 조건 Love:Roa>=30 등이 정확히 작동)
            AffinityCalculator.SyncToGameState(heroineId);
        }

        /// <summary>
        /// 이벤트에서 히로인 선택 시 (eventTag: Event1, Event2, Event3, Festival, MT)
        /// </summary>
        public static void RecordEventChoice(string heroineId, string eventTag, int basePoints)
        {
            if (!points.ContainsKey(heroineId)) return;

            // 이벤트 포인트 부여
            AddPoint(heroineId, PointCategory.Event, basePoints);

            // 선택 횟수 증가
            eventSelectionCount[heroineId] = eventSelectionCount.GetValueOrDefault(heroineId) + 1;

            // 이벤트 선택 기록 (3차 복구 보정용)
            eventChoices[eventTag] = heroineId;

            // 3차 이벤트에서 같은 히로인 재선택 시 +2 보정
            // 기획서: "3차 데이트에서 다시 같은 히로인 선택 시 +2 보정"
            if (eventTag == "Event3")
            {
                bool selectedBefore = false;
                if (eventChoices.TryGetValue("Event1", out var e1) && e1 == heroineId) selectedBefore = true;
                if (eventChoices.TryGetValue("Event2", out var e2) && e2 == heroineId) selectedBefore = true;

                if (selectedBefore)
                {
                    // 로아는 제외 (올인 조건)
                    if (heroineId != "Roa")
                    {
                        AddPoint(heroineId, PointCategory.Event, 2);
                        Debug.Log($"[PointTracker] {heroineId} 3차 재선택 보정 +2");
                    }
                }
            }

            Debug.Log($"[PointTracker] 이벤트 선택: {heroineId} @ {eventTag} (+{basePoints}점)");
        }

        /// <summary>
        /// 카테고리별 포인트 조회
        /// </summary>
        public static int GetPoint(string heroineId, PointCategory category)
        {
            if (!points.ContainsKey(heroineId)) return 0;
            return points[heroineId].GetValueOrDefault(category);
        }

        /// <summary>
        /// 히로인의 총 포인트 (스탯보정 제외)
        /// </summary>
        public static int GetTotalPoint(string heroineId)
        {
            if (!points.ContainsKey(heroineId)) return 0;
            int total = 0;
            foreach (var kv in points[heroineId])
                total += kv.Value;
            return total;
        }

        /// <summary>
        /// 히로인 이벤트 선택 횟수 조회
        /// </summary>
        public static int GetEventSelectionCount(string heroineId)
        {
            return eventSelectionCount.GetValueOrDefault(heroineId);
        }

        /// <summary>
        /// 특정 이벤트에서 선택한 히로인 조회
        /// </summary>
        public static string GetEventChoice(string eventTag)
        {
            return eventChoices.GetValueOrDefault(eventTag);
        }

        #region Save / Load

        /// <summary>세이브용 데이터 추출</summary>
        public static PointTrackerSaveData GetSaveData()
        {
            var data = new PointTrackerSaveData();

            foreach (var kv in points)
            {
                data.Points[kv.Key] = new Dictionary<string, int>();
                foreach (var cat in kv.Value)
                    data.Points[kv.Key][cat.Key.ToString()] = cat.Value;
            }

            data.EventSelectionCount = new(eventSelectionCount);
            data.EventChoices = new(eventChoices);
            return data;
        }

        /// <summary>로드 시 복원</summary>
        public static void RestoreFromSave(PointTrackerSaveData data)
        {
            if (data == null) return;

            Reset();

            foreach (var kv in data.Points)
            {
                if (!points.ContainsKey(kv.Key)) continue;
                foreach (var cat in kv.Value)
                {
                    if (Enum.TryParse<PointCategory>(cat.Key, out var pc))
                        points[kv.Key][pc] = cat.Value;
                }
            }

            if (data.EventSelectionCount != null)
            {
                foreach (var kv in data.EventSelectionCount)
                    eventSelectionCount[kv.Key] = kv.Value;
            }

            if (data.EventChoices != null)
            {
                foreach (var kv in data.EventChoices)
                    eventChoices[kv.Key] = kv.Value;
            }

            // 로드 후 GameState.lovePoints 동기화
            AffinityCalculator.SyncAllToGameState();
        }

        #endregion
    }
}
