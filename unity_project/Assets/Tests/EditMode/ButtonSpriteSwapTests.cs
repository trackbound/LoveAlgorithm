using NUnit.Framework;
using UnityEngine;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// ButtonSpriteSwap 순수 결정층 단위테스트(GameObject 불필요). 어댑터(포인터/interactable→Image)는
    /// PlayMode(<c>ButtonSpriteSwapPlayModeTests</c>)에서 검증.
    /// </summary>
    public class ButtonSpriteSwapTests
    {
        static Sprite NewSprite() => Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

        [Test]
        public void Resolve_Priority_DisabledOverOnOverHoverOverNormal()
        {
            Sprite n = NewSprite(), h = NewSprite(), on = NewSprite(), d = NewSprite();
            // 비활성 최우선(토글 ON·호버 무관)
            Assert.AreSame(d, ButtonSpriteSwap.ResolveSprite(false, true, true, n, h, on, d));
            // 토글 ON > 호버
            Assert.AreSame(on, ButtonSpriteSwap.ResolveSprite(true, true, true, n, h, on, d));
            // 호버
            Assert.AreSame(h, ButtonSpriteSwap.ResolveSprite(true, false, true, n, h, on, d));
            // 기본
            Assert.AreSame(n, ButtonSpriteSwap.ResolveSprite(true, false, false, n, h, on, d));
        }

        [Test]
        public void Resolve_NullVariants_FallBackToNormal()
        {
            Sprite n = NewSprite();
            Assert.AreSame(n, ButtonSpriteSwap.ResolveSprite(true, false, true, n, null, null, null));  // 호버 없음
            Assert.AreSame(n, ButtonSpriteSwap.ResolveSprite(true, true, false, n, null, null, null));  // on 없음
            Assert.AreSame(n, ButtonSpriteSwap.ResolveSprite(false, false, false, n, null, null, null)); // disabled 없음
        }

        [Test]
        public void ResolveTint_PressedMultipliesBase_OnlyWhenInteractable()
        {
            var baseColor = Color.white;
            var tint = new Color(0.7803922f, 0.7803922f, 0.7803922f, 1f); // C7C7C7

            // 눌림 + 상호작용 가능 → base * tint (어두워짐)
            Assert.AreEqual(baseColor * tint, ButtonSpriteSwap.ResolveTint(true, true, baseColor, tint));
            // 안 눌림 → base 유지
            Assert.AreEqual(baseColor, ButtonSpriteSwap.ResolveTint(true, false, baseColor, tint));
            // 비활성이면 눌림이어도 base 유지(비활성 색은 스프라이트가 담당)
            Assert.AreEqual(baseColor, ButtonSpriteSwap.ResolveTint(false, true, baseColor, tint));
        }

        [Test]
        public void ResolveTextColor_Priority_DisabledOverOnOverHoverOverNormal()
        {
            var c = new ButtonSpriteSwap.TextColorBlock
            {
                drive = true,
                normal = Color.black,   // OFF/기본
                hover = Color.white,
                on = Color.red,         // 토글 ON
                disabled = Color.gray,
            };

            // 비활성 최우선(ON·호버 무관)
            Assert.AreEqual(c.disabled, ButtonSpriteSwap.ResolveTextColor(false, true, true, c));
            // ON > 호버 (AUTO 버튼: ON 상태가 호버를 이김)
            Assert.AreEqual(c.on, ButtonSpriteSwap.ResolveTextColor(true, true, true, c));
            // 호버 (호버만 바꾸는 케이스)
            Assert.AreEqual(c.hover, ButtonSpriteSwap.ResolveTextColor(true, false, true, c));
            // OFF/기본
            Assert.AreEqual(c.normal, ButtonSpriteSwap.ResolveTextColor(true, false, false, c));
        }
    }
}
