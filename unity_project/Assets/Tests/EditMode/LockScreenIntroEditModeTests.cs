using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// 비밀번호 입력 커스텀 시스템 S1 신규 컴포넌트 중 코루틴이 없는 로직 단위 검증(EditMode — 포커스 불필요).
    /// 타임라인/진동 등 진짜 런타임이 필요한 검증은 PlayMode(LockScreenIntroPlayModeTests)로 분리.
    /// </summary>
    public class LockScreenIntroEditModeTests
    {
        [Test]
        public void GuideText_SetState_Swaps_Label_Text()
        {
            var go = new GameObject("Guide");
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            var guide = go.AddComponent<LockScreenGuideText>();
            guide.Label = label;
            guide.SetupText = "설정문구";
            guide.SetupCompleteText = "완료문구";
            guide.NormalText = "입력문구";
            guide.LostText = "분실문구";

            guide.SetState(LockScreenGuideText.LockGuideState.Setup);
            Assert.AreEqual("설정문구", label.text);
            guide.SetState(LockScreenGuideText.LockGuideState.SetupComplete);
            Assert.AreEqual("완료문구", label.text);
            guide.SetState(LockScreenGuideText.LockGuideState.Normal);
            Assert.AreEqual("입력문구", label.text);
            guide.SetState(LockScreenGuideText.LockGuideState.Lost);
            Assert.AreEqual("분실문구", label.text);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PasswordField_Sets_Limit_And_Toggles_Masking()
        {
            var go = new GameObject("PwField");
            go.SetActive(false);
            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(go.transform);
            var input = inputGo.AddComponent<TMP_InputField>();
            var iconGo = new GameObject("Eye");
            iconGo.transform.SetParent(go.transform);
            var icon = iconGo.AddComponent<Image>();
            var field = go.AddComponent<PasswordInputField>();
            field.Input = input;
            field.EyeIcon = icon;
            var closed = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            var open = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            field.EyeClosedSprite = closed;
            field.EyeOpenSprite = open;
            field.MaxLength = 7;
            go.SetActive(true); // OnEnable → characterLimit 적용

            Assert.AreEqual(7, input.characterLimit, "7자 제한 적용");

            field.SetMasked(true);
            Assert.IsTrue(field.Masked);
            Assert.AreEqual(TMP_InputField.ContentType.Password, input.contentType, "마스킹 시 Password");
            Assert.AreSame(closed, icon.sprite, "마스킹=감은눈");

            field.ToggleEye();
            Assert.IsFalse(field.Masked);
            Assert.AreEqual(TMP_InputField.ContentType.Standard, input.contentType, "노출 시 Standard");
            Assert.AreSame(open, icon.sprite, "노출=뜬눈");

            Object.DestroyImmediate(go);
        }
    }
}
