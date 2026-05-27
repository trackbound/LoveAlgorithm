using System;
using System.Collections.Generic;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 포인트 트래커 세이브 데이터.
    /// C4-Phase A Group H에서 LoveAlgo.Modules.Affinity → LoveAlgo.Contracts 로 이동.
    /// </summary>
    [Serializable]
    public class PointTrackerSaveData
    {
        /// <summary>히로인별 카테고리별 포인트</summary>
        public Dictionary<string, Dictionary<string, int>> Points = new();

        /// <summary>히로인별 이벤트 선택 횟수</summary>
        public Dictionary<string, int> EventSelectionCount = new();

        /// <summary>이벤트별 선택 히로인</summary>
        public Dictionary<string, string> EventChoices = new();
    }
}
