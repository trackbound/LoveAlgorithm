using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using LoveAlgo.Common;
using LoveAlgo.Core;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 통합 팝업 매니저.
    /// - 모든 팝업은 <see cref="PopupBase"/>의 자식. Type 기반 registry.
    /// - <see cref="PopupBase.Layer"/>로 Modal/Top 구분 (Modal 위에 Top 가능).
    /// - 베이스가 Show/Hide 시 <see cref="NotifyOpened"/>/<see cref="NotifyClosed"/>를 호출 → openStack 관리.
    /// - ESC / Dimmer 클릭 / dimmer 표시 정책 모두 Stack 기준으로 자동.
    /// </summary>
    public class PopupManager : SingletonMonoBehaviour<PopupManager>
    {
        [Header("Layer Roots (비워두면 자동 생성)")]
        [SerializeField] Transform layerModal;
        [SerializeField] Transform layerNotification;

        [Header("Dimmer")]
        [SerializeField] GameObject dimmer;
        [SerializeField] CanvasGroup dimmerCanvasGroup;
        [SerializeField] float dimmerFadeDuration = 0.2f;

        [Header("팝업 프리팹 (모든 PopupBase 인스턴스)")]
        [Tooltip("Layer에 따라 자동으로 layerModal/layerNotification 아래에 생성됨")]
        [SerializeField] List<PopupBase> popupPrefabs;

        // Type → Instance 캐시 (PreWarm + Lazy)
        readonly Dictionary<Type, PopupBase> cache = new();

        // 현재 열린 팝업 스택 (가장 위 = 끝)
        readonly List<PopupBase> openStack = new();

        protected override void OnSingletonAwake()
        {
            EnsureLayerRoots();
            InitDimmer();
            PreWarm();
        }

        void EnsureLayerRoots()
        {
            if (layerModal == null) layerModal = CreateLayerRoot("Modal", 0);
            if (layerNotification == null) layerNotification = CreateLayerRoot("Notification", 1);
        }

        Transform CreateLayerRoot(string name, int siblingIndex)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetSiblingIndex(siblingIndex);
            return rt;
        }

        void InitDimmer()
        {
            if (dimmer == null) return;
            dimmer.SetActive(false);

            var btn = dimmer.GetComponent<Button>();
            if (btn == null) btn = dimmer.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(OnDimmerClicked);
        }

        /// <summary>등록된 모든 프리팹 인스턴스 사전 생성 (첫 표시 시 렉 방지).</summary>
        void PreWarm()
        {
            if (popupPrefabs == null) return;
            foreach (var prefab in popupPrefabs)
            {
                if (prefab == null) continue;
                Materialize(prefab);
            }
        }

        PopupBase Materialize(PopupBase prefab)
        {
            var type = prefab.GetType();
            if (cache.TryGetValue(type, out var existing)) return existing;

            var parent = prefab.Layer == PopupLayer.Notification ? layerNotification : layerModal;
            var instance = Instantiate(prefab, parent);
            instance.name = prefab.name; // (Clone) 제거
            instance.gameObject.SetActive(false);
            cache[type] = instance;
            UISoundManager.Instance?.BindButtonsInTransform(instance.transform);
            return instance;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (dimmerCanvasGroup != null) dimmerCanvasGroup.DOKill();
        }

        // ── 공개 API ──────────────────────────────────────────

        /// <summary>Type으로 팝업 인스턴스 조회 (없으면 popupPrefabs에서 찾아 생성).</summary>
        public T Get<T>() where T : PopupBase
        {
            var type = typeof(T);
            if (cache.TryGetValue(type, out var existing)) return existing as T;

            if (popupPrefabs != null)
            {
                foreach (var prefab in popupPrefabs)
                {
                    if (prefab is T)
                        return Materialize(prefab) as T;
                }
            }
            Debug.LogError($"[PopupManager] 팝업 프리팹 미등록: {type.Name}");
            return null;
        }

        /// <summary>팝업 표시 후 인스턴스 반환. 추가 설정은 호출자가 진행.</summary>
        public T Show<T>() where T : PopupBase
        {
            var popup = Get<T>();
            popup?.Show();
            return popup;
        }

        /// <summary>
        /// 외부 모듈이 자기 팝업 프리팹을 등록 (모듈 응집 패턴).
        /// 모듈 Awake에서 호출 권장: PopupManager.Instance.Register(myPopupPrefab).
        /// 반환값은 생성된 인스턴스 — 모듈이 직접 참조 캐싱하려면 사용.
        /// </summary>
        public T Register<T>(T prefab) where T : PopupBase
        {
            if (prefab == null) return null;
            return Materialize(prefab) as T;
        }

        // ── 도메인 헬퍼 (시그니처 유지, 내부는 Generic Show) ────────

        public UniTask AlertAsync(string message, string confirmText = null)
        {
            var p = Get<AlertPopup>();
            return p != null ? p.ShowAsync(message, confirmText) : UniTask.CompletedTask;
        }

        public UniTask<bool> ConfirmAsync(string message, string confirmText = null, string cancelText = null)
        {
            var p = Get<ConfirmPopup>();
            return p != null ? p.ShowAsync(message, confirmText, cancelText) : UniTask.FromResult(false);
        }

        public UniTask<bool> ConfirmAsync(ConfirmPopupData data)
        {
            var p = Get<ConfirmPopup>();
            return p != null ? p.ShowAsync(data) : UniTask.FromResult(false);
        }

        /// <summary>콜백 버전 (await 안 하고 처리).</summary>
        public void Confirm(string message, Action onConfirm, Action onCancel = null)
            => Get<ConfirmPopup>()?.Show(message, onConfirm, onCancel);

        public void Toast(string title, string message, float duration = 2f)
            => Get<ToastNotification>()?.Show(title, message, duration);

        public void ToastSequence(string title, List<string> messages, float holdPerItem = 0.8f)
            => Get<ToastNotification>()?.ShowSequence(title, messages, holdPerItem);

        // 도메인 진입점은 각 모듈로 이전됨 (ISave.ShowSaveUI/ShowLoadUI, ISettings.ShowSettingsUI, INarrative.ShowLogUI).
        // PopupManager는 공용 인프라(Alert/Confirm/Toast)만 유지.

        // ── Stack / Dimmer 관리 (PopupBase가 호출) ───────────────

        internal void NotifyOpened(PopupBase popup)
        {
            if (popup == null) return;
            openStack.Remove(popup); // 중복 방지
            openStack.Add(popup);
            UpdateDimmer();
        }

        internal void NotifyClosed(PopupBase popup)
        {
            if (popup == null) return;
            openStack.Remove(popup);
            UpdateDimmer();
        }

        /// <summary>openStack에 useDimmer=true인 팝업이 있으면 dimmer ON, 아니면 OFF.</summary>
        void UpdateDimmer()
        {
            bool needDimmer = false;
            for (int i = 0; i < openStack.Count; i++)
            {
                if (openStack[i] != null && openStack[i].UseDimmer) { needDimmer = true; break; }
            }
            ShowDimmer(needDimmer);
        }

        // ── ESC / Dimmer 클릭 ────────────────────────────────

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                HandleEscape();
        }

        void HandleEscape()
        {
            var top = PeekTop();
            top?.Close();
        }

        void OnDimmerClicked()
        {
            // 최상위가 Modal이면 닫기 시도 (Top 팝업은 디머 클릭 시 닫지 않음 — 기존 정책 유지)
            var top = PeekTop();
            if (top != null && top.Layer == PopupLayer.Modal) top.Close();
        }

        PopupBase PeekTop()
        {
            for (int i = openStack.Count - 1; i >= 0; i--)
            {
                if (openStack[i] != null && openStack[i].IsVisible) return openStack[i];
            }
            return null;
        }

        // ── Dimmer 애니메이션 ────────────────────────────────

        void ShowDimmer(bool show)
        {
            if (dimmer == null) return;
            dimmerCanvasGroup?.DOKill();

            if (show)
            {
                dimmer.SetActive(true);
                if (dimmerCanvasGroup != null)
                {
                    dimmerCanvasGroup.alpha = 0f;
                    dimmerCanvasGroup.DOFade(1f, dimmerFadeDuration).SetEase(Ease.OutQuad);
                }
            }
            else
            {
                if (dimmerCanvasGroup != null)
                {
                    dimmerCanvasGroup.DOFade(0f, dimmerFadeDuration)
                        .SetEase(Ease.InQuad)
                        .OnKill(() => { if (dimmer != null && !IsAnyDimmerPopupOpen()) dimmer.SetActive(false); });
                }
                else
                {
                    dimmer.SetActive(false);
                }
            }
        }

        bool IsAnyDimmerPopupOpen()
        {
            for (int i = 0; i < openStack.Count; i++)
                if (openStack[i] != null && openStack[i].UseDimmer && openStack[i].IsVisible) return true;
            return false;
        }

        // ── 유틸 ───────────────────────────────────────────

        public bool IsAnyPopupOpen => openStack.Count > 0;

        public void CloseAll()
        {
            var snapshot = openStack.ToArray();
            for (int i = snapshot.Length - 1; i >= 0; i--)
                snapshot[i]?.Hide();
        }
    }
}
