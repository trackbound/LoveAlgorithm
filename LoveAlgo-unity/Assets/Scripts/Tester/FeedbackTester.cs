using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using LoveAlgo.Core;

namespace LoveAlgo.Tester
{
    /// <summary>
    /// FeedbackManager 테스트용 UI
    /// F12 키로 토글
    /// </summary>
    public class FeedbackTester : MonoBehaviour
    {
        [Header("테스트 설정")]
        [SerializeField] bool showGUI = true;
        
        Camera cachedCamera;
        Vector3 lastCamPos;

        void Update()
        {
            // F12로 토글
            if (Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame)
            {
                showGUI = !showGUI;
            }
            
            // 카메라 위치 변화 감지
            if (cachedCamera != null)
            {
                var currentPos = cachedCamera.transform.localPosition;
                if (currentPos != lastCamPos)
                {
                    Debug.Log($"[FeedbackTester] Camera moved! {lastCamPos} → {currentPos}");
                    lastCamPos = currentPos;
                }
            }
        }

        void OnGUI()
        {
            if (!showGUI) return;

            GUILayout.BeginArea(new Rect(10, 10, 250, 400));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== Feedback Tester ===");
            GUILayout.Label("Press F12 to toggle");
            GUILayout.Space(10);

            // FeedbackManager 상태
            var fm = FeedbackManager.Instance;
            if (fm == null)
            {
                GUILayout.Label("❌ FeedbackManager not found!");
                GUILayout.EndVertical();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label("✅ FeedbackManager found");
            GUILayout.Space(5);

            // 등록된 피드백 버튼들
            GUILayout.Label("[ Registered Feedbacks ]");
            
            if (GUILayout.Button("▶ ShakeLight"))
            {
                TestFeedback("ShakeLight");
            }
            
            if (GUILayout.Button("▶ FlashWhite"))
            {
                TestFeedback("FlashWhite");
            }
            
            if (GUILayout.Button("▶ SlowMo"))
            {
                TestFeedback("SlowMo");
            }
            
            if (GUILayout.Button("▶ UIPopupIn"))
            {
                TestFeedback("UIPopupIn");
            }

            GUILayout.Space(10);
            GUILayout.Label("[ ScreenFX Direct ]");

            if (GUILayout.Button("▶ CamShake (DOTween)"))
            {
                TestScreenFXCamShake();
            }

            if (GUILayout.Button("▶ Flash"))
            {
                TestScreenFXFlash();
            }

            GUILayout.Space(10);
            GUILayout.Label("[ Debug Info ]");
            
            var screenFX = ScreenFX.Instance;
            if (screenFX != null)
            {
                GUILayout.Label("✅ ScreenFX found");
                
                // 리플렉션으로 바인딩 상태 확인
                var type = screenFX.GetType();
                
                var camField = type.GetField("stageCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var cam = camField?.GetValue(screenFX) as Camera;
                GUILayout.Label($"  stageCamera: {(cam != null ? cam.name : "NULL")}");
                
                // 카메라 캐시
                if (cam != null && cachedCamera != cam)
                {
                    cachedCamera = cam;
                    lastCamPos = cam.transform.localPosition;
                }
                
                // 카메라 위치 표시
                if (cam != null)
                {
                    var pos = cam.transform.localPosition;
                    GUILayout.Label($"  camPos: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");
                }
                
                var rtField = type.GetField("stageTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var rt = rtField?.GetValue(screenFX) as RectTransform;
                GUILayout.Label($"  stageTransform: {(rt != null ? rt.name : "NULL")}");
                
                var feedbackField = type.GetField("cameraShakeFeedback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var feedback = feedbackField?.GetValue(screenFX);
                GUILayout.Label($"  cameraShakeFeedback: {(feedback != null ? "SET" : "NULL")}");
            }
            else
            {
                GUILayout.Label("❌ ScreenFX not found");
            }
            
            // 강한 shake 테스트
            GUILayout.Space(10);
            if (GUILayout.Button("▶ STRONG Shake (DOTween)"))
            {
                TestStrongShake();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        void TestFeedback(string name)
        {
            var fm = FeedbackManager.Instance;
            if (fm == null)
            {
                Debug.LogError("[FeedbackTester] FeedbackManager.Instance is null!");
                return;
            }

            bool hasFeedback = fm.HasFeedback(name);
            Debug.Log($"[FeedbackTester] HasFeedback('{name}'): {hasFeedback}");

            if (hasFeedback)
            {
                bool played = fm.TryPlay(name);
                Debug.Log($"[FeedbackTester] TryPlay('{name}'): {played}");
            }
            else
            {
                Debug.LogWarning($"[FeedbackTester] Feedback '{name}' not registered!");
            }
        }

        async void TestScreenFXCamShake()
        {
            var fx = ScreenFX.Instance;
            if (fx == null)
            {
                Debug.LogError("[FeedbackTester] ScreenFX.Instance is null!");
                return;
            }

            Debug.Log("[FeedbackTester] Calling ScreenFX.CamShakeAsync(0.5, 20)...");
            await fx.CamShakeAsync(0.5f, 20f);
            Debug.Log("[FeedbackTester] CamShakeAsync completed.");
        }

        async void TestScreenFXFlash()
        {
            var fx = ScreenFX.Instance;
            if (fx == null)
            {
                Debug.LogError("[FeedbackTester] ScreenFX.Instance is null!");
                return;
            }

            Debug.Log("[FeedbackTester] Calling ScreenFX.FlashAsync(0.2)...");
            await fx.FlashAsync(0.2f);
            Debug.Log("[FeedbackTester] FlashAsync completed.");
        }
        
        async void TestStrongShake()
        {
            if (cachedCamera == null)
            {
                Debug.LogError("[FeedbackTester] No camera cached!");
                return;
            }
            
            Debug.Log($"[FeedbackTester] STRONG shake starting. Camera: {cachedCamera.name}, pos: {cachedCamera.transform.localPosition}");
            
            // DOTween으로 직접 강하게 흔들기
            var originalPos = cachedCamera.transform.localPosition;
            await cachedCamera.transform
                .DOShakePosition(1f, 2f, 30, 90, false, true)  // strength 2.0 (매우 큼)
                .ToUniTask();
            
            cachedCamera.transform.localPosition = originalPos;
            Debug.Log($"[FeedbackTester] STRONG shake completed. pos: {cachedCamera.transform.localPosition}");
        }
    }
}
