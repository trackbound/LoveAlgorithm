using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using LoveAlgo.LockScreen;
using LoveAlgo.LockScreen.UI;
using UnityEngine;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// CSV에서 PC잠금 화면을 호출하는 Flow 명령.
    /// 기획서 §진입 정보: "스크립트 시트에서 필요한 타이밍에 따로 표기".
    ///
    /// 형식:
    ///   LockScreen:OpenFirstSetup                  비번 첫 설정 (이름 입력 직후 호출)
    ///   LockScreen:OpenNormal                      평상 잠금화면 (재로그인 연출)
    ///   LockScreen:OpenNormal:Time=23:58           시계 1회 오버라이드
    ///
    /// 동작: LockScreenPanel 표시 → OnFlowComplete까지 await → 스토리 진행 복귀.
    ///
    /// 예시 CSV:
    ///   ,Flow,,LockScreen:OpenFirstSetup,>
    ///   ,Flow,,LockScreen:OpenNormal:Time=07:30,>
    /// </summary>
    public static class LockScreenFlowCommand
    {
        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            if (parts.Length < 2)
            {
                Debug.LogWarning("[Flow][LockScreen] 인자 부족 — LockScreen:OpenFirstSetup | LockScreen:OpenNormal[:Time=HH:mm]");
                return;
            }

            var ls = Services.Get<ILockScreen>();
            if (ls == null)
            {
                Debug.LogError("[Flow][LockScreen] ILockScreen 서비스 미등록 — 씬에 LockScreenModule 확인");
                return;
            }

            var module = UnityEngine.Object.FindObjectOfType<LockScreenModule>();
            if (module == null)
            {
                Debug.LogError("[Flow][LockScreen] LockScreenModule GameObject 미발견");
                return;
            }
            var panel = module.Panel;
            if (panel == null)
            {
                Debug.LogError("[Flow][LockScreen] LockScreenPanel 미할당 — Module의 SerializeField 확인");
                return;
            }

            // 시계 오버라이드 파싱 (parts[2..]에서 Time=HH:mm 검색)
            for (int i = 2; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                if (parts[i].StartsWith("Time="))
                {
                    string hhmm = parts[i].Substring(5);
                    ls.SetClockOverride(hhmm);
                    Debug.Log($"[Flow][LockScreen] 시계 오버라이드: {hhmm}");
                }
            }

            // 모드별 진입
            switch (parts[1])
            {
                case "OpenFirstSetup":
                    panel.OpenFirstSetup();
                    break;
                case "OpenNormal":
                    panel.OpenNormal();
                    break;
                case "OpenReset":
                    panel.OpenReset();
                    break;
                default:
                    Debug.LogWarning($"[Flow][LockScreen] 알 수 없는 서브명령: {parts[1]}");
                    return;
            }

            // OnFlowComplete까지 대기
            bool done = false;
            System.Action onComplete = () => done = true;
            panel.OnFlowComplete += onComplete;
            try
            {
                while (!done)
                {
                    if (ct.IsCancellationRequested) break;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                panel.OnFlowComplete -= onComplete;
                ls.SetClockOverride(""); // 다음 호출에 영향 X
            }

            Debug.Log("[Flow][LockScreen] 완료 — 스토리 복귀");
        }
    }
}
