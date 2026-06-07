using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// StyledButton 어댑터 PlayMode: 순수 결정층(<c>StyledButtonTests</c>)이 실제 컴포넌트의 비주얼로 배선되는지.
    /// 탭 active용 <see cref="StyledButton.SetSelected"/>가 즉시 overrideSprite/라벨 색을 selected/normal로 구동하는지
    /// 검증한다(색 틴트는 네이티브 ColorBlock 영역이므로 transition=None으로 어댑터만 격리).
    /// </summary>
    public class StyledButtonPlayModeTests
    {
        static Sprite NewSprite() => Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

        [UnityTest]
        public IEnumerator SetSelected_DrivesOverrideSprite_AndLabelColor()
        {
            var go = new GameObject("Styled", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.SetActive(false); // 바인딩 후 활성화 → OnEnable에서 Normal 적용 타이밍 정렬
            var img = go.GetComponent<Image>();
            var styled = go.AddComponent<StyledButton>();
            styled.transition = Selectable.Transition.None; // 색 틴트는 네이티브 — 어댑터(스프라이트/텍스트)만 검증
            styled.targetGraphic = img;

            var sel = NewSprite();
            styled.SelectedSprite = sel; // NormalSprite는 null → Normal 시 overrideSprite 해제(=base sprite)

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            styled.Label = tmp;
            styled.TextColors = new StyledButton.TextColorBlock
            {
                drive = true,
                normal = Color.black,
                highlighted = Color.white,
                pressed = Color.white,
                selected = Color.green,
                disabled = Color.gray,
            };

            go.SetActive(true);
            yield return null;

            try
            {
                styled.SetSelected(true);
                Assert.AreSame(sel, img.overrideSprite, "active 탭 → overrideSprite=selectedSprite");
                Assert.AreEqual(Color.green, tmp.color, "active 탭 → 라벨 selected 색");

                styled.SetSelected(false);
                Assert.IsNull(img.overrideSprite, "비활성 → overrideSprite 해제(base sprite 복귀)");
                Assert.AreEqual(Color.black, tmp.color, "비활성 → 라벨 normal 색");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [UnityTest]
        public IEnumerator FocusedButton_StillShowsHoverSprite_OnPointerEnter()
        {
            // 버그 재현·수정: 클릭으로 EventSystem 포커스가 남으면 Unity는 호버해도 SelectionState를 Selected로
            // 돌려준다(Highlighted보다 우선). 그래도 포인터가 안에 있으면 hover가 떠야 한다.
            var es = new GameObject("EventSystem", typeof(EventSystem));
            var evs = es.GetComponent<EventSystem>();
            var go = new GameObject("Styled", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.SetActive(false);
            var img = go.GetComponent<Image>();
            var styled = go.AddComponent<StyledButton>();
            styled.transition = Selectable.Transition.None; // 어댑터(스프라이트)만 격리
            styled.targetGraphic = img;
            var hover = NewSprite();
            styled.HighlightedSprite = hover; // NormalSprite=null → Normal 시 overrideSprite 해제
            go.SetActive(true);
            yield return null;

            try
            {
                // 클릭으로 포커스 잔류 모사(hasSelection=true → raw가 Selected)
                evs.SetSelectedGameObject(go);
                yield return null;
                Assert.IsNull(img.overrideSprite, "포커스만(호버 아님) → Normal");

                styled.OnPointerEnter(new PointerEventData(evs));
                Assert.AreSame(hover, img.overrideSprite, "포커스 상태에서도 호버 시 hover 스프라이트(버그 수정)");

                styled.OnPointerExit(new PointerEventData(evs));
                Assert.IsNull(img.overrideSprite, "이탈 → Normal");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(es);
            }
        }
    }
}
