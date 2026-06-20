using UnityEngine;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 메타 진행도 영속(PlayerPrefs — 앱 영속, 세이브게임 JsonSaveStore와 분리). <see cref="LoveAlgo.Settings"/>의
    /// SettingsStore 형제 패턴: 플레이스루를 가로지르는 누적값(회차 카운터 등)을 담는다. 새 게임(GameBoot.NewGame)은
    /// GameStateSO만 리셋하므로 여기 값은 살아남는다 — "처음부터 다시 플레이"해도 누적되는 진행도 전용.
    ///
    /// 순수 정적 I/O. Story 분기(ConditionEvaluator <c>Meta:</c>)가 읽고, 엔진 Flow(<c>MetaInc:</c>)가 증가시킨다.
    /// 테스트는 <see cref="DeleteKey"/>로 키를 청소한다(전역 PlayerPrefs 오염 방지).
    /// </summary>
    public static class MetaProgressStore
    {
        const string P = "lovealgo.meta.";

        /// <summary>프롤로그 완주(엔딩 도달) 누적 횟수 키. 0=첫 회차 → 회차별 엔딩 분기 게이트가 읽는다.</summary>
        public const string PrologueClears = "prologueClears";

        /// <summary>key의 정수값(미설정 시 <paramref name="defaultValue"/>). 접두사는 내부에서 붙인다.</summary>
        public static int GetInt(string key, int defaultValue = 0)
            => PlayerPrefs.GetInt(P + (key ?? ""), defaultValue);

        /// <summary>key에 정수값을 저장하고 즉시 영속화한다.</summary>
        public static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(P + (key ?? ""), value);
            PlayerPrefs.Save();
        }

        /// <summary>key 값을 <paramref name="delta"/>(기본 1)만큼 증가시키고 새 값을 반환한다(즉시 영속).</summary>
        public static int Increment(string key, int delta = 1)
        {
            int v = GetInt(key) + delta;
            SetInt(key, v);
            return v;
        }

        /// <summary>key 삭제(테스트 청소/공장 초기화). 접두사 포함.</summary>
        public static void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(P + (key ?? ""));
            PlayerPrefs.Save();
        }
    }
}
