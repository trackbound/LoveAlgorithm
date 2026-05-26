using System;
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
    /// 기획서 §진입 정보 + §비밀번호 입력 커스텀 시스템 통합.
    ///
    /// 서브명령 (case-insensitive):
    ///   FirstSetup / OpenFirstSetup     비번 첫 설정 (이름 입력 직후) — 평문 입력
    ///   Normal     / OpenNormal         평상 잠금화면 (재로그인 연출) — 마스킹
    ///   Reset      / OpenReset          재설정 — FirstSetup과 동일 흐름
    ///   Auto                            비번 설정 여부 자동 판별 (있으면 Normal, 없으면 FirstSetup)
    ///   GameStart                       게임 첫 시작 sugar (5초 페이드인 강제 + Auto)
    ///
    /// 옵션 토큰 (순서 자유):
    ///   Time=HH:mm                       시계 1회 오버라이드
    ///   FadeOut                          Outro에 페이드아웃까지 포함 → 완료 시 화면 노출 상태
    ///   NoFadeOut                        명시적으로 페이드아웃 생략 (기본값과 동일)
    ///
    /// 동작: LockScreenPanel 표시 → OnFlowComplete까지 await → 스토리 진행 복귀.
    ///
    /// 예시 CSV:
    ///   ,Flow,,LockScreen:FirstSetup,>
    ///   ,Flow,,LockScreen:Normal:Time=07:30,>
    ///   ,Flow,,LockScreen:Auto:FadeOut:Time=23:58,await
    ///   ,Flow,,LockScreen:GameStart,await
    /// </summary>
    public static class LockScreenFlowCommand
    {
        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            if (parts.Length < 2)
            {
                Debug.LogWarning("[Flow][LockScreen] 인자 부족 — LockScreen:<FirstSetup|Normal|Reset|Auto|GameStart>[:Time=HH:mm][:FadeOut]");
                return;
            }

            var ls = Services.Get<ILockScreen>();
            if (ls == null)
            {
                Debug.LogError("[Flow][LockScreen] ILockScreen 서비스 미등록 — 씬에 LockScreenModule 확인");
                return;
            }

            var module = UnityEngine.Object.FindAnyObjectByType<LockScreenModule>();
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

            // ── 옵션 토큰 파싱 (parts[2..]) ──
            bool? withFadeOut = null;
            for (int i = 2; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                string token = parts[i];

                if (token.StartsWith("Time=", StringComparison.OrdinalIgnoreCase))
                {
                    string hhmm = token.Substring(5);
                    ls.SetClockOverride(hhmm);
                    Debug.Log($"[Flow][LockScreen] 시계 오버라이드: {hhmm}");
                }
                else if (string.Equals(token, "FadeOut", StringComparison.OrdinalIgnoreCase))
                    withFadeOut = true;
                else if (string.Equals(token, "NoFadeOut", StringComparison.OrdinalIgnoreCase))
                    withFadeOut = false;
            }

            // ── outro fade-out 옵션 적용 ──
            panel.SetFadeOutAfter(withFadeOut);

            // ── 모드별 진입 (case-insensitive) ──
            string sub = parts[1];
            if (Equals(sub, "FirstSetup") || Equals(sub, "OpenFirstSetup"))
                panel.OpenFirstSetup();
            else if (Equals(sub, "Normal") || Equals(sub, "OpenNormal"))
                panel.OpenNormal();
            else if (Equals(sub, "Reset") || Equals(sub, "OpenReset"))
                panel.OpenReset();
            else if (Equals(sub, "Auto"))
                panel.OpenAuto();
            else if (Equals(sub, "GameStart"))
                panel.OpenForGameStart();
            else
            {
                Debug.LogWarning($"[Flow][LockScreen] 알 수 없는 서브명령: {sub}");
                return;
            }

            // OnFlowComplete까지 대기. 외부 강제 종료(GameFlowJumper.TearDownEverythingAsync 등)
            // 안전망: panel이 inactive면 탈출 — Close()가 OnFlowComplete를 발행하지 않으므로 필수.
            bool done = false;
            System.Action onComplete = () => done = true;
            panel.OnFlowComplete += onComplete;
            try
            {
                while (!done)
                {
                    if (ct.IsCancellationRequested) break;
                    if (panel == null || !panel.gameObject.activeSelf) break;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                if (panel != null) panel.OnFlowComplete -= onComplete;
                ls.SetClockOverride(""); // 다음 호출에 영향 X
            }

            Debug.Log("[Flow][LockScreen] 완료 — 스토리 복귀");
        }

        static bool Equals(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
