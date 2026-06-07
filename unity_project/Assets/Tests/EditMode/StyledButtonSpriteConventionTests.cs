using System.Collections.Generic;
using NUnit.Framework;
using Conv = LoveAlgo.DevTools.Editor.StyledButtonSpriteConvention;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// StyledButton 일괄배선 툴의 순수 네이밍 resolver 단위테스트(GameObject/AssetDatabase 불필요).
    /// SerializedObject 스왑·AssetDatabase 열거 등 에디터 글루는 Title 롤아웃으로 검증한다.
    /// </summary>
    public class StyledButtonSpriteConventionTests
    {
        static HashSet<string> Set(params string[] names) => new HashSet<string>(names);

        [Test]
        public void Resolve_AllThreeSiblings_MapsEach()
        {
            var r = Conv.Resolve("btn_x", Set("btn_x", "btn_x_hover", "btn_x_disabled", "btn_x_on"));
            Assert.AreEqual("btn_x_hover", r.Highlighted);
            Assert.AreEqual("btn_x_disabled", r.Disabled);
            Assert.AreEqual("btn_x_on", r.Selected);
        }

        [Test]
        public void Resolve_NoSiblings_AllNull() // Title 케이스(형제 없음 → 시각 무변경)
        {
            var r = Conv.Resolve("btn_start", Set("btn_start", "btn_config", "btn_exit"));
            Assert.IsNull(r.Highlighted);
            Assert.IsNull(r.Disabled);
            Assert.IsNull(r.Selected);
            Assert.IsFalse(r.Any);
        }

        [Test]
        public void Resolve_OnlyDisabled_DisabledOnly() // config/saveload 화살표(경계 회색화)
        {
            var r = Conv.Resolve("btn_config_arrow_left",
                Set("btn_config_arrow_left", "btn_config_arrow_left_disabled"));
            Assert.IsNull(r.Highlighted);
            Assert.AreEqual("btn_config_arrow_left_disabled", r.Disabled);
            Assert.IsNull(r.Selected);
        }

        [Test]
        public void Resolve_OnlyOn_SelectedOnly() // config mode 토글
        {
            var r = Conv.Resolve("btn_config_mode", Set("btn_config_mode", "btn_config_mode_on"));
            Assert.IsNull(r.Highlighted);
            Assert.IsNull(r.Disabled);
            Assert.AreEqual("btn_config_mode_on", r.Selected);
        }

        [Test]
        public void Resolve_LocaleSibling_Ignored() // _kr 는 상태가 아님
        {
            var r = Conv.Resolve("btn_title", Set("btn_title", "btn_title_kr"));
            Assert.IsFalse(r.Any);
        }

        [Test]
        public void Resolve_FromVariantCurrentSprite_NormalizesFirst()
        {
            // 현재 스프라이트가 hover 변형이어도 base로 정규화 후 형제 해석.
            var baseName = Conv.NormalizeBase("btn_x_hover");
            var r = Conv.Resolve(baseName, Set("btn_x", "btn_x_hover", "btn_x_on"));
            Assert.AreEqual("btn_x_hover", r.Highlighted);
            Assert.AreEqual("btn_x_on", r.Selected);
            Assert.IsNull(r.Disabled);
        }

        [Test]
        public void NormalizeBase_StripsStateSuffix_PassThroughOtherwise()
        {
            Assert.AreEqual("btn_x", Conv.NormalizeBase("btn_x_hover"));
            Assert.AreEqual("btn_x", Conv.NormalizeBase("btn_x_disabled"));
            Assert.AreEqual("btn_x", Conv.NormalizeBase("btn_x_on"));
            Assert.AreEqual("btn_x", Conv.NormalizeBase("btn_x"));          // base passthrough
            Assert.AreEqual("btn_title_kr", Conv.NormalizeBase("btn_title_kr")); // 로케일은 안 벗김
        }

        [Test]
        public void IsStateVariant_DetectsWhitelistSuffixesOnly()
        {
            Assert.IsTrue(Conv.IsStateVariant("btn_x_hover"));
            Assert.IsTrue(Conv.IsStateVariant("btn_x_disabled"));
            Assert.IsTrue(Conv.IsStateVariant("btn_x_on"));
            Assert.IsFalse(Conv.IsStateVariant("btn_x"));
            Assert.IsFalse(Conv.IsStateVariant("btn_title_kr"));
        }
    }
}
