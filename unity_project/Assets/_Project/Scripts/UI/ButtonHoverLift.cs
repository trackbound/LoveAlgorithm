using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 호버 피드백: 버튼을 살짝 위로 띄우고(라이즈) 이미지를 <see cref="hoverSprite"/>로 교체, 이탈 시 원복한다.
    /// 자식 child-swap이 아닌 <b>단일 Image 버튼</b>용(Extra 팝업 SCENE/CG/COLLECT 등). 트윈은
    /// <see cref="PopupSlideAnimator"/>와 동일하게 코루틴+unscaled time+SmoothStep(일시정지에서도 동작).
    /// 평상 스프라이트·정착 위치는 <see cref="OnEnable"/> 시 1회 캡처한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ButtonHoverLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("스프라이트를 교체할 Image(비우면 이 오브젝트의 Image).")]
        [SerializeField] Image targetImage;
        [Tooltip("호버 시 스프라이트(비우면 교체 없이 라이즈만). 평상 스프라이트는 OnEnable에서 캡처.")]
        [SerializeField] Sprite hoverSprite;
        [Tooltip("호버 시 위로 띄울 거리(px, anchoredPosition.y).")]
        [SerializeField] float liftPixels = 16f;
        [Tooltip("라이즈/원복 트윈 시간(초).")]
        [SerializeField] float duration = 0.12f;

        RectTransform _rt;
        Sprite _normalSprite;
        Vector2 _basePos;
        bool _captured;
        Coroutine _co;

        public Image TargetImage { get => targetImage; set => targetImage = value; }
        public Sprite HoverSprite { get => hoverSprite; set => hoverSprite = value; }
        public float LiftPixels { get => liftPixels; set => liftPixels = value; }

        void Capture()
        {
            if (_captured) return;
            _rt = (RectTransform)transform;
            if (targetImage == null) targetImage = GetComponent<Image>();
            _normalSprite = targetImage != null ? targetImage.sprite : null;
            _basePos = _rt.anchoredPosition;
            _captured = true;
        }

        void OnEnable() => Capture();

        void OnDisable()
        {
            StopCo();
            SnapTo(_basePos, _normalSprite); // 호버 중 비활성 시 라이즈/호버 스프라이트 잔류 방지
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Capture();
            Play(_basePos + new Vector2(0f, liftPixels), hoverSprite != null ? hoverSprite : _normalSprite);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Capture();
            Play(_basePos, _normalSprite);
        }

        // 스프라이트 즉시 교체 + 목표 위치로 트윈 시작.
        void Play(Vector2 targetPos, Sprite sprite)
        {
            if (targetImage != null && sprite != null) targetImage.sprite = sprite;
            StopCo();
            if (isActiveAndEnabled) _co = StartCoroutine(Animate(targetPos));
            else SnapTo(targetPos, sprite);
        }

        IEnumerator Animate(Vector2 targetPos)
        {
            Vector2 start = _rt.anchoredPosition;
            float dur = Mathf.Max(0.0001f, duration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                _rt.anchoredPosition = Vector2.LerpUnclamped(start, targetPos, k);
                yield return null;
            }
            _rt.anchoredPosition = targetPos;
            _co = null;
        }

        void SnapTo(Vector2 pos, Sprite sprite)
        {
            if (_rt == null) _rt = (RectTransform)transform;
            _rt.anchoredPosition = pos;
            if (targetImage != null && sprite != null) targetImage.sprite = sprite;
        }

        void StopCo()
        {
            if (_co != null) { StopCoroutine(_co); _co = null; }
        }
    }
}
