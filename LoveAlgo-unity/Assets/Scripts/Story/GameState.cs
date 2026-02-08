using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 게임 상태 - 호감도, 스탯, 플래그, 돈 관리
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

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

        public string PlayerName => playerName;
        public int Money => money;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(gameObject);  // 데모: 단일 씬
            }
            else
            {
                Destroy(gameObject);
            }
        }

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
            switch (statName.ToLower())
            {
                case "str":
                case "strength":
                    strength += value;
                    break;
                case "int":
                case "intelligence":
                    intelligence += value;
                    break;
                case "soc":
                case "sociability":
                    sociability += value;
                    break;
                case "per":
                case "perseverance":
                    perseverance += value;
                    break;
                case "fatigue":
                    fatigue += value;
                    break;
            }
            Debug.Log($"[GameState] Stat {statName} += {value}");
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
            money += value;
            Debug.Log($"[GameState] Money += {value} (현재: {money})");
        }

        #endregion

        #region 조건 체크

        /// <summary>
        /// 조건 문자열 평가
        /// 형식: Love:Roa>=30, Stat:Int>=20, Flag:Met_Roa, !Flag:Confessed
        /// </summary>
        public bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return true;

            // 언더스코어 형식 정규화: Love_Roa>5 → Love:Roa>5
            if (condition.StartsWith("Love_") || condition.StartsWith("Stat_"))
            {
                int idx = condition.IndexOf('_');
                condition = condition.Substring(0, idx) + ":" + condition.Substring(idx + 1);
            }

            // 부정 플래그: !Flag:Name
            if (condition.StartsWith("!Flag:"))
            {
                string flagName = condition.Substring(6);
                return !GetFlag(flagName);
            }

            // 플래그: Flag:Name
            if (condition.StartsWith("Flag:"))
            {
                string flagName = condition.Substring(5);
                return GetFlag(flagName);
            }

            // 호감도: Love:Character>=Value
            if (condition.StartsWith("Love:"))
            {
                return EvaluateComparison(condition.Substring(5), GetLove);
            }

            // 스탯: Stat:StatName>=Value 또는 StatName>=Value
            if (condition.StartsWith("Stat:"))
            {
                return EvaluateComparison(condition.Substring(5), GetStat);
            }

            // 직접 스탯 비교: Int>=20, Fatigue>=50
            return EvaluateComparison(condition, GetStat);
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
        }

        #endregion

        #region Save/Load 지원

        public void SetPlayerName(string name) => playerName = name;
        public void SetMoney(int value) => money = value;

        public void SetStat(string statName, int value)
        {
            switch (statName.ToLower())
            {
                case "str": case "strength": strength = value; break;
                case "int": case "intelligence": intelligence = value; break;
                case "soc": case "sociability": sociability = value; break;
                case "per": case "perseverance": perseverance = value; break;
                case "fatigue": fatigue = value; break;
            }
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

        #endregion
    }
}
