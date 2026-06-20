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
    /// ButtonStateDriver 어댑터 PlayMode: 포인터/SetOn/SetInteractable가 상태 자식을 정확히 하나 활성으로
    /// 구동하고(child-swap), 활성 자식 Image에 pressed 틴트를 곱하며, 라벨 색을 상태대로 바꾸는지.
    /// </summary>
    public class ButtonStateDriverPlayModeTests
    {
        static Sprite NewSprite() => Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

        static GameObject NewStateChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().sprite = NewSprite();
            return go;
        }

        [UnityTest]
        public IEnumerator ChildSwap_Tint_LabelColor_DrivenByState()
        {
            var root = new GameObject("Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.SetActive(false);
            var driver = root.AddComponent<ButtonStateDriver>();

            var normal = NewStateChild(root.transform, "Normal");
            var hover = NewStateChild(root.transform, "Hover");
            var on = NewStateChild(root.transform, "On");
            var disabled = NewStateChild(root.transform, "Disabled");

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            var label = labelGo.AddComponent<TMPro.TextMeshProUGUI>();

            driver.NormalState = normal;
            driver.HoverState = hover;
            driver.OnState = on;
            driver.DisabledState = disabled;
            driver.Label = label;
            driver.TextColors = new ButtonStateDriver.TextColorBlock
            {
                drive = true, normal = Color.black, hover = Color.white, on = Color.red, disabled = Color.gray,
            };

            root.SetActive(true); // OnEnable: base 캡처 + Apply
            yield return null;

            try
            {
                // 기본: Normal만 활성, 라벨 검정
                Assert.IsTrue(normal.activeSelf && !hover.activeSelf && !on.activeSelf && !disabled.activeSelf, "기본=Normal");
                Assert.AreEqual(Color.black, label.color, "기본 라벨 검정");

                // 호버 → Hover만 활성, 라벨 흰
                driver.OnPointerEnter(new PointerEventData(EventSystem.current));
                Assert.IsTrue(hover.activeSelf && !normal.activeSelf, "호버=Hover");
                Assert.AreEqual(Color.white, label.color, "호버 라벨 흰");

                // 눌림 → 활성(Hover) 자식 Image에 C7C7C7 틴트
                driver.OnPointerDown(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                var tint = new Color(0.7803922f, 0.7803922f, 0.7803922f, 1f);
                Assert.AreEqual(Color.white * tint, hover.GetComponent<Image>().color, "눌림 틴트");
                driver.OnPointerUp(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                Assert.AreEqual(Color.white, hover.GetComponent<Image>().color, "떼면 복원");

                // 이탈 → Normal
                driver.OnPointerExit(new PointerEventData(EventSystem.current));
                Assert.IsTrue(normal.activeSelf, "이탈=Normal");

                // 토글 ON → On만 활성(호버해도 ON 유지), 라벨 빨강
                driver.SetOn(true);
                Assert.IsTrue(on.activeSelf && !normal.activeSelf, "ON=On");
                Assert.AreEqual(Color.red, label.color, "ON 라벨 빨강");
                driver.OnPointerEnter(new PointerEventData(EventSystem.current));
                Assert.IsTrue(on.activeSelf, "ON 중 호버해도 On 유지");
                driver.OnPointerExit(new PointerEventData(EventSystem.current));
                driver.SetOn(false);

                // 비활성 → Disabled, 라벨 회색
                driver.SetInteractable(false);
                Assert.IsTrue(disabled.activeSelf && !normal.activeSelf, "비활성=Disabled");
                Assert.AreEqual(Color.gray, label.color, "비활성 라벨 회색");
                driver.SetInteractable(true);
                Assert.IsTrue(normal.activeSelf, "재활성=Normal");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator MissingChild_FallsBackToNormal()
        {
            var root = new GameObject("Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.SetActive(false);
            var driver = root.AddComponent<ButtonStateDriver>();
            var normal = NewStateChild(root.transform, "Normal");
            driver.NormalState = normal; // hover/on/disabled 비움
            root.SetActive(true);
            yield return null;

            try
            {
                driver.OnPointerEnter(new PointerEventData(EventSystem.current));
                Assert.IsTrue(normal.activeSelf, "Hover 자식 없으면 Normal 유지");
                driver.SetInteractable(false);
                Assert.IsTrue(normal.activeSelf, "Disabled 자식 없으면 Normal 유지");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
