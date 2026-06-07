using NUnit.Framework;
using UnityEngine;
using LoveAlgo.UI;
using VS = LoveAlgo.UI.StyledButton.VisualState;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// StyledButton 순수 결정층 단위테스트(GameObject 불필요). SelectionState↔VisualState 매핑과
    /// 비주얼 적용(어댑터)은 PlayMode(<c>StyledButtonPlayModeTests</c>)에서 검증한다.
    /// </summary>
    public class StyledButtonTests
    {
        // 매 호출 distinct 인스턴스(참조 동일성 검증용).
        static Sprite NewSprite() => Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

        [Test]
        public void ResolveEffective_SelectedFocus_MapsToNormal()
        {
            // EventSystem 포커스 잔류(Selected)는 Normal로 — 클릭 후 스티키 하이라이트 제거.
            Assert.AreEqual(VS.Normal, StyledButton.ResolveEffective(VS.Selected, false));
        }

        [Test]
        public void ResolveEffective_OrdinaryStates_PassThrough()
        {
            Assert.AreEqual(VS.Normal, StyledButton.ResolveEffective(VS.Normal, false));
            Assert.AreEqual(VS.Highlighted, StyledButton.ResolveEffective(VS.Highlighted, false));
            Assert.AreEqual(VS.Pressed, StyledButton.ResolveEffective(VS.Pressed, false));
            Assert.AreEqual(VS.Disabled, StyledButton.ResolveEffective(VS.Disabled, false));
        }

        [Test]
        public void ResolveEffective_Override_ForcesSelected()
        {
            // 탭 active: 외부 override가 Selected를 강제(원시 상태 무관).
            Assert.AreEqual(VS.Selected, StyledButton.ResolveEffective(VS.Normal, true));
            Assert.AreEqual(VS.Selected, StyledButton.ResolveEffective(VS.Highlighted, true));
            Assert.AreEqual(VS.Selected, StyledButton.ResolveEffective(VS.Pressed, true));
        }

        [Test]
        public void SpriteForState_ReturnsPerStateSprite()
        {
            Sprite n = NewSprite(), h = NewSprite(), p = NewSprite(), s = NewSprite(), d = NewSprite();
            Assert.AreSame(n, StyledButton.SpriteForState(VS.Normal, n, h, p, s, d));
            Assert.AreSame(h, StyledButton.SpriteForState(VS.Highlighted, n, h, p, s, d));
            Assert.AreSame(p, StyledButton.SpriteForState(VS.Pressed, n, h, p, s, d));
            Assert.AreSame(s, StyledButton.SpriteForState(VS.Selected, n, h, p, s, d));
            Assert.AreSame(d, StyledButton.SpriteForState(VS.Disabled, n, h, p, s, d));
        }

        [Test]
        public void SpriteForState_PressedAndSelected_FallBackToHighlighted_WhenNull()
        {
            Sprite n = NewSprite(), h = NewSprite();
            // Pressed 전용 없으면 hover 스프라이트 유지 → 그 위에 ColorBlock pressed 틴트(C8C8C8)가 곱해진다.
            Assert.AreSame(h, StyledButton.SpriteForState(VS.Pressed, n, h, null, null, null));
            Assert.AreSame(h, StyledButton.SpriteForState(VS.Selected, n, h, null, null, null));
        }

        [Test]
        public void SpriteForState_Normal_NullPassesThrough()
        {
            // Normal에 전용 스프라이트 없으면 null → 호출 측이 overrideSprite를 비워 base sprite로 복귀.
            Assert.IsNull(StyledButton.SpriteForState(VS.Normal, null, NewSprite(), null, null, null));
        }

        [Test]
        public void TextColorForState_ReturnsPerStateColor()
        {
            var c = new StyledButton.TextColorBlock
            {
                drive = true,
                normal = Color.black,
                highlighted = Color.white,
                pressed = Color.red,
                selected = Color.green,
                disabled = Color.gray,
            };
            Assert.AreEqual(Color.black, StyledButton.TextColorForState(VS.Normal, c));
            Assert.AreEqual(Color.white, StyledButton.TextColorForState(VS.Highlighted, c));
            Assert.AreEqual(Color.red, StyledButton.TextColorForState(VS.Pressed, c));
            Assert.AreEqual(Color.green, StyledButton.TextColorForState(VS.Selected, c));
            Assert.AreEqual(Color.gray, StyledButton.TextColorForState(VS.Disabled, c));
        }
    }
}
