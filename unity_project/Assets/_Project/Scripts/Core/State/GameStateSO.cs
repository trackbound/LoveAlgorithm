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
    }
}
