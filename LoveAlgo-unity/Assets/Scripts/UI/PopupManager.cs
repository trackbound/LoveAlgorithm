using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using LoveAlgo.Core;
using LoveAlgo.Story;

namespace LoveAlgo.UI
{
    public enum PopupLayer { Modal, Top }

    /// <summary>
    /// 팝업 매니저 - 레이어 기반 팝업 관리
    /// </summary>
    public class PopupManager : SingletonMonoBehaviour<PopupManager>
    {
        [Header("레이어")]
        [SerializeField] Transform layerModal;
        [SerializeField] Transform layerTop;
        [SerializeField] GameObject dimmer;
        [SerializeField] CanvasGroup dimmerCanvasGroup;
        [SerializeField] float dimmerFadeDuration = 0.2f;

        [Header("Top 팝업 — Confirm 프리팹 (이름으로 조회)")]
        [SerializeField] List<ConfirmPopup> confirmPrefabs;

        [Header("Top 팝업 — 기타")]
        [SerializeField] AlertPopup alertPopup;
        [SerializeField] ToastPopup toastPopup;
        [SerializeField] LogPopup logPopup;

        [Header("Modal 팝업 (프리팹)")]
        [SerializeField] List<GameObject> modalPrefabs;

        // Modal 캐시 (Type → Instance)
        readonly Dictionary<Type, GameObject> modalCache = new();

        // Confirm 캐시 (프리팹 이름 → 인스턴스)
        readonly Dictionary<string, ConfirmPopup> confirmCache = new();

        // 현재 열린 Modal
        GameObject currentModal;

        protected override void OnSingletonAwake()
        {
            PreWarmConfirms();
            InitPopups();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (dimmerCanvasGroup != null) dimmerCanvasGroup.DOKill();
        }

        void Update()
        {
            // ESC 키로 팝업 닫기 (새 Input System)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                HandleEscapeKey();
            }
        }

        /// <summary>
        /// ESC 키 처리 - 열린 팝업 순서대로 닫기
        /// </summary>
        void HandleEscapeKey()
        {
            // 1. Top 팝업 우선 (Confirm variants, Alert, Log)
            foreach (var kv in confirmCache)
            {
                if (kv.Value != null && kv.Value.IsVisible)
                {
                    kv.Value.Hide();
                    return;
                }
            }
            if (alertPopup != null && alertPopup.IsVisible)
            {
                alertPopup.Hide();
                return;
            }
            if (logPopup != null && logPopup.IsVisible)
            {
                logPopup.Hide();
                return;
            }

            // 2. Modal 팝업 닫기 (변경사항 확인 후)
            if (currentModal != null)
            {
                TryCloseModalAsync().Forget();
                return;
            }
        }

        /// <summary>
        /// Modal 닫기 시도 (변경사항 확인 포함)
        /// </summary>
        async UniTaskVoid TryCloseModalAsync()
        {
            if (currentModal == null) return;

            var popup = currentModal.GetComponent<ModalPopupBase>();
            if (popup != null)
            {
                bool canClose = await popup.TryCloseAsync();
                if (canClose)
                {
                    CloseModal();
                }
            }
            else
            {
                CloseModal();
            }
        }

        void InitPopups()
        {
            // Confirm 캐시 인스턴스 초기 비활성화
            foreach (var kv in confirmCache)
            {
                if (kv.Value != null) kv.Value.gameObject.SetActive(false);
            }
            alertPopup?.gameObject.SetActive(false);
            toastPopup?.gameObject.SetActive(false);
            logPopup?.gameObject.SetActive(false);
            dimmer?.SetActive(false);

            // Dimmer 클릭 시 Modal 닫기 (디머에 Button 컴포넌트 추가)
            if (dimmer != null)
            {
                var dimBtn = dimmer.GetComponent<Button>();
                if (dimBtn == null)
                    dimBtn = dimmer.AddComponent<Button>();
                dimBtn.transition = Selectable.Transition.None;
                dimBtn.onClick.AddListener(OnDimmerClicked);
            }

            // Modal 프리팩 사전 생성 (Lazy 대신 즉시 캐싱 → 첫 클릭 렉 방지)
            PreWarmModals();
        }

        /// <summary>
        /// Confirm 프리팹을 미리 생성하여 캐시 (이름 → 인스턴스)
        /// </summary>
        void PreWarmConfirms()
        {
            confirmCache.Clear();
            if (confirmPrefabs == null) return;
            foreach (var prefab in confirmPrefabs)
            {
                if (prefab == null) continue;
                string key = prefab.gameObject.name;
                if (confirmCache.ContainsKey(key)) continue;

                var instance = Instantiate(prefab, layerTop);
                instance.gameObject.SetActive(false);
                confirmCache[key] = instance;

                UISoundManager.Instance?.BindButtonsInTransform(instance.transform);
            }
        }

