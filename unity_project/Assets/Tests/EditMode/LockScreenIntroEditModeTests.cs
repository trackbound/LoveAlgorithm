using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LoveAlgo.UI;
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Events;   // ShowLockScreenCommand, SubmitPasswordCommand, LockMode, CompletionHandle

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// 비밀번호 입력 커스텀 시스템 S1 신규 컴포넌트 중 코루틴이 없는 로직 단위 검증(EditMode — 포커스 불필요).
    /// 타임라인/진동 등 진짜 런타임이 필요한 검증은 PlayMode(LockScreenIntroPlayModeTests)로 분리.
    /// </summary>
    public class LockScreenIntroEditModeTests
    {
        [Test]
        public void IntroDirector_Advance_ClampsLargeDelta()
        {
            // 첫 진입 랜덤 점프 버그: 스폰/히치로 deltaTime이 커도(엔진 max 0.333s) 한 프레임에 maxStep만 진행해야
            // 연출이 한 번에 끝나지 않는다.
            Assert.AreEqual(0.25f, LockScreenIntroDirector.Advance(0.2f, 0.333f, 0.05f), 1e-5f, "큰 델타 → maxStep으로 클램프");
            Assert.AreEqual(0.21f, LockScreenIntroDirector.Advance(0.2f, 0.01f, 0.05f), 1e-5f, "작은 델타 → 그대로 진행");
        }

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

        [Test]
        public void LoginButton_Toggles_Sprite_And_Confirms()
        {
            // view + overlay + input 배선(Confirm 경로 확인)
            var viewGo = new GameObject("View");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            var vInputGo = new GameObject("VInput");
            vInputGo.transform.SetParent(viewGo.transform);
            var vInput = vInputGo.AddComponent<TMP_InputField>();
            view.Overlay = overlay;
            view.Input = vInput;
            viewGo.SetActive(true);

            // LoginButton 배선
            var go = new GameObject("LoginBtn");
            go.SetActive(false);
            var btnGo = new GameObject("Btn");
            btnGo.transform.SetParent(go.transform);
            var image = btnGo.AddComponent<Image>();
            var button = btnGo.AddComponent<Button>();
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            var lb = go.AddComponent<LoginButton>();
            lb.Input = vInput;
            lb.Button = button;
            lb.Image = image;
            lb.Label = label;
            lb.View = view;
            var act = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            var deact = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            lb.ActiveSprite = act;
            lb.DeactiveSprite = deact;
            go.SetActive(true);
            // EditMode 헤드리스에서는 MonoBehaviour.OnEnable이 자동 발화하지 않으므로(런타임 전용),
            // LoginButton이 OnEnable에서 수행하는 배선을 명시적으로 구동한다(어서션은 동일).
            lb.Button.onClick.AddListener(lb.View.Confirm); // OnEnable의 onClick→view.Confirm 대체
            lb.Refresh();                                   // OnEnable의 초기 Refresh 대체

            Assert.AreSame(deact, image.sprite, "빈 입력=deact");
            Assert.IsFalse(button.interactable, "빈 입력=비활성");

            lb.SetLabel("입력 완료");
            Assert.AreEqual("입력 완료", label.text);

            // Show로 활성 잠금화면 보장(OnShow가 input.text를 비우므로 입력은 그 이후에 설정).
            // view.OnEnable(EventBus 구독)은 헤드리스 미발화 → OnShow 직접 호출로 overlay 활성.
            string published = null;
            var sub = EventBus.Subscribe<SubmitPasswordCommand>(e => published = e.Password);
            view.OnShow(new ShowLockScreenCommand(LockMode.FirstSetup, false, null, new CompletionHandle()));

            // 입력 발생
            vInput.text = "1234";
            lb.Refresh();
            Assert.AreSame(act, image.sprite, "입력 있음=active");
            Assert.IsTrue(button.interactable, "입력 있음=활성");

            // 클릭 → view.Confirm → Submit 발행
            button.onClick.Invoke();
            Assert.AreEqual("1234", published, "클릭 → view.Confirm → Submit 발행");

            sub.Dispose();
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(viewGo);
        }

        [Test]
        public void View_FirstSetup_Configures_Plaintext_And_SetupGuide()
        {
            var viewGo = new GameObject("View");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            var vInputGo = new GameObject("VInput");
            vInputGo.transform.SetParent(viewGo.transform);
            var vInput = vInputGo.AddComponent<TMP_InputField>();
            view.Overlay = overlay;
            view.Input = vInput;

            // 하위 위젯
            var pf = viewGo.AddComponent<PasswordInputField>();
            pf.Input = vInput;
            view.PasswordField = pf;
            var guideGo = new GameObject("Guide");
            guideGo.transform.SetParent(viewGo.transform);
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(guideGo.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            var guide = guideGo.AddComponent<LockScreenGuideText>();
            guide.Label = label;
            guide.SetupText = "설정문구";
            guide.NormalText = "입력문구";
            view.Guide = guide;
            viewGo.SetActive(true);

            // EditMode 헤드리스에서는 OnEnable/EventBus 구독이 자동 발화하지 않으므로 OnShow를 직접 호출한다.
            // OnShow가 ConfigureForMode(→SetMasked/SetState)를 동기로 수행하므로 OnEnable 의존 없음.
            view.OnShow(new ShowLockScreenCommand(LockMode.FirstSetup, false, null, new CompletionHandle()));
            Assert.IsFalse(pf.Masked, "FirstSetup=평문(마스킹 off)");
            Assert.AreEqual("설정문구", label.text, "FirstSetup=설정 가이드");

            // 제출 시 '설정 완료!'로 전환
            guide.SetupCompleteText = "완료문구";
            vInput.text = "1234";
            view.Confirm();
            Assert.AreEqual("완료문구", label.text, "제출 후 설정 완료 텍스트");

            Object.DestroyImmediate(viewGo);
        }

        [Test]
        public void View_Normal_Confirm_Defers_Close()
        {
            var viewGo = new GameObject("View");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            var vInputGo = new GameObject("VInput");
            vInputGo.transform.SetParent(viewGo.transform);
            var vInput = vInputGo.AddComponent<TMP_InputField>();
            view.Overlay = overlay;
            view.Input = vInput;
            viewGo.SetActive(true);

            view.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            Assert.IsTrue(overlay.activeSelf, "Normal Show → 오버레이 활성");

            vInput.text = "1234";
            view.Confirm(); // Normal: 제출하되 닫지 않음(검증 대기)
            Assert.IsTrue(overlay.activeSelf, "Normal 제출 후에도 유지(재입력/검증 대기)");

            Object.DestroyImmediate(viewGo);
        }

        [Test]
        public void KeyResetButton_SetVisible_Toggles_Root()
        {
            var go = new GameObject("KeyReset");
            var rootVis = new GameObject("KeyVisual");
            rootVis.transform.SetParent(go.transform);
            var kr = go.AddComponent<KeyResetButton>();
            kr.Root = rootVis;

            kr.SetVisible(false);
            Assert.IsFalse(rootVis.activeSelf, "숨김");
            kr.SetVisible(true);
            Assert.IsTrue(rootVis.activeSelf, "노출");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void KeyResetButton_Yes_Publishes_ResetCommand()
        {
            var go = new GameObject("KeyReset");
            var kr = go.AddComponent<KeyResetButton>();

            LoveAlgo.Events.ShowModalCommand captured = default;
            bool gotModal = false;
            var sub1 = EventBus.Subscribe<LoveAlgo.Events.ShowModalCommand>(e => { captured = e; gotModal = true; });
            int resetCount = 0;
            var sub2 = EventBus.Subscribe<LoveAlgo.Events.RequestPasswordResetCommand>(_ => resetCount++);

            kr.RequestReset();
            Assert.IsTrue(gotModal, "재설정 확인 모달 발행");
            Assert.AreEqual(2, captured.Buttons.Count, "예/아니오 2버튼");

            captured.Handle.Select(1); // 아니오 → 재설정 안 함
            Assert.AreEqual(0, resetCount, "아니오는 재설정 발행 안 함");

            // 새 모달에서 예 선택
            gotModal = false;
            kr.RequestReset();
            captured.Handle.Select(0); // 예 → 재설정
            Assert.AreEqual(1, resetCount, "예 → RequestPasswordResetCommand 1회");

            sub1.Dispose(); sub2.Dispose();
            Object.DestroyImmediate(go);
        }
    }
}
