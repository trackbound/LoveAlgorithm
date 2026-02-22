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
        public struct ThumbnailPopupState
        {
            public bool LayerModalActive;
            public bool LayerTopActive;
            public bool DimmerActive;
            public bool ConfirmActive;
            public bool ScheduleConfirmActive;
            public bool AlertActive;
            public bool ToastActive;
            public bool LogActive;
        }

        [Header("레이어")]
        [SerializeField] Transform layerModal;
        [SerializeField] Transform layerTop;
        [SerializeField] GameObject dimmer;  // 배경 어둡게 (선택)
        [SerializeField] CanvasGroup dimmerCanvasGroup;  // 딤 페이드용
        [SerializeField] float dimmerFadeDuration = 0.2f;

        [Header("Top 팝업 (인스턴스 바인딩)")]
        [SerializeField] ConfirmPopup confirmPopup;
        [SerializeField] ConfirmPopup scheduleConfirmPopup;  // 스케줄용 (다른 디자인)
        [SerializeField] AlertPopup alertPopup;
        [SerializeField] ToastPopup toastPopup;
        [SerializeField] LogPopup logPopup;

        [Header("Modal 팝업 (프리팹)")]
        [SerializeField] List<GameObject> modalPrefabs;

        // Modal 캐시 (Type → Instance)
        readonly Dictionary<Type, GameObject> modalCache = new();

        // 현재 열린 Modal
        GameObject currentModal;

        protected override void OnSingletonAwake()
        {
            InitPopups();
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
            // 1. Top 팝업 우선 (Confirm, Alert, Log)
            if (confirmPopup != null && confirmPopup.IsVisible)
            {
                confirmPopup.Hide();
                return;
            }
            if (scheduleConfirmPopup != null && scheduleConfirmPopup.IsVisible)
            {
                scheduleConfirmPopup.Hide();
                return;
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
            // Top 팝업들 초기 비활성화
            confirmPopup?.gameObject.SetActive(false);
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
            if ((confirmPopup != null && confirmPopup.IsVisible) ||
                (scheduleConfirmPopup != null && scheduleConfirmPopup.IsVisible) ||
                (alertPopup != null && alertPopup.IsVisible))
                return;

            // Modal 팝업 닫기
            if (currentModal != null)
                TryCloseModalAsync().Forget();
        }

        #region Top 팝업 (즉시 사용)

        /// <summary>
        /// 확인 팝업 (예/아니오) - Async 버전
        /// </summary>
        public UniTask<bool> ConfirmAsync(string message, string confirmText = null, string cancelText = null)
        {
            if (confirmPopup == null)
            {
                Debug.LogWarning("[PopupManager] confirmPopup이 바인딩되지 않음");
                return UniTask.FromResult(false);
            }
            return confirmPopup.ShowAsync(message, sub: null, confirmText: confirmText, cancelText: cancelText);
        }

        /// <summary>
        /// 확인 팝업 (예/아니오) - 콜백 버전
        /// </summary>
        public void Confirm(string message, Action onConfirm, Action onCancel)
        {
            if (confirmPopup == null)
            {
                Debug.LogWarning("[PopupManager] confirmPopup이 바인딩되지 않음");
                onCancel?.Invoke();
                return;
            }
            confirmPopup.Show(message, onConfirm, onCancel);
        }

        /// <summary>
        /// 스케줄 확인 팝업 (메시지 + 효과 텍스트)
        /// </summary>
        public UniTask<bool> ScheduleConfirmAsync(string message, string effect)
        {
            if (scheduleConfirmPopup == null)
            {
                Debug.LogWarning("[PopupManager] scheduleConfirmPopup이 바인딩되지 않음");
                return UniTask.FromResult(false);
            }
            return scheduleConfirmPopup.ShowAsync(message, sub: effect);
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
        /// 현재 Modal 닫기
        /// </summary>
        public void CloseModal()
        {
            if (currentModal != null)
            {
                // Hide() 메서드 호출 (슬라이딩 애니메이션 실행)
                var popup = currentModal.GetComponent<ModalPopupBase>();
                if (popup != null)
                    popup.Hide();
                else
                    currentModal.SetActive(false);
                    
                currentModal = null;
                ShowDimmer(false);
            }
        }

        /// <summary>
        /// 현재 Modal 닫기 (애니메이션 완료까지 대기)
        /// </summary>
        public async UniTask CloseModalAsync()
        {
            if (currentModal != null)
            {
                var popup = currentModal.GetComponent<ModalPopupBase>();
                ShowDimmer(false);  // 디머와 패널 동시 페이드

                if (popup != null)
                    await popup.HideAsync();
                else
                    currentModal.SetActive(false);

                currentModal = null;
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

        public void ShowSave()
        {
            // 팝업이 뜨기 전에 게임 화면 캡처 (팝업이 찍히는 것 방지)
            Story.SaveManager.CapturePendingScreenshot();
            ShowModal<SaveLoadPopup>()?.ShowSave(slot =>
            {
                GameManager.Instance?.Save(slot);
                Toast("저장 완료", $"슬롯 {slot}에 저장했습니다.");
            });
        }

        public void ShowLoad()
        {
            ShowModal<SaveLoadPopup>()?.ShowLoad(slot => 
            {
                GameManager.Instance?.LoadGame(slot);
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
        /// 썸네일 캡처 시 팝업 UI를 제외하기 위해 현재 상태를 저장하고 숨김
        /// </summary>
        public ThumbnailPopupState HideForThumbnailCapture()
        {
            var state = new ThumbnailPopupState
            {
                LayerModalActive = layerModal != null && layerModal.gameObject.activeSelf,
                LayerTopActive = layerTop != null && layerTop.gameObject.activeSelf,
                DimmerActive = dimmer != null && dimmer.activeSelf,
                ConfirmActive = confirmPopup != null && confirmPopup.gameObject.activeSelf,
                ScheduleConfirmActive = scheduleConfirmPopup != null && scheduleConfirmPopup.gameObject.activeSelf,
                AlertActive = alertPopup != null && alertPopup.gameObject.activeSelf,
                ToastActive = toastPopup != null && toastPopup.gameObject.activeSelf,
                LogActive = logPopup != null && logPopup.gameObject.activeSelf
            };

            if (confirmPopup != null) confirmPopup.gameObject.SetActive(false);
            if (scheduleConfirmPopup != null) scheduleConfirmPopup.gameObject.SetActive(false);
            if (alertPopup != null) alertPopup.gameObject.SetActive(false);
            if (toastPopup != null) toastPopup.gameObject.SetActive(false);
            if (logPopup != null) logPopup.gameObject.SetActive(false);
            if (layerTop != null) layerTop.gameObject.SetActive(false);
            if (layerModal != null) layerModal.gameObject.SetActive(false);
            if (dimmer != null) dimmer.SetActive(false);

            return state;
        }

        /// <summary>
        /// 썸네일 캡처 후 팝업 UI 상태 복원
        /// </summary>
        public void RestoreAfterThumbnailCapture(ThumbnailPopupState state)
        {
            if (layerModal != null) layerModal.gameObject.SetActive(state.LayerModalActive);
            if (layerTop != null) layerTop.gameObject.SetActive(state.LayerTopActive);
            if (dimmer != null) dimmer.SetActive(state.DimmerActive);

            if (confirmPopup != null) confirmPopup.gameObject.SetActive(state.ConfirmActive);
            if (scheduleConfirmPopup != null) scheduleConfirmPopup.gameObject.SetActive(state.ScheduleConfirmActive);
            if (alertPopup != null) alertPopup.gameObject.SetActive(state.AlertActive);
            if (toastPopup != null) toastPopup.gameObject.SetActive(state.ToastActive);
            if (logPopup != null) logPopup.gameObject.SetActive(state.LogActive);
        }

        /// <summary>
        /// 모든 팝업 닫기
        /// </summary>
        public void CloseAll()
        {
            CloseModal();
            confirmPopup?.Hide();
            alertPopup?.Hide();
        }

        /// <summary>
        /// 팝업이 열려있는지
        /// </summary>
        public bool IsAnyPopupOpen => 
            currentModal != null || 
            (confirmPopup != null && confirmPopup.IsVisible) ||
            (alertPopup != null && alertPopup.IsVisible) ||
            (logPopup != null && logPopup.IsVisible);

        #endregion
    }
}
