using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // OverlayGate
using LoveAlgo.Events; // ShowExtraCommand
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// Extra(부가 콘텐츠) 팝업 뷰(*View, ADR-013 Overlay). <see cref="ShowExtraCommand"/>로 표시.
    /// 표시/은닉·OverlayGate·형제정렬·입력차단 패턴은 SettingsView/SaveLoadView와 1:1, 연출은
    /// <see cref="PopupSlideAnimator"/>(있을 때)에 위임한다(우→좌 슬라이드 인 / 좌→우 아웃).
    /// 내부 콘텐츠 버튼(수집/CG/씬 등)은 후속 마일스톤 — 여기선 열기/닫기만 담당한다.
    /// </summary>
    public class ExtraView : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Button closeButton;
        [Tooltip("있으면 슬라이드/페이드 연출, 없으면 즉시 표시.")]
        [SerializeField] PopupSlideAnimator slide;

        IDisposable _showSub;
        IDisposable _gate; // OverlayGate 토큰(표시 중에만 non-null — 뒤로가기 CloseTop이 닫기 호출)
        bool _visible;

        void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(() => SetVisible(false));
            SetVisible(false); // 부팅 숨김(프리팹 CanvasGroup alpha0과 정합)
        }

        void OnEnable() => _showSub = EventBus.Subscribe<ShowExtraCommand>(_ => SetVisible(true));

        void OnDisable()
        {
            _showSub?.Dispose();
            _showSub = null;
            _gate?.Dispose(); // 표시 중 비활성 시 게이트 누수 방지(중복 무해)
            _gate = null;
            _visible = false;
        }

        void SetVisible(bool v)
        {
            if (canvasGroup == null) return;
            if (v)
            {
                transform.SetAsLastSibling();
                _gate?.Dispose();
                _gate = OverlayGate.Push(() => SetVisible(false));
                _visible = true;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                if (slide != null) slide.PlayShow();
                else canvasGroup.alpha = 1f;
            }
            else
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                bool wasVisible = _visible;
                if (wasVisible) { _gate?.Dispose(); _gate = null; _visible = false; }
                if (slide != null) { if (wasVisible) slide.PlayHide(); else slide.ApplyHiddenInstant(); }
                else canvasGroup.alpha = 0f;
            }
        }
    }
}
