using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 잠금화면 확정 버튼(*View: LockScreen). 모드별 라벨("입력 완료"/"LOGIN")을 받고,
    /// 입력 유무로 active/deact 스프라이트와 interactable을 토글한다. 클릭 시 <see cref="LockScreenView.Confirm"/>.
    /// ADR-007: 표시 + 명령 위임만(저장은 Controller). 미바인딩 필드는 null-safe.
    /// </summary>
    public class LoginButton : MonoBehaviour
    {
        [SerializeField] TMP_InputField input;
        [SerializeField] Button button;
        [SerializeField] Image image;
        [SerializeField] TMP_Text label;
        [SerializeField] Sprite activeSprite;   // btn_login_active
        [SerializeField] Sprite deactiveSprite; // btn_login_deact
        [SerializeField] LockScreenView view;

        public TMP_InputField Input { get => input; set => input = value; }
        public Button Button { get => button; set => button = value; }
        public Image Image { get => image; set => image = value; }
        public TMP_Text Label { get => label; set => label = value; }
        public Sprite ActiveSprite { get => activeSprite; set => activeSprite = value; }
        public Sprite DeactiveSprite { get => deactiveSprite; set => deactiveSprite = value; }
        public LockScreenView View { get => view; set => view = value; }

        void OnEnable()
        {
            if (input != null) input.onValueChanged.AddListener(OnValueChanged);
            if (button != null) button.onClick.AddListener(OnClick);
            Refresh();
        }

        void OnDisable()
        {
            if (input != null) input.onValueChanged.RemoveListener(OnValueChanged);
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        void OnValueChanged(string _) => Refresh();

        /// <summary>입력 유무로 스프라이트/interactable 동기.</summary>
        public void Refresh()
        {
            bool has = input != null && !string.IsNullOrEmpty(input.text);
            if (image != null)
            {
                var s = has ? activeSprite : deactiveSprite;
                if (s != null) image.sprite = s;
            }
            if (button != null) button.interactable = has;
        }

        void OnClick()
        {
            if (view != null) view.Confirm();
        }
    }
}
