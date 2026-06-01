using System;
using System.Collections.Generic;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 런타임에 변하는 게임 상태(가변 Instance). 세이브 직렬화 대상.
    /// SO 에셋에 영구 저장 금지 — <see cref="GameStateSO"/>가 런타임으로만 보유(부팅 리셋). (ADR-012, dev_guide §4-1a)
    /// JsonUtility 호환을 위해 Dictionary 대신 직렬화 가능한 엔트리 리스트를 사용한다.
    /// </summary>
    [Serializable]
    public class GameStateData
    {
        public string playerName = "";
        public long money;

        // 플레이어 스탯 (REWRITE_FEATURE_INVENTORY §5: Str/Int/Soc/Per/Fatigue, 0~100)
        public int str;
        public int intel;
        public int soc;
        public int per;
        public int fatigue;

        public int day = 1;

        // 호감도(히로인id→점수) / 플래그(이름→bool). dict 대용 엔트리 리스트.
        public List<IntEntry> lovePoints = new();
        public List<BoolEntry> flags = new();

        [Serializable] public struct IntEntry { public string key; public int value; }
        [Serializable] public struct BoolEntry { public string key; public bool value; }
    }
}