        /// <summary>
        /// Modal 프리팩을 미리 생성하여 캐시 (Instantiate 렉 방지)
        /// </summary>
        void PreWarmModals()
        {
            foreach (var prefab in modalPrefabs)
            {
                if (prefab == null) continue;
                var popup = prefab.GetComponent<ModalPopupBase>();
                if (popup == null) continue;

                var type = popup.GetType();
                if (modalCache.ContainsKey(type)) continue;

                var instance = Instantiate(prefab, layerModal);
                instance.SetActive(false);
                modalCache[type] = instance;

                // 버튼 사운드 사전 바인딩
                UISoundManager.Instance?.BindButtonsInTransform(instance.transform);
            }
        }

        /// <summary>
        /// Dimmer 클릭 처리: Modal만 닫기 (Top 팝업은 닫지 않음)
        /// </summary>
        void OnDimmerClicked()
        {
            // Top 팝업이 열려있으면 디머 클릭 무시 (alert, confirm 등)
            foreach (var kv in confirmCache)
            {
                if (kv.Value != null && kv.Value.IsVisible) return;
            }
            if (alertPopup != null && alertPopup.IsVisible)
                return;

            // Modal 팝업 닫기
            if (currentModal != null)
                TryCloseModalAsync().Forget();
        }

        #region Top 팝업 (즉시 사용)

        /// <summary>
        /// 이름으로 Confirm 프리팹 인스턴스 조회. 없으면 null.
        /// </summary>
        ConfirmPopup GetConfirm(string name)
        {
            confirmCache.TryGetValue(name, out var popup);
            return popup;
        }

        /// <summary>
        /// 첫 번째 Confirm 프리팹 (기본값)
        /// </summary>
        ConfirmPopup GetDefaultConfirm()
        {
            if (confirmPrefabs != null && confirmPrefabs.Count > 0)
                return GetConfirm(confirmPrefabs[0].gameObject.name);
            return null;
        }

        /// <summary>
        /// 확인 팝업 (예/아니오) - 기본 프리팹 사용
        /// </summary>
        public UniTask<bool> ConfirmAsync(string message, string confirmText = null, string cancelText = null)
        {
            var popup = GetDefaultConfirm();
            if (popup == null)
            {
                Debug.LogWarning("[PopupManager] 기본 confirmPopup 프리팹이 없음");
                return UniTask.FromResult(false);
            }
            return popup.ShowAsync(message, confirmText, cancelText);
        }

        /// <summary>
        /// 이름 지정 확인 팝업 — 프리팹 이름으로 조회
        /// </summary>
        public UniTask<bool> ConfirmAsync(string prefabName, ConfirmPopupData data)
        {
            var popup = GetConfirm(prefabName);
            if (popup == null)
            {
                Debug.LogWarning($"[PopupManager] Confirm 프리팹 없음: {prefabName}");
                return UniTask.FromResult(false);
            }
            return popup.ShowAsync(data);
        }

        /// <summary>
        /// 확인 팝업 (예/아니오) - 콜백 버전
        /// </summary>
        public void Confirm(string message, Action onConfirm, Action onCancel)
        {
            var popup = GetDefaultConfirm();
            if (popup == null)
            {
                Debug.LogWarning("[PopupManager] 기본 confirmPopup 프리팹이 없음");
                onCancel?.Invoke();
                return;
            }
            popup.Show(message, onConfirm, onCancel);
        }

        /// <summary>
        /// 알림 팝업 (확인만)
        /// </summary>
        public UniTask AlertAsync(string message)
        {
            if (alertPopup == null)
            {
                Debug.LogWarning("[PopupManager] alertPopup이 바인딩되지 않음");
                return UniTask.CompletedTask;
            }
            return alertPopup.ShowAsync(message);
        }

        /// <summary>
        /// 토스트 메시지 (자동 사라짐)
        /// </summary>
        public void Toast(string title, string message, float duration = 2f)
        {
            if (toastPopup == null)
            {
                Debug.LogWarning("[PopupManager] toastPopup이 바인딩되지 않음");
                return;
            }
            toastPopup.Show(title, message, duration);
        }

        #endregion

        #region Modal 팝업 (Lazy 생성)

        /// <summary>
        /// Modal 팝업 표시 (Lazy 생성 + 캐시)
        /// </summary>
        public T ShowModal<T>() where T : ModalPopupBase
        {
            var type = typeof(T);

            // 캐시 확인
            if (!modalCache.TryGetValue(type, out var instance))
            {
                var prefab = FindModalPrefab<T>();
                if (prefab == null)
                {
                    Debug.LogError($"[PopupManager] Modal 프리팹 없음: {type.Name}");
                    return null;
                }

                instance = Instantiate(prefab, layerModal);
                modalCache[type] = instance;
            }

            // 이전 Modal 숨김
            if (currentModal != null && currentModal != instance)
            {
                var prevPopup = currentModal.GetComponent<ModalPopupBase>();
                if (prevPopup != null)
                    prevPopup.Hide();
                else
                    currentModal.SetActive(false);
            }

            currentModal = instance;
            ShowDimmer(true);

            // Show() 메서드 호출 (슬라이딩 애니메이션 실행)
            var popup = instance.GetComponent<T>();
            popup?.Show();

            return popup;
        }

