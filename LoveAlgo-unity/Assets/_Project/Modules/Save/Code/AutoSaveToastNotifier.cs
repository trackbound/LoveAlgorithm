using LoveAlgo.Common;
using LoveAlgo.Contracts;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.Save
{
    /// <summary>
    /// AutoSaveCompletedEvent를 받아 "자동 저장됨" 토스트를 살짝 띄우는 알리미.
    /// PlayerPrefs 토글로 끌 수 있음 (기본 ON).
    ///
    /// AfterSceneLoad에서 자동 부트스트랩 — 씬 배치 불필요.
    /// Headless 환경에선 부트스트랩 자체를 건너뜀.
    /// 명시적 사용자 액션에서 비롯된 저장(예: 매뉴얼 저장)에는 안 뜸 (이벤트는 AutoSaveAsync만 발행).
    /// </summary>
    public class AutoSaveToastNotifier : MonoBehaviour
    {
        /// <summary>토글 키 — 설정 메뉴에서 노출 시 동일 키 참조.</summary>
        public const string EnabledPlayerPref = "AutoSaveToast.Enabled";

        /// <summary>토스트 표시 시간 — 다른 토스트보다 짧게 (눈에 띄지만 거슬리지 않도록).</summary>
        const float ToastDuration = 1.4f;

        /// <summary>토글 — 옵션 메뉴/디버그에서 호출. 기본 true.</summary>
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledPlayerPref, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(EnabledPlayerPref, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Headless.IsEnabled) return;

            var go = new GameObject("[AutoSaveToastNotifier]");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoSaveToastNotifier>();
        }

        void Awake()
        {
            this.SubscribeOnDestroy<AutoSaveCompletedEvent>(OnAutoSaveCompleted);
        }

        void OnAutoSaveCompleted(AutoSaveCompletedEvent _)
        {
            if (!Enabled) return;
            if (PopupManager.Instance == null) return;

            PopupManager.Instance.Toast("자동 저장", "저장됨", ToastDuration);
        }
    }
}
