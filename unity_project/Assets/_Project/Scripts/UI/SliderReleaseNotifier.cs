using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 슬라이더 드래그 종료(포인터 업/드래그 끝) 시 <see cref="onRelease"/>를 1회 발화. uGUI <see cref="Slider"/>가
    /// 노출하지 않는 "값 확정" 시점을 제공 — 드래그 중 매 프레임 onValueChanged 연타와 분리해
    /// '일회성' 피드백(예: 설정 슬라이더 알림음)에 사용한다.
    ///
    /// <para>포인터 다운~업 사이 값 변화가 있었을 때만 발화(움직임 없는 클릭은 무음). 드래그는 EndDrag가
    /// 먼저, 곧이어 PointerUp이 오므로 <c>_tracking</c> 가드로 둘 중 한 번만 발화한다.</para>
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class SliderReleaseNotifier : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IEndDragHandler
    {
        [Tooltip("드래그를 놓는 순간(값이 바뀐 경우) 1회 호출.")]
        public UnityEvent onRelease = new UnityEvent();

        Slider _slider;
        bool _tracking; // 포인터 다운~업 사이만 true
        bool _changed;  // 그 사이 값 변화 발생 여부

        void Awake()
        {
            _slider = GetComponent<Slider>();
            if (_slider != null)
                _slider.onValueChanged.AddListener(_ => { if (_tracking) _changed = true; });
        }

        public void OnPointerDown(PointerEventData e) { _tracking = true; _changed = false; }
        public void OnPointerUp(PointerEventData e) => Fire();
        public void OnEndDrag(PointerEventData e) => Fire();

        void Fire()
        {
            if (!_tracking) return;
            _tracking = false;
            if (_changed) onRelease?.Invoke();
        }
    }
}