        /// <summary>
        /// 현재 Modal 닫기 (애니메이션 완료 후 상태 해제)
        /// </summary>
        public void CloseModal()
        {
            CloseModalInternal().Forget();
        }

        async UniTaskVoid CloseModalInternal()
        {
            if (currentModal != null)
            {
                // Modal 위에 떠 있는 ConfirmPopup이 있으면 먼저 닫기
                DismissActiveConfirmPopups();

                var popup = currentModal.GetComponent<ModalPopupBase>();
                ShowDimmer(false);

                if (popup != null)
                    await popup.HideAsync();
                else
                    currentModal.SetActive(false);

                currentModal = null;
            }
        }

        /// <summary>
        /// 현재 Modal 닫기 (애니메이션 완료까지 대기)
        /// </summary>
        public async UniTask CloseModalAsync()
        {
            if (currentModal != null)
            {
                // Modal 위에 떠 있는 ConfirmPopup이 있으면 먼저 닫기
                DismissActiveConfirmPopups();

                var popup = currentModal.GetComponent<ModalPopupBase>();
                ShowDimmer(false);  // 디머와 패널 동시 페이드

                if (popup != null)
                    await popup.HideAsync();
                else
                    currentModal.SetActive(false);

                currentModal = null;
            }
        }

        /// <summary>
        /// 활성 상태인 Confirm/ScheduleConfirm 팝업을 모두 닫기
        /// Hide()가 tcs.TrySetResult(false)를 호출하므로 대기 중인 UniTask도 자동 완료됨
        /// </summary>
        void DismissActiveConfirmPopups()
        {
            foreach (var kv in confirmCache)
            {
                if (kv.Value != null && kv.Value.IsVisible)
                    kv.Value.Hide();
            }
        }

        GameObject FindModalPrefab<T>() where T : ModalPopupBase
        {
            foreach (var prefab in modalPrefabs)
            {
                if (prefab != null && prefab.GetComponent<T>() != null)
                    return prefab;
            }
            return null;
        }

        #endregion

        #region 편의 메서드

        /// <summary>
        /// 세이브 팝업 열기
        /// </summary>
        public void ShowSavePopup(System.Action<int> onSlotSelected = null)
        {
            var popup = ShowModal<SaveLoadPopup>();
            popup?.ShowSave(onSlotSelected);
        }

        /// <summary>
        /// 로드 팝업 열기
        /// </summary>
        public void ShowLoadPopup(System.Action<int> onSlotSelected = null)
        {
            var popup = ShowModal<SaveLoadPopup>();
            popup?.ShowLoad(onSlotSelected);
        }

        /// <summary>
        /// 설정 팝업 열기
        /// </summary>
        public void ShowSettings()
        {
            ShowModal<SettingsPopup>();
        }

        public async void ShowSave()
        {
            // 팝업이 뜨기 전에 게임 화면 캡처 (1프레임 대기하여 팝업이 찍히는 것 방지)
            await Story.SaveManager.CapturePendingScreenshotAsync();
            ShowModal<SaveLoadPopup>()?.ShowSave(slot =>
            {
                GameManager.Instance?.Save(slot);
                UISoundManager.Instance?.PlaySaveComplete();
                Toast("저장 완료", $"슬롯 {slot}에 저장했습니다.");
            });
        }

        public void ShowLoad()
        {
            ShowModal<SaveLoadPopup>()?.ShowLoad(slot => 
            {
                GameManager.Instance?.LoadGame(slot);
                UISoundManager.Instance?.PlayLoadComplete();
                Toast("로드 완료", $"슬롯 {slot}에서 불러왔습니다.");
            });
        }

        public void ShowLog(IReadOnlyList<DialogueLogEntry> log)
        {
            if (logPopup == null)
            {
                Debug.LogError("[PopupManager] logPopup이 바인딩되지 않음 - Inspector에서 할당 필요");
                return;
            }
            logPopup.Show(log);
        }

        #endregion

        #region Dimmer

        void ShowDimmer(bool show)
        {
            if (dimmer == null) return;

            // 기존 dimmer 트윈 정리 (DOTween.KillAll() 등으로 OnComplete 누락 방지)
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
                        .OnKill(() => { if (dimmer != null) dimmer.SetActive(false); });
                }
                else
                {
                    dimmer.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Top 레이어 팝업용 Dimmer (외부 호출용)
        /// </summary>
        public void ShowTopDimmer(bool show)
        {
            ShowDimmer(show);
        }

        #endregion

        #region 유틸

        /// <summary>
        /// 모든 팝업 닫기
        /// </summary>
        public void CloseAll()
        {
            CloseModal();
            DismissActiveConfirmPopups();
            alertPopup?.Hide();
        }

        /// <summary>
        /// 팝업이 열려있는지
        /// </summary>
        public bool IsAnyPopupOpen
        {
            get
            {
                if (currentModal != null) return true;
                foreach (var kv in confirmCache)
                {
                    if (kv.Value != null && kv.Value.IsVisible) return true;
                }
                return (alertPopup != null && alertPopup.IsVisible) ||
                       (logPopup != null && logPopup.IsVisible);
            }
        }

        #endregion
    }
}
