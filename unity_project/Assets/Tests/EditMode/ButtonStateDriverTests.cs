using NUnit.Framework;
using UnityEngine;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// ButtonStateDriver 순수 결정층 단위테스트(GameObject 불필요). 어댑터(포인터/SetOn/SetInteractable→자식 SetActive)는
    /// PlayMode(ButtonStateDriverPlayModeTests)에서 검증.
    /// </summary>
    public class ButtonStateDriverTests
    {
        [Test]
        public void ResolveActiveState_Priority_DisabledOverOnOverHoverOverNormal()
        {
            // 비활성 최우선(ON·호버 무관)
            Assert.AreEqual(ButtonStateDriver.State.Disabled, ButtonStateDriver.ResolveActiveState(false, true, true));
            // ON > 호버
            Assert.AreEqual(ButtonStateDriver.State.On, ButtonStateDriver.ResolveActiveState(true, true, true));
            // 호버
            Assert.AreEqual(ButtonStateDriver.State.Hover, ButtonStateDriver.ResolveActiveState(true, false, true));
            // 기본
            Assert.AreEqual(ButtonStateDriver.State.Normal, ButtonStateDriver.ResolveActiveState(true, false, false));
        }

        [Test]
        public void ResolvePressedTint_MultipliesBase_OnlyWhenInteractableAndPressed()
        {
            var baseColor = Color.white;
            var tint = new Color(0.7803922f, 0.7803922f, 0.7803922f, 1f); // C7C7C7

            Assert.AreEqual(baseColor * tint, ButtonStateDriver.ResolvePressedTint(true, true, baseColor, tint));
            Assert.AreEqual(baseColor, ButtonStateDriver.ResolvePressedTint(true, false, baseColor, tint));
            Assert.AreEqual(baseColor, ButtonStateDriver.ResolvePressedTint(false, true, baseColor, tint)); // 비활성이면 패스
        }

        [Test]
        public void ResolvePressedScale_OnlyWhenInteractableAndPressed()
        {
            Assert.AreEqual(0.95f, ButtonStateDriver.ResolvePressedScale(true, true, 0.95f));   // 눌림 → 축소
            Assert.AreEqual(1f, ButtonStateDriver.ResolvePressedScale(true, false, 0.95f));     // 안 눌림 → 1
            Assert.AreEqual(1f, ButtonStateDriver.ResolvePressedScale(false, true, 0.95f));     // 비활성 → 1
        }

        [Test]
        public void ResolveTextColor_Priority_DisabledOverOnOverHoverOverNormal()
        {
            var c = new ButtonStateDriver.TextColorBlock
            {
                drive = true,
                normal = Color.black,
                hover = Color.white,
                on = Color.red,
                disabled = Color.gray,
            };
            Assert.AreEqual(c.disabled, ButtonStateDriver.ResolveTextColor(ButtonStateDriver.State.Disabled, c));
            Assert.AreEqual(c.on, ButtonStateDriver.ResolveTextColor(ButtonStateDriver.State.On, c));
            Assert.AreEqual(c.hover, ButtonStateDriver.ResolveTextColor(ButtonStateDriver.State.Hover, c));
            Assert.AreEqual(c.normal, ButtonStateDriver.ResolveTextColor(ButtonStateDriver.State.Normal, c));
        }

        [Test]
        public void ResolveSfx_RoleAndHover_NullTableSilent()
        {
            // table null → 항상 null (무음)
            Assert.IsNull(ButtonStateDriver.ResolveSfx(ButtonStateDriver.UiSoundRole.General, true, null));
            // Silent 역할 → null
            var table = ScriptableObject.CreateInstance<UiSoundSO>();
            Assert.IsNull(ButtonStateDriver.ResolveSfx(ButtonStateDriver.UiSoundRole.Silent, true, table));
            // General/Choice는 table 항목을 반환(기본 빈 문자열 — null 아님)
            Assert.AreEqual(table.ButtonHover, ButtonStateDriver.ResolveSfx(ButtonStateDriver.UiSoundRole.General, true, table));
            Assert.AreEqual(table.ButtonClick, ButtonStateDriver.ResolveSfx(ButtonStateDriver.UiSoundRole.General, false, table));
            Assert.AreEqual(table.ChoiceHover, ButtonStateDriver.ResolveSfx(ButtonStateDriver.UiSoundRole.Choice, true, table));
            Assert.AreEqual(table.ChoiceClick, ButtonStateDriver.ResolveSfx(ButtonStateDriver.UiSoundRole.Choice, false, table));
            Object.DestroyImmediate(table);
        }
    }
}
