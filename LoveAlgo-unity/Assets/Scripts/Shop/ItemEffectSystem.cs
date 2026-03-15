using System;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 아이템 효과 시스템 (정적 유틸리티)
    /// 
    /// 기획서 기반 핵심 메커니즘:
    ///   1. 선물 계층 → 포인트 계산 (2차/3차 이벤트 시점별)
    ///   2. 동일날 중복 사용 패널티 (같은 태그 2회차부터 50%)
    ///   3. 세션 버프 (자유행동 1회 동안 스탯 보정)
    ///   4. 아이템 해금 시점 판정
    /// </summary>
    public static class ItemEffectSystem
    {
        // ── 동일날 중복 추적 ──
        static int lastTrackedDay = -1;
        static readonly Dictionary<string, int> dayUsageCount = new();

        // ── 세션 버프 (자유행동 1회 동안 유효) ──
        static string activeBuffStat;
        static int activeBuffValue;
        static bool hasActiveBuff;

        static ItemEffectSystem()
        {
            Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DomainReload() => Reset();

        /// <summary>초기화</summary>
        public static void Reset()
        {
            lastTrackedDay = -1;
            dayUsageCount.Clear();
            activeBuffStat = null;
            activeBuffValue = 0;
            hasActiveBuff = false;
        }

        #region 선물 포인트 계산

        /// <summary>
        /// 선물 계층 기반 포인트 계산 (기획서 4.2)
        /// 
        /// | 계층   | 2차(Event2 이전) | 3차(Event3 이후) |
        /// |--------|-----------------|-----------------|
        /// | 저가   | +1              | +2              |
        /// | 중급   | +2              | +3              |
        /// | 고급   | +3              | +4              |
        /// | 최고급 | +3              | +5              |
        /// </summary>
        /// <param name="tier">아이템 선물 계층</param>
        /// <param name="isEvent3OrLater">3차 이벤트(StoryArc.FreeTime5) 이후 여부</param>
        public static int GetGiftPoints(GiftTier tier, bool isEvent3OrLater)
        {
            return tier switch
            {
                GiftTier.Low     => isEvent3OrLater ? 2 : 1,
                GiftTier.Mid     => isEvent3OrLater ? 3 : 2,
                GiftTier.High    => isEvent3OrLater ? 4 : 3,
                GiftTier.Premium => isEvent3OrLater ? 5 : 3,
                _ => 0
            };
        }

        #endregion

        #region 동일날 중복 패널티

        /// <summary>
        /// 동일날 중복 사용 시 효과 계산 (기획서: 2회차부터 50%)
        /// 사용 횟수를 자동 증가시킨 뒤 보정된 효과값 반환
        /// </summary>
        /// <param name="duplicateTag">아이템의 중복 추적 태그</param>
        /// <param name="baseEffect">기본 효과값</param>
        /// <param name="currentDay">현재 날짜 (GameManager.CurrentDay)</param>
        /// <returns>최종 효과값 (최소 1)</returns>
        public static int ApplyDuplicatePenalty(string duplicateTag, int baseEffect, int currentDay)
        {
            RefreshDay(currentDay);

            int usageCount = dayUsageCount.GetValueOrDefault(duplicateTag);
            dayUsageCount[duplicateTag] = usageCount + 1;

            // 2회차부터 50% 감소
            if (usageCount > 0)
                return Mathf.Max(1, baseEffect / 2);

            return baseEffect;
        }

        /// <summary>날짜가 바뀌면 사용 횟수 리셋</summary>
        static void RefreshDay(int currentDay)
        {
            if (lastTrackedDay != currentDay)
            {
                dayUsageCount.Clear();
                lastTrackedDay = currentDay;
            }
        }

        #endregion

        #region 세션 버프

        /// <summary>
        /// 세션 버프 아이템 사용 (자유행동 1회 동안 스탯 보정)
        /// 기존 버프가 있으면 덮어씀
        /// </summary>
        /// <param name="item">SessionBuff 카테고리 아이템</param>
        /// <param name="currentDay">현재 날짜 (중복 패널티 적용용)</param>
        /// <returns>실제 적용될 버프값 (중복 패널티 적용 후)</returns>
        public static int ActivateSessionBuff(ItemData item, int currentDay)
        {
            if (item.Category != ItemCategory.SessionBuff || string.IsNullOrEmpty(item.EffectStat))
            {
                Debug.LogWarning($"[ItemEffectSystem] 세션 버프 아이템이 아님: {item.Id}");
                return 0;
            }

            int finalValue = ApplyDuplicatePenalty(item.GetDuplicateTag(), item.EffectValue, currentDay);

            activeBuffStat = item.EffectStat;
            activeBuffValue = finalValue;
            hasActiveBuff = true;

            Debug.Log($"[ItemEffectSystem] 세션 버프 활성화: {item.EffectStat} +{finalValue} (원본: +{item.EffectValue})");
            return finalValue;
        }

        /// <summary>세션 버프가 활성화되어 있는지</summary>
        public static bool HasActiveBuff => hasActiveBuff;

        /// <summary>활성 버프 정보 조회 (소비하지 않음)</summary>
        public static (string stat, int bonus) PeekSessionBuff()
        {
            if (!hasActiveBuff) return (null, 0);
            return (activeBuffStat, activeBuffValue);
        }

        /// <summary>
        /// 세션 버프 소비 (자유행동 1회 완료 시 호출)
        /// 스케줄 실행 후 GameManager.OnScheduleSelected에서 호출
        /// </summary>
        /// <returns>(대상 스탯 ID, 보너스 값) — 버프 없으면 (null, 0)</returns>
        public static (string stat, int bonus) ConsumeSessionBuff()
        {
            if (!hasActiveBuff) return (null, 0);

            var result = (activeBuffStat, activeBuffValue);
            Debug.Log($"[ItemEffectSystem] 세션 버프 소비: {activeBuffStat} +{activeBuffValue}");

            activeBuffStat = null;
            activeBuffValue = 0;
            hasActiveBuff = false;

            return result;
        }

        #endregion

        #region 해금 시점 판정

        /// <summary>
        /// 현재 스토리 진행도에 따른 최대 아이템 해금 단계
        /// GameTimeline.StoryArc 기반
        /// </summary>
        public static ItemAvailability GetCurrentAvailability(StoryArc arc)
        {
            if (arc >= StoryArc.Confession)
                return ItemAvailability.AfterConfession;
            if (arc >= StoryArc.FreeTime5)
                return ItemAvailability.AfterEvent3Start;
            if (arc >= StoryArc.FreeTime3)
                return ItemAvailability.AfterEvent2Start;
            return ItemAvailability.Always;
        }

        /// <summary>현재 해금 단계에서 아이템을 구매할 수 있는지</summary>
        public static bool IsItemAvailable(ItemData item, StoryArc arc)
        {
            return item.Availability <= GetCurrentAvailability(arc);
        }

        #endregion

        #region Save / Load

        /// <summary>세이브용 데이터 추출</summary>
        public static ItemEffectSaveData GetSaveData()
        {
            return new ItemEffectSaveData
            {
                ActiveBuffStat = activeBuffStat,
                ActiveBuffValue = activeBuffValue,
                HasActiveBuff = hasActiveBuff,
                LastTrackedDay = lastTrackedDay,
                DayUsageCount = new Dictionary<string, int>(dayUsageCount)
            };
        }

        /// <summary>로드 시 복원</summary>
        public static void RestoreFromSave(ItemEffectSaveData data)
        {
            Reset();
            if (data == null) return;

            activeBuffStat = data.ActiveBuffStat;
            activeBuffValue = data.ActiveBuffValue;
            hasActiveBuff = data.HasActiveBuff;
            lastTrackedDay = data.LastTrackedDay;

            if (data.DayUsageCount != null)
            {
                foreach (var kv in data.DayUsageCount)
                    dayUsageCount[kv.Key] = kv.Value;
            }
        }

        #endregion
    }

    /// <summary>ItemEffectSystem 세이브 데이터</summary>
    [Serializable]
    public class ItemEffectSaveData
    {
        public string ActiveBuffStat;
        public int ActiveBuffValue;
        public bool HasActiveBuff;
        public int LastTrackedDay;
        public Dictionary<string, int> DayUsageCount = new();
    }
}
