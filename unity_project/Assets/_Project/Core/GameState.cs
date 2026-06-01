using System;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 게임 상태 - 호감도, 스탯, 플래그, 돈 관리
    /// </summary>
    public class GameState : SingletonMonoBehaviour<GameState>
    {
        [Header("플레이어 정보")]
        [SerializeField] string playerName = "플레이어";

        [Header("스탯")]
        [SerializeField] int strength;      // 체력
        [SerializeField] int intelligence;  // 지성
        [SerializeField] int sociability;   // 사교성
        [SerializeField] int perseverance;  // 끈기
        [SerializeField] int fatigue;       // 피로

        [Header("소지금")]
        [SerializeField] int money;

        // 호감도 (캐릭터별)
        Dictionary<string, int> lovePoints = new();

        // 플래그
        Dictionary<string, bool> flags = new();

        // 선택지 이력 (JumpTarget 목록, 로그 복원용)
        List<string> choiceHistory = new();

        public string PlayerName => playerName;
        public int Money => money;

        /// <summary>스탯/머니 등 상태 변경 시 발행</summary>
        public event Action OnChanged;

        void NotifyChanged() => OnChanged?.Invoke();

        #region 스탯

        public int GetStat(string statName)
        {
            return statName.ToLower() switch
            {
                "str" or "strength" => strength,
                "int" or "intelligence" => intelligence,
                "soc" or "sociability" => sociability,
                "per" or "perseverance" => perseverance,
                "fatigue" => fatigue,
                _ => 0
            };
        }

        public void AddStat(string statName, int value)
        {
            SetStat(statName, GetStat(statName) + value);
            Debug.Log($"[GameState] Stat {statName} += {value}");
            NotifyChanged();
        }

        #endregion

        #region 호감도

        public int GetLove(string character)
        {
            return lovePoints.TryGetValue(character, out int value) ? value : 0;
        }

        public void AddLove(string character, int value)
        {
            if (!lovePoints.ContainsKey(character))
                lovePoints[character] = 0;

            lovePoints[character] += value;
            Debug.Log($"[GameState] Love {character} += {value} (현재: {lovePoints[character]})");
        }

        public void SetLove(string character, int value)
        {
            lovePoints[character] = value;
        }

        #endregion

        #region 플래그

        public bool GetFlag(string flagName)
        {
            return flags.TryGetValue(flagName, out bool value) && value;
        }

        public void SetFlag(string flagName, bool value)
        {
            flags[flagName] = value;
            Debug.Log($"[GameState] Flag {flagName} = {value}");
        }

        #endregion

        #region 돈

        public void AddMoney(int value)
        {
            money = Mathf.Max(0, money + value);
            Debug.Log($"[GameState] Money += {MoneyFormat.SignedCurrency(value)} (현재: {MoneyFormat.Currency(money)})");
            NotifyChanged();
        }

        #endregion

        #region 회차 카운터 (머신-와이드, PlayerPrefs)

        const string EndingCountKey = "GameState.EndingCount";

        /// <summary>
        /// 엔딩 진입 누적 횟수. 첫 엔딩 도달 시 1, 두 번째 2 ...
        /// PlayerPrefs 저장 → New Game·세이브 슬롯과 무관하게 머신 단위로 누적.
        /// </summary>
        public static int EndingCount => PlayerPrefs.GetInt(EndingCountKey, 0);

        /// <summary>EnterEnding 진입 시점에 1회 호출.</summary>
        public static void IncrementEndingCount()
        {
            int next = EndingCount + 1;
            PlayerPrefs.SetInt(EndingCountKey, next);
            PlayerPrefs.Save();
            Debug.Log($"[GameState] EndingCount → {next}");
        }

        /// <summary>디버그·치트용. 게임 진행 중에는 호출하지 말 것.</summary>
        public static void ResetEndingCount()
        {
            PlayerPrefs.DeleteKey(EndingCountKey);
            PlayerPrefs.Save();
        }

        #endregion

        #region 조건 체크

        /// <summary>
        /// 조건 문자열 평가
        /// 형식: Love:Roa>=30, Stat:Int>=20, Flag:Met_Roa, !Flag:Confessed, EndingCount>=2
        /// </summary>
        public bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return true;

            condition = NormalizeLegacyPrefix(condition);

            if (condition.StartsWith("!Flag:")) return !GetFlag(condition.Substring(6));
            if (condition.StartsWith("Flag:"))  return  GetFlag(condition.Substring(5));
            if (condition.StartsWith("Love:"))  return EvaluateComparison(condition.Substring(5), GetLove);
            if (condition.StartsWith("Stat:"))  return EvaluateComparison(condition.Substring(5), GetStat);

            // 머신-와이드 카운터: EndingCount>=N (PlayerPrefs)
            if (condition.StartsWith("EndingCount"))
                return EvaluateComparison(condition, _ => EndingCount);

            // 직접 스탯 비교: Int>=20, Fatigue>=50
            return EvaluateComparison(condition, GetStat);
        }

        /// <summary>
        /// 옛 표기를 새 표기로 변환. 현재 대상: Love_Xxx → Love:Xxx, Stat_Xxx → Stat:Xxx.
        /// 첫 언더스코어만 콜론으로 교체해 변수명 안의 언더스코어는 그대로 둔다.
        /// </summary>
        static string NormalizeLegacyPrefix(string condition)
        {
            if (!condition.StartsWith("Love_") && !condition.StartsWith("Stat_"))
                return condition;
            int idx = condition.IndexOf('_');
            return condition.Substring(0, idx) + ":" + condition.Substring(idx + 1);
        }

        bool EvaluateComparison(string expr, Func<string, int> getValue)
        {
            // 연산자 찾기
            string[] operators = { ">=", "<=", "==", ">", "<" };
            foreach (var op in operators)
            {
                int idx = expr.IndexOf(op);
                if (idx > 0)
                {
                    string name = expr.Substring(0, idx);
                    string valueStr = expr.Substring(idx + op.Length);

                    if (int.TryParse(valueStr, out int targetValue))
                    {
                        int currentValue = getValue(name);
                        return op switch
                        {
                            ">=" => currentValue >= targetValue,
                            "<=" => currentValue <= targetValue,
                            "==" => currentValue == targetValue,
                            ">" => currentValue > targetValue,
                            "<" => currentValue < targetValue,
                            _ => false
                        };
                    }
                }
            }

            return false;
        }

        #endregion

        #region 초기화

        public void ResetAll()
        {
            strength = 0;
            intelligence = 0;
            sociability = 0;
            perseverance = 0;
            fatigue = 0;
            money = 0;
            lovePoints.Clear();
            flags.Clear();
            choiceHistory.Clear();
            Shop.ShopSystem.Reset();
            NotifyChanged();
        }

        #endregion

        #region Save/Load 지원

        public void SetPlayerName(string name) => playerName = name;
        public void SetMoney(int value)
        {
            money = value;
            NotifyChanged();
        }

        public void SetStat(string statName, int value)
        {
            value = Mathf.Clamp(value, 0, GameConstants.MaxStat);
            switch (statName.ToLower())
            {
                case "str": case "strength": strength = value; break;
                case "int": case "intelligence": intelligence = value; break;
                case "soc": case "sociability": sociability = value; break;
                case "per": case "perseverance": perseverance = value; break;
                case "fatigue": fatigue = value; break;
            }
            NotifyChanged();
        }

        public Dictionary<string, int> GetAllLovePoints() => new(lovePoints);
        public void SetAllLovePoints(Dictionary<string, int> data)
        {
            lovePoints.Clear();
            if (data != null)
                foreach (var kv in data)
                    lovePoints[kv.Key] = kv.Value;
        }

        public Dictionary<string, bool> GetAllFlags() => new(flags);
        public void SetAllFlags(Dictionary<string, bool> data)
        {
            flags.Clear();
            if (data != null)
                foreach (var kv in data)
                    flags[kv.Key] = kv.Value;
        }

        public void AddChoice(string jumpTarget) => choiceHistory.Add(jumpTarget);
        public List<string> GetChoiceHistory() => new(choiceHistory);
        public void SetChoiceHistory(List<string> data)
        {
            choiceHistory.Clear();
            if (data != null)
                choiceHistory.AddRange(data);
        }

        #endregion
    }
}
