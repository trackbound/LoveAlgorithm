using UnityEngine;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 단일 런타임 상태 컨테이너 (ADR-007). 참조만 있으면 동기적으로 즉시 읽을 수 있어
    /// Service Locator 없이 모듈들이 상태를 공유한다. 변경 통지는 EventBus.
    /// 가변 상태는 <see cref="Data"/>(일반 클래스)에만 보유하고 SO 에셋 필드엔 영구화하지 않는다
    /// (<c>[NonSerialized]</c> + 부팅 시 <see cref="ResetRuntime"/>). (ADR-012, dev_guide §4-1a)
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/Game State", fileName = "GameStateSO")]
    public class GameStateSO : ScriptableObject
    {
        // 에셋에 직렬화되지 않음 → 에디터에 런타임 값이 박히는 SO 최대 함정 방지.
        [System.NonSerialized] GameStateData _runtime = new();

        /// <summary>현재 런타임 상태(읽기/쓰기). 세이브 시 통째로 직렬화된다.</summary>
        public GameStateData Data => _runtime;

        /// <summary>새 게임 시작 — 상태 초기화.</summary>
        public void ResetRuntime() => _runtime = new GameStateData();

        /// <summary>세이브에서 복원.</summary>
        public void Load(GameStateData data) => _runtime = data ?? new GameStateData();

        // ── 호감도 동기 접근 (엔트리 리스트 ↔ dict 의미) ──
        public int GetLove(string heroineId)
        {
            var list = _runtime.lovePoints;
            for (int i = 0; i < list.Count; i++)
                if (list[i].key == heroineId) return list[i].value;
            return 0;
        }

        public void SetLove(string heroineId, int value)
        {
            var list = _runtime.lovePoints;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].key == heroineId)
                {
                    list[i] = new GameStateData.IntEntry { key = heroineId, value = value };
                    return;
                }
            }
            list.Add(new GameStateData.IntEntry { key = heroineId, value = value });
        }

        public void AddLove(string heroineId, int delta) => SetLove(heroineId, GetLove(heroineId) + delta);

        // ── 스탯 동기 접근 (REWRITE_FEATURE_INVENTORY §5: Str/Int/Soc/Per/Fatigue, 0~MaxStat 클램프) ──
        // 문자열 id로 접근하는 이유: 호감도 공식·CSV 조건(Stat:Id>=N)이 id 기반이라 필드 직접 노출보다
        // 일관적. "Int"는 예약어 회피로 내부 필드명이 intel이므로 여기서 매핑한다.
        public const int MinStat = 0;
        public const int MaxStat = 100;

        /// <summary>플레이어 스탯 id 정본 집합. 스냅샷/순회(통합층의 StatChangedEvent 산출 등)의 단일 진실 소스.</summary>
        public static readonly string[] StatIds = { "Str", "Int", "Soc", "Per", "Fatigue" };

        public int GetStat(string statId)
        {
            switch (statId)
            {
                case "Str": return _runtime.str;
                case "Int": return _runtime.intel;
                case "Soc": return _runtime.soc;
                case "Per": return _runtime.per;
                case "Fatigue": return _runtime.fatigue;
                default: return 0;
            }
        }

        public void SetStat(string statId, int value)
        {
            int v = Mathf.Clamp(value, MinStat, MaxStat);
            switch (statId)
            {
                case "Str": _runtime.str = v; break;
                case "Int": _runtime.intel = v; break;
                case "Soc": _runtime.soc = v; break;
                case "Per": _runtime.per = v; break;
                case "Fatigue": _runtime.fatigue = v; break;
            }
        }

        public void AddStat(string statId, int delta) => SetStat(statId, GetStat(statId) + delta);

        // ── 데이루프 카운터 / 소지금 동기 접근 ──
        // 진행 로직(공식)은 Data 레이어 DayLoop가 담당하고, 여기선 상태 read/write만 노출한다.
        public int Day { get => _runtime.day; set => _runtime.day = value; }

        // 현재 화면 페이즈(ADR-013). PhaseController만 set한다(유일 권위자). 런타임 전용(부팅 리셋).
        public ScreenPhase Phase { get => _runtime.phase; set => _runtime.phase = value; }
        public int RemainingActions { get => _runtime.remainingActions; set => _runtime.remainingActions = value; }

        // 소지금은 0 미만이 될 수 없다(구 GameState.AddMoney = Mathf.Max(0, …) 재현). 세터에서 바닥 클램프.
        public long Money { get => _runtime.money; set => _runtime.money = value < 0 ? 0 : value; }
        public void AddMoney(long delta) => Money = _runtime.money + delta;

        // ── 1일 1회 제한 스케줄 추적 (도메인 규칙, 하루 전환 시 ClearDailyLimits로 리셋) ──
        public bool HasUsedLimited(string scheduleId) => _runtime.usedLimitedToday.Contains(scheduleId);

        public void MarkLimitedUsed(string scheduleId)
        {
            if (!_runtime.usedLimitedToday.Contains(scheduleId))
                _runtime.usedLimitedToday.Add(scheduleId);
        }

        /// <summary>하루 전환 시 호출 — 오늘 사용한 제한 스케줄 기록을 비운다(DayLoop.BeginRun/AdvanceDay).</summary>
        public void ClearDailyLimits() => _runtime.usedLimitedToday.Clear();

        // ── 플래그 동기 접근 ──
        public bool GetFlag(string name)
        {
            var list = _runtime.flags;
            for (int i = 0; i < list.Count; i++)
                if (list[i].key == name) return list[i].value;
            return false;
        }

        public void SetFlag(string name, bool value)
        {
            var list = _runtime.flags;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].key == name)
                {
                    list[i] = new GameStateData.BoolEntry { key = name, value = value };
                    return;
                }
            }
            list.Add(new GameStateData.BoolEntry { key = name, value = value });
        }

        // ── 중복 구매 페널티 추적 (Shop §5: 같은 날 같은 DuplicateTag 2회차부터 효과 절반) ──
        // 날짜가 바뀌면 카운트를 비우고(하루 단위 추적), 호출마다 해당 태그 사용 횟수를 1 증가시킨 뒤
        // "이번이 2회차 이상인가"(= 페널티 대상)를 반환한다. 실제 절반 계산은 소비처(ShopService.Penalized).
        // dailyDuplicateUsage/lastDuplicateDay는 세이브 직렬화 — 하루 중 재로드 시에도 페널티가 정확히 유지된다.
        public bool RegisterDuplicateUse(string tag, int currentDay)
        {
            if (currentDay != _runtime.lastDuplicateDay)
            {
                _runtime.dailyDuplicateUsage.Clear();
                _runtime.lastDuplicateDay = currentDay;
            }
            int usage = GetDuplicateUsage(tag);
            SetDuplicateUsage(tag, usage + 1);
            return usage > 0; // 2회차 이상이면 페널티 대상(같은 날 같은 태그)
        }

        int GetDuplicateUsage(string tag)
        {
            var list = _runtime.dailyDuplicateUsage;
            for (int i = 0; i < list.Count; i++)
                if (list[i].key == tag) return list[i].value;
            return 0;
        }

        void SetDuplicateUsage(string tag, int value)
        {
            var list = _runtime.dailyDuplicateUsage;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].key == tag)
                {
                    list[i] = new GameStateData.IntEntry { key = tag, value = value };
                    return;
                }
            }
            list.Add(new GameStateData.IntEntry { key = tag, value = value });
        }
    }
}
