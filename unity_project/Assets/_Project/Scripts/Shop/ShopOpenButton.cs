using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowShopCommand
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점 열기 버튼 어댑터(얇은 UI — ADR-007). 클릭 → <see cref="ShowShopCommand"/> 발행만.
    /// 스케줄 화면의 "아이템 구매" 버튼(내부 콘텐츠 기획서 p4 "클릭 시 상점 UI 출력")에 부착 —
    /// Schedule↔Shop 피처 간 직접 참조 없이 Core 이벤트로 경유.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ShopOpenButton : MonoBehaviour
    {
        Button _button;

        void Awake() => _button = GetComponent<Button>();

        void OnEnable()
        {
            if (_button == null) _button = GetComponent<Button>();
            _button.onClick.AddListener(Open);
        }

        void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(Open);
        }

        void Open() => EventBus.Publish(new ShowShopCommand());
    }
}
