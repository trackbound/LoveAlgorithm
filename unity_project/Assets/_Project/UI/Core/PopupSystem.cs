using LoveAlgo.Contracts;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using LoveAlgo.Common;
using LoveAlgo.Core;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 통합 팝업 매니저.
    /// - 모든 팝업은 <see cref="PopupBase"/>의 자식. Type 기반 registry.
    /// - <see cref="PopupBase.Layer"/>로 Modal/Dialog/Notification 구분.
    /// - 베이스가 Show/Hide 시 <see cref="NotifyOpened"/>/<see cref="NotifyClosed"/>를 호출 → openStack 관리.
    /// - Dim은 각 popup prefab 자체에 포함 (중앙 dimmer 없음).
    /// </summary>
    public class PopupSystem : MonoBehaviour
    {
        public static PopupSystem Instance { get; private set; }

        [Header("Layer Roots (비워두면 자동 생성)")]
        [SerializeField] Transform layerModal;
        [SerializeField] Transform layerDialog;
        [SerializeField] Transform layerNotification;

        [Header("팝업 프리팹 (모든 PopupBase 인스턴스)")]
        [Tooltip("Layer에 따라 자동으로 layerModal/layerNotification 아래에 생성됨")]
        [SerializeField] List<PopupBase> popupPrefabs;

        // Type → Instance 캐시 (PreWarm + Lazy)
        readonly Dictionary<Type, PopupBase> cache = new();

        // 현재 열린 팝업 스택 (가장 위 = 끝)
        readonly List<PopupBase> openStack = new();

        void Awake()
        {
            Instance = this;
            EnsureLayerRoots();
            PreWarm();
        }

        void EnsureLayerRoots()
        {
            if (layerModal == null) layerModal = CreateLayerRoot("Modal", 0);
            if (layerDialog == null) layerDialog = CreateLayerRoot("Dialog", 1);
            if (layerNotification == null) layerNotification = CreateLayerRoot("Notification", 2);
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

            var parent = prefab.Layer switch
            {
                PopupLayer.Notification => layerNotification,
                PopupLayer.Dialog       => layerDialog,
                _                        => layerModal,
            };
            var instance = Instantiate(prefab, parent);
            instance.name = prefab.name; // (Clone) 제거
            instance.gameObject.SetActive(false);
            cache[type] = instance;
            LoveAlgo.Modules.Audio.AudioManager.Instance?.BindButtonsInTransform(instance.transform);
            return instance;
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
            Debug.LogError($"[PopupSystem] 팝업 프리팹 미등록: {type.Name}");
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
        // PopupSystem은 공용 인프라(Alert/Confirm/Toast)만 유지.

        // ── Stack / Dimmer 관리 (PopupBase가 호출) ───────────────

        internal void NotifyOpened(PopupBase popup)
        {
            if (popup == null) return;
            openStack.Remove(popup); // 중복 방지
            openStack.Add(popup);
        }

        internal void NotifyClosed(PopupBase popup)
        {
            if (popup == null) return;
            openStack.Remove(popup);
        }

        // ── ESC ─────────────────────────────────────────────

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

        PopupBase PeekTop()
        {
            for (int i = openStack.Count - 1; i >= 0; i--)
            {
                if (openStack[i] != null && openStack[i].IsVisible) return openStack[i];
            }
            return null;
        }

        // ── 유틸 ───────────────────────────────────────────

        public bool IsAnyPopupOpen => openStack.Count > 0;

        /// <summary>일반 닫기 — 각 popup이 자기 fade 트윈을 진행 후 비활성화.
        /// UI 흐름 안에서 정상 종료 시 사용. 트윈이 끝나기 전에 호출자가 다음 작업을 하면
        /// popup이 잠시 보일 수 있음 — 점프/씬 전환에는 <see cref="CloseAllImmediate"/> 사용.</summary>
        public void CloseAll()
        {
            var snapshot = openStack.ToArray();
            for (int i = snapshot.Length - 1; i >= 0; i--)
                snapshot[i]?.Hide();
        }

        /// <summary>
        /// 외부 강제 종료 — 모든 popup의 fade 트윈을 죽이고 즉시 비활성화.
        /// GameFlowJumper.TearDownEverythingAsync 등 화면 전환 직전에 사용.
        /// </summary>
        public void CloseAllImmediate()
        {
            var snapshot = openStack.ToArray();
            for (int i = snapshot.Length - 1; i >= 0; i--)
                snapshot[i]?.ForceHideImmediate();
            openStack.Clear();  // ForceHideImmediate가 NotifyClosed로 비우긴 하지만 안전망
        }
    }
}
