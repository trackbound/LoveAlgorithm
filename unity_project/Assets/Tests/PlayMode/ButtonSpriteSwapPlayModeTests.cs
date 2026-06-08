using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// ButtonSpriteSwap 어댑터 PlayMode: 포인터 enter/exit·SetOn·SetInteractable가 실제 Image.sprite를
    /// 순수 결정대로 구동하는지(네이티브 Button + ColorTint 동반, raw 포인터 이벤트라 포커스 무관).
    /// </summary>
    public class ButtonSpriteSwapPlayModeTests
    {
        static Sprite NewSprite() => Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

        [UnityTest]
        public IEnumerator Hover_On_Disabled_DriveImageSprite()
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.SetActive(false);
            var img = go.GetComponent<Image>();
            var normal = NewSprite();
            img.sprite = normal; // 기준
            var swap = go.AddComponent<ButtonSpriteSwap>();
            swap.TargetImage = img;
            var hover = NewSprite();
            var on = NewSprite();
            var disabled = NewSprite();
            swap.HoverSprite = hover;
            swap.OnSprite = on;
            swap.DisabledSprite = disabled;

            go.SetActive(true); // OnEnable: normal 캡처 + Apply
            yield return null;

            try
            {
                Assert.AreSame(normal, img.sprite, "기본");

                swap.OnPointerEnter(new PointerEventData(EventSystem.current));
                Assert.AreSame(hover, img.sprite, "호버 → hover");

                swap.OnPointerExit(new PointerEventData(EventSystem.current));
                Assert.AreSame(normal, img.sprite, "이탈 → normal");

                swap.SetOn(true);
                Assert.AreSame(on, img.sprite, "토글 ON → on");
                swap.OnPointerEnter(new PointerEventData(EventSystem.current));
                Assert.AreSame(on, img.sprite, "ON 중 호버해도 on 유지");
                swap.SetOn(false);
                swap.OnPointerExit(new PointerEventData(EventSystem.current));

                swap.SetInteractable(false);
                Assert.AreSame(disabled, img.sprite, "비활성 → disabled");
                swap.SetInteractable(true);
                Assert.AreSame(normal, img.sprite, "재활성 → normal");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
