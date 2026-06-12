using UnityEngine;

namespace LoveAlgo.Gacha
{
    /// <summary>
    /// 랜덤가챠 정의(Definition, ADR-012) — 퍼즐 구성(기획: 가로 6×세로 5 = 30조각)·레어도 가중치·연출 수치.
    /// 레어도는 외부 비표기, 추첨 확률 구분용(기획서 결론). 조각별 레어도 배치(어느 부위가 희귀한가)는
    /// 기획 입력 영역 — 기본값은 분량표(1:10 / 2:7 / 3:6 / 4:5 / 5:2장) 순서 채움.
    /// 에셋: Resources/Data/GachaTuning.asset.
    /// </summary>
    [CreateAssetMenu(fileName = "GachaTuning", menuName = "LoveAlgo/Gacha Tuning")]
    public class GachaTuningSO : ScriptableObject
    {
        [Header("퍼즐 구성 (기획: 6×5 = 30조각)")]
        [Min(1)] public int columns = 6;
        [Min(1)] public int rows = 5;

        public int PieceCount => columns * rows;

        [Header("레어도 가중치 (인덱스 0 = 레어도1 … 4 = 레어도5, 숫자 클수록 희귀)")]
        [Tooltip("미보유 풀 안에서의 상대 가중치 — 시작값 40/25/18/12/5는 감독 튜닝 영역.")]
        public int[] rarityWeights = { 40, 25, 18, 12, 5 };

        [Header("조각별 레어도 (1~5, 길이 = PieceCount)")]
        [Tooltip("배치는 기획 입력(특정 부위 희귀). 기본 = 분량표 순서: 1×10, 2×7, 3×6, 4×5, 5×2.")]
        public int[] pieceRarities =
        {
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3,
            4, 4, 4, 4, 4,
            5, 5
        };

        [Header("완성 일러스트 (고수위 — 검열판/키는 기획 확정 후. 비우면 placeholder)")]
        public Sprite illustration;

        [Header("오픈 연출 수치 (기획서 p47~49)")]
        [Tooltip("가챠권 흔들흔들 시간(기획: 약 2초, 포장 푸는 효과음 구간).")]
        [Min(0f)] public float ticketShakeDuration = 2f;
        [Tooltip("조각이 뒤집히며 등장 후 제자리로 날아가는 시간.")]
        [Min(0f)] public float pieceFlyDuration = 0.6f;
        [Tooltip("완성 시 퍼즐 선 사라지는 페이드 시간(이후 전체화면 자동 전환).")]
        [Min(0f)] public float completeLineFadeDuration = 0.8f;

        /// <summary>조각 인덱스의 레어도(1~5 클램프, 배열 밖/비정상은 1).</summary>
        public int RarityOf(int pieceIndex)
        {
            if (pieceRarities == null || pieceIndex < 0 || pieceIndex >= pieceRarities.Length) return 1;
            return Mathf.Clamp(pieceRarities[pieceIndex], 1, 5);
        }

        /// <summary>레어도(1~5)의 가중치. 표 밖/0 이하 방어는 1.</summary>
        public int WeightOf(int rarity)
        {
            int idx = Mathf.Clamp(rarity, 1, 5) - 1;
            if (rarityWeights == null || idx >= rarityWeights.Length) return 1;
            return Mathf.Max(1, rarityWeights[idx]);
        }
    }
}
