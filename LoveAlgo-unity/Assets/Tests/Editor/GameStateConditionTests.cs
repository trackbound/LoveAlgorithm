using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Story;

namespace LoveAlgo.Tests
{
    /// <summary>
    /// GameState 조건 평가 테스트
    /// 참고: GameState는 MonoBehaviour이므로 직접 인스턴스화가 어려움
    /// 여기서는 EvaluateCondition 로직을 테스트용 래퍼로 검증
    /// </summary>
    public class GameStateConditionTests
    {
        // 테스트용 상태 시뮬레이터
        private class TestGameState
        {
            public int Strength { get; set; }
            public int Intelligence { get; set; }
            public int Sociability { get; set; }
            public int Perseverance { get; set; }
            public int Fatigue { get; set; }
            public System.Collections.Generic.Dictionary<string, int> LovePoints { get; } = new();
            public System.Collections.Generic.Dictionary<string, bool> Flags { get; } = new();

            public int GetStat(string statName)
            {
                return statName.ToLower() switch
                {
                    "str" or "strength" => Strength,
                    "int" or "intelligence" => Intelligence,
                    "soc" or "sociability" => Sociability,
                    "per" or "perseverance" => Perseverance,
                    "fatigue" => Fatigue,
                    _ => 0
                };
            }

            public int GetLove(string character)
            {
                return LovePoints.TryGetValue(character, out int value) ? value : 0;
            }

            public bool GetFlag(string flagName)
            {
                return Flags.TryGetValue(flagName, out bool value) && value;
            }

            /// <summary>
            /// GameState.EvaluateCondition과 동일한 로직
            /// </summary>
            public bool EvaluateCondition(string condition)
            {
                if (string.IsNullOrEmpty(condition)) return true;

                if (condition.StartsWith("!Flag:"))
                {
                    string flagName = condition.Substring(6);
                    return !GetFlag(flagName);
                }

                if (condition.StartsWith("Flag:"))
                {
                    string flagName = condition.Substring(5);
                    return GetFlag(flagName);
                }

                if (condition.StartsWith("Love:"))
                {
                    return EvaluateComparison(condition.Substring(5), GetLove);
                }

                if (condition.StartsWith("Stat:"))
                {
                    return EvaluateComparison(condition.Substring(5), GetStat);
                }

                return EvaluateComparison(condition, GetStat);
            }

            private bool EvaluateComparison(string expr, System.Func<string, int> getValue)
            {
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
        }

        private TestGameState state;

        [SetUp]
        public void Setup()
        {
            state = new TestGameState
            {
                Intelligence = 25,
                Strength = 15,
                Fatigue = 30
            };
            state.LovePoints["Roa"] = 35;
            state.LovePoints["Daeun"] = 10;
            state.Flags["Met_Roa"] = true;
            state.Flags["Confessed"] = false;
        }

        #region 플래그 테스트

        [Test]
        public void EvaluateCondition_FlagTrue_ReturnsTrue()
        {
            Assert.IsTrue(state.EvaluateCondition("Flag:Met_Roa"));
        }

        [Test]
        public void EvaluateCondition_FlagFalse_ReturnsFalse()
        {
            Assert.IsFalse(state.EvaluateCondition("Flag:Confessed"));
        }

        [Test]
        public void EvaluateCondition_NotFlag_ReturnsOpposite()
        {
            Assert.IsFalse(state.EvaluateCondition("!Flag:Met_Roa"));
            Assert.IsTrue(state.EvaluateCondition("!Flag:Confessed"));
        }

        [Test]
        public void EvaluateCondition_UndefinedFlag_ReturnsFalse()
        {
            Assert.IsFalse(state.EvaluateCondition("Flag:NeverDefined"));
        }

        #endregion

        #region 호감도 테스트

        [Test]
        public void EvaluateCondition_LoveGreaterOrEqual_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Love:Roa>=30"));
            Assert.IsTrue(state.EvaluateCondition("Love:Roa>=35"));
        }

        [Test]
        public void EvaluateCondition_LoveGreaterOrEqual_Fail()
        {
            Assert.IsFalse(state.EvaluateCondition("Love:Roa>=40"));
        }

        [Test]
        public void EvaluateCondition_LoveLessThan_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Love:Daeun<20"));
        }

        [Test]
        public void EvaluateCondition_LoveEquals_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Love:Roa==35"));
        }

        [Test]
        public void EvaluateCondition_UnknownCharacter_ReturnsZero()
        {
            Assert.IsTrue(state.EvaluateCondition("Love:Unknown>=0"));
            Assert.IsFalse(state.EvaluateCondition("Love:Unknown>0"));
        }

        #endregion

        #region 스탯 테스트

        [Test]
        public void EvaluateCondition_StatWithPrefix_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Stat:Int>=20"));
            Assert.IsTrue(state.EvaluateCondition("Stat:Int>=25"));
        }

        [Test]
        public void EvaluateCondition_StatDirect_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Int>=20"));
            Assert.IsTrue(state.EvaluateCondition("Fatigue>=30"));
        }

        [Test]
        public void EvaluateCondition_FatigueLessThan_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Fatigue<50"));
        }

        [Test]
        public void EvaluateCondition_StatLessOrEqual_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Str<=15"));
            Assert.IsTrue(state.EvaluateCondition("Str<=20"));
        }

        #endregion

        #region 엣지 케이스

        [Test]
        public void EvaluateCondition_EmptyString_ReturnsTrue()
        {
            Assert.IsTrue(state.EvaluateCondition(""));
            Assert.IsTrue(state.EvaluateCondition(null));
        }

        [Test]
        public void EvaluateCondition_InvalidFormat_ReturnsFalse()
        {
            Assert.IsFalse(state.EvaluateCondition("InvalidCondition"));
            Assert.IsFalse(state.EvaluateCondition("Love:Roa"));
        }

        #endregion
    }
}
