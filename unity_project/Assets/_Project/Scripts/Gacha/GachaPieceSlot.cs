using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Gacha
{
    /// <summary>
    /// 퍼즐판 칸 1개(*Slot) — 보유 시 일러스트의 해당 영역만 드러낸다(칸의 RectMask2D 안에
    /// 보드 크기 일러스트를 칸 위치만큼 음수 오프셋으로 깔아 자르기 — 아트 분할 없이 어떤
    /// 스프라이트도 30조각으로 동작). 일러스트가 없으면 placeholder 색 + 조각 번호.
    /// 오프셋 베이킹은 빌더/뷰(Setup), 런타임은 보유 토글만.
    /// </summary>
    public class GachaPieceSlot : MonoBehaviour
    {
        [SerializeField] GameObject revealRoot;   // 보유 시 활성(RectMask2D 보유)
        [SerializeField] Image illustImage;       // 보드 크기 일러스트(오프셋 배치) — 없으면 비활성
        [SerializeField] Image placeholderImage;  // 일러스트 부재 시 색 박스
        [SerializeField] TMP_Text indexLabel;     // placeholder용 조각 번호
        [SerializeField] GameObject emptyRoot;    // 미보유 칸 표시(빈 칸 테두리)

        public GameObject RevealRoot { get => revealRoot; set => revealRoot = value; }
        public Image IllustImage { get => illustImage; set => illustImage = value; }
        public Image PlaceholderImage { get => placeholderImage; set => placeholderImage = value; }
        public TMP_Text IndexLabel { get => indexLabel; set => indexLabel = value; }
        public GameObject EmptyRoot { get => emptyRoot; set => emptyRoot = value; }

        public int PieceIndex { get; private set; }
        public bool IsOwned { get; private set; }

        /// <summary>칸 구성 — 일러스트 오프셋(보드 좌하단 기준)과 placeholder 라벨을 베이킹.</summary>
        public void Setup(int pieceIndex, int col, int row, Vector2 cellSize, Vector2 boardSize, Sprite illustration)
        {
            PieceIndex = pieceIndex;

            bool hasIllust = illustration != null && illustImage != null;
            if (illustImage != null)
            {
                illustImage.gameObject.SetActive(hasIllust);
                if (hasIllust)
                {
                    illustImage.sprite = illustration;
                    var rt = (RectTransform)illustImage.transform;
                    rt.pivot = new Vector2(0, 0);
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                    rt.sizeDelta = boardSize;
                    // 칸(col,row — row 0 = 최상단)의 좌하단이 보드에서 차지하는 위치만큼 당긴다.
                    float x = -col * cellSize.x;
                    float y = -(boardSize.y - (row + 1) * cellSize.y);
                    rt.anchoredPosition = new Vector2(x, y);
                }
            }
            if (placeholderImage != null) placeholderImage.gameObject.SetActive(!hasIllust);
            if (indexLabel != null)
            {
                indexLabel.gameObject.SetActive(!hasIllust);
                indexLabel.text = (pieceIndex + 1).ToString();
            }
            SetOwned(false);
        }

        public void SetOwned(bool owned)
        {
            IsOwned = owned;
            if (revealRoot != null) revealRoot.SetActive(owned);
            if (emptyRoot != null) emptyRoot.SetActive(!owned);
        }
    }
}
