using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// LoadingScene 명령 — 로딩화면 표시 (자동저장 없음 — Save는 CSV에서 별도 배치)
    /// CSV: Flow,,LoadingScene[:표시시간],await
    /// 2레이어 구조: 검은 배경이 항상 화면을 덮고, 일러스트만 페이드 IN/OUT
    /// 종료 시 ScreenFX 암전 유지 → SceneStart가 장면 설정 후 reveal 담당
    /// </summary>
    public static class LoadingSceneFlowCommand
    {
        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            float displayTime = parts.Length > 1 && float.TryParse(parts[1], out float dt) ? dt : 2.0f;
            Debug.Log($"[Flow] LoadingScene (표시={displayTime}s)");

            var loading = LoadingScreen.Instance;
            var fx = ScreenFX.Instance;

            if (loading != null)
            {
                // 로딩 화면 표시 (검은 배경 즉시 + 일러스트 페이드인)
                // LoadingScreen은 ScreenFX 위 레이어이므로 즉시 화면을 덮음
                await loading.ShowAsync(ct);

                // ScreenFX 초기화 (LoadingScreen의 검은 배경이 덮고 있으므로 안전)
                fx?.SetClear();

                // 최소 표시 시간 보장
                float elapsed = loading.FadeInDuration;
                float wait = Mathf.Max(displayTime, loading.MinDisplayTime) - elapsed;
                if (wait > 0f)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(wait), cancellationToken: ct);

                // 일러스트만 페이드아웃 (검은 배경 유지 → 화면 노출 없음)
                await loading.HideIllustrationAsync(ct);

                // ScreenFX 암전 설정 → LoadingScreen 제거
                fx?.SetBlack();
                loading.HideImmediate();

                // 암전 상태로 종료 — SceneStart가 BG 설정 + reveal 담당
            }
            else
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(displayTime), cancellationToken: ct);
            }

            Debug.Log("[Flow] LoadingScene 완료 (암전 유지)");
        }
    }
}
