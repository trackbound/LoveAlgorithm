using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.UI;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// Username 명령 — 스토리 중간에 이름 입력 UI를 열어 1회 입력 후 스토리로 복귀
    /// CSV: Flow,,Username,>
    /// 입력된 이름은 GameManager / GameState 양쪽에 저장되어 대사 변수 치환에 즉시 반영됨.
    /// </summary>
    public static class UsernameFlowCommand
    {
        /// <summary>페이드 아웃/인 시간 (초)</summary>
        const float FadeDuration = 0.35f;

        public static async UniTask ExecuteAsync(CancellationToken ct)
        {
            Debug.Log("[Flow] Username — 인라인 이름 입력 시작");

            var gm = GameManager.Instance;
            var usernameUI = UIManager.Instance?.UsernameUI;
            if (gm == null || usernameUI == null)
            {
                Debug.LogWarning("[Flow] Username — GameManager 또는 UsernameUI 없음");
                return;
            }

            var fx = ScreenFX.Instance;
            var dialogueUI = UIManager.Instance?.DialogueUI;

            // 페이드 아웃 → UI 전환 → 페이드 인
            if (fx != null) await fx.FadeOutAsync(FadeDuration, ct);

            dialogueUI?.HideImmediate();
            UIManager.Instance?.ShowOnly(MainUIType.Username);

            if (fx != null) await fx.FadeInAsync(FadeDuration, ct);

            // 입력 완료까지 대기 (확인 팝업 OK까지)
            string playerName = await usernameUI.ShowInlineAsync(ct);

            // GameManager / GameState에 이름 반영 (phase 전환은 하지 않음)
            gm.SetPlayerName(playerName);
            if (GameState.Instance != null)
                GameState.Instance.SetPlayerName(playerName);

            // 페이드 아웃 → 스토리 UI 복귀 → 페이드 인
            if (fx != null) await fx.FadeOutAsync(FadeDuration, ct);

            UIManager.Instance?.ShowOnly(MainUIType.Dialogue);
            dialogueUI?.Clear();
            dialogueUI?.HideImmediate();

            if (fx != null) await fx.FadeInAsync(FadeDuration, ct);

            Debug.Log($"[Flow] Username — 인라인 이름 입력 완료: {playerName}");
        }
    }
}
