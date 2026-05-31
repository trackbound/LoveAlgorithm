using LoveAlgo.Contracts;
using LoveAlgo.Common;
using LoveAlgo.Story;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Modules.Stats
{
    /// <summary>
    /// 스탯 모듈 진입점.
    /// GameState를 래핑해 IStats 인터페이스를 제공하고, 변경 시 EventBus에 StatChangedEvent 발행.
    /// 씬 하이어라키: _Modules/StatsModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class StatsModule : MonoBehaviour, IStats
    {
        void Awake()
        {
            Services.Register<IStats>(this);
        }

        void OnDestroy()
        {
            if (Services.TryGet<IStats>() == (IStats)this)
                Services.Unregister<IStats>();
        }

        public int Get(string statId)
        {
            var gs = GameState.Instance;
            return gs != null ? gs.GetStat(statId) : 0;
        }

        public void Add(string statId, int delta)
        {
            var gs = GameState.Instance;
            if (gs == null || delta == 0) return;
            int oldValue = gs.GetStat(statId);
            gs.AddStat(statId, delta);
            int newValue = gs.GetStat(statId);
            if (oldValue != newValue)
                EventBus.Publish(new StatChangedEvent(statId, oldValue, newValue));
        }

        public void Set(string statId, int value)
        {
            var gs = GameState.Instance;
            if (gs == null) return;
            int oldValue = gs.GetStat(statId);
            if (oldValue == value) return;
            gs.SetStat(statId, value);
            int newValue = gs.GetStat(statId); // GameState가 클램프할 수 있음
            EventBus.Publish(new StatChangedEvent(statId, oldValue, newValue));
        }
    }
}
