using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Core;
using UnityEngine;

namespace LoveAlgo.Stage
{
    /// <summary>
    /// 카메라 프리셋 실행기 (Phase D5).
    /// CameraPresetTable에서 프리셋을 찾아 step들을 순차 실행. 각 step은 ScreenFX.ExecuteAsync로 dispatch.
    ///
    /// 호출 예 (FXLineExecutor의 CamPreset case):
    ///   await CameraPresetRunner.RunAsync("ZoomIn-Hard", ct);
    /// </summary>
    public static class CameraPresetRunner
    {
        /// <summary>
        /// 프리셋 실행. 이름이 없으면 LogWarning + 무동작.
        /// step의 waitForCompletion=false면 fire-and-forget(.Forget) — 다음 step 즉시 시작.
        /// step의 delaySec > 0이면 그만큼 await 후 실행.
        /// </summary>
        public static async UniTask RunAsync(string name, CancellationToken ct = default)
        {
            var entry = CameraPresetTable.Resolve(name);
            if (entry == null)
            {
                Debug.LogWarning($"[CameraPresetRunner] 프리셋 '{name}' 없음 — 등록된 이름: {string.Join(", ", CameraPresetTable.Names)}");
                return;
            }

            var fx = ScreenFX.Instance;
            if (fx == null)
            {
                Debug.LogWarning($"[CameraPresetRunner] ScreenFX 인스턴스 없음 — '{name}' 스킵");
                return;
            }

            foreach (var step in entry.steps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.command)) continue;

                if (step.delaySec > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(step.delaySec), cancellationToken: ct);

                if (step.waitForCompletion)
                {
                    await fx.ExecuteAsync(step.command, ct);
                }
                else
                {
                    fx.ExecuteAsync(step.command, ct).Forget();
                }
            }
        }
    }
}
