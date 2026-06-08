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
    }
}
