using System.Collections.Generic;
using LoveAlgo.Core; // GameStateSO

namespace LoveAlgo.Gacha
{
    /// <summary>
    /// 랜덤가챠 결정 로직(순수 static, EventBus·RNG 무관 — EditMode 테스트).
    /// 일반 VN 수집형 패턴(감독 결정 2026-06-12): 추첨은 **미보유 조각 풀에서만**(가중치는 풀 내 상대 확률,
    /// 조각 수만큼 구매하면 반드시 완성되는 자연 천장) · 완성 후 추가 구매는 보상 없는 연출 + 업적 카운트
    /// (퍼즐 콜렉터 +5 / 퍼즐 마스터 +10, 기획서 p44) · 호칭은 기존 플래그로 영속(표시 UI는 노출처 생길 때).
    /// </summary>
    public static class GachaPuzzleService
    {
        /// <summary>완성 후 추가 구매 업적 임계(기획서: +5 콜렉터 / +10 마스터).</summary>
        public const int CollectorBonusCount = 5;
        public const int MasterBonusCount = 10;
        public const string CollectorFlag = "Title_PuzzleCollector";
        public const string MasterFlag = "Title_PuzzleMaster";

        /// <summary>
        /// 추첨(순수) — 미보유 조각 중 가중치 비례로 1개 선택. 전부 보유면 -1(완성 후 구매 = 연출만).
        /// <paramref name="roll01"/>은 [0,1) 난수(호출자 주입 — ScheduleController 투자 RNG 선례).
        /// </summary>
        public static int Draw(GameStateSO gs, GachaTuningSO tuning, float roll01)
        {
            if (gs == null || tuning == null) return -1;

            int count = tuning.PieceCount;
            int totalWeight = 0;
            var pool = new List<(int piece, int weight)>(count);
            for (int i = 0; i < count; i++)
            {
                if (IsOwned(gs, i)) continue;
                int w = tuning.WeightOf(tuning.RarityOf(i));
                pool.Add((i, w));
                totalWeight += w;
            }
            if (pool.Count == 0) return -1; // 완성 상태

            if (roll01 < 0f) roll01 = 0f;
            if (roll01 >= 1f) roll01 = 0.999999f;
            float target = roll01 * totalWeight;

            float acc = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                acc += pool[i].weight;
                if (target < acc) return pool[i].piece;
            }
            return pool[pool.Count - 1].piece; // 부동소수 끝단 방어
        }

        /// <summary>조각 보유 기록(중복 무시). 유효 범위 밖은 무시.</summary>
        public static bool Own(GameStateSO gs, GachaTuningSO tuning, int pieceIndex)
        {
            if (gs == null || tuning == null) return false;
            if (pieceIndex < 0 || pieceIndex >= tuning.PieceCount) return false;
            if (IsOwned(gs, pieceIndex)) return false;
            gs.Data.gachaOwnedPieces.Add(pieceIndex);
            return true;
        }

        public static bool IsOwned(GameStateSO gs, int pieceIndex)
            => gs != null && gs.Data.gachaOwnedPieces.Contains(pieceIndex);

        public static int OwnedCount(GameStateSO gs)
            => gs != null ? gs.Data.gachaOwnedPieces.Count : 0;

        /// <summary>퍼즐 완성 여부(보유 수 ≥ 조각 수).</summary>
        public static bool IsComplete(GameStateSO gs, GachaTuningSO tuning)
            => gs != null && tuning != null && OwnedCount(gs) >= tuning.PieceCount;

        /// <summary>
        /// 완성 후 추가 구매 1회 기록 + 도달한 업적 플래그 세팅(재호출 안전 — 플래그 덮어쓰기 무해).
        /// 반환 = 누적 추가 구매 수.
        /// </summary>
        public static int RecordBonusPurchase(GameStateSO gs)
        {
            if (gs == null) return 0;
            gs.Data.gachaBonusPurchases++;
            if (gs.Data.gachaBonusPurchases >= CollectorBonusCount) gs.SetFlag(CollectorFlag, true);
            if (gs.Data.gachaBonusPurchases >= MasterBonusCount) gs.SetFlag(MasterFlag, true);
            return gs.Data.gachaBonusPurchases;
        }
    }
}
