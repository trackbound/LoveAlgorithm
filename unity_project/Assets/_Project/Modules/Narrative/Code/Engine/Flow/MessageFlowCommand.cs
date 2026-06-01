using LoveAlgo.Contracts;
using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Phone;
using UnityEngine;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// 메신저 메시지 트리거 Flow 명령.
    ///
    /// 문법:
    ///   Flow,,Message:{characterId}:{text},>          — 메시지 도착 (즉시 스토리 계속)
    ///   Flow,,Message:{characterId}:{text}:wait,await — 메시지 도착 + 메신저 열어 사용자 응답 시까지 대기
    ///
    /// 예:
    ///   Flow,,Message:c01:안녕! 뭐해?,>
    ///   Flow,,Message:c03:중요한 얘기 있어:wait,await
    ///
    /// characterId는 StoryMappings의 정전 ID(c01~c05). MessengerSystem 친구 ID로 매핑됨.
    /// </summary>
    public static class MessageFlowCommand
    {
        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            // parts: ["Message", "{characterId}", "{text}", "(wait)?"]
            if (parts.Length < 3)
            {
                Debug.LogWarning("[Flow] Message — 파라미터 부족. 형식: Message:{characterId}:{text}[:wait]");
                return;
            }

            string characterId = parts[1];
            string text = parts[2];
            bool waitForResponse = parts.Length >= 4 && parts[3].Equals("wait", System.StringComparison.OrdinalIgnoreCase);

            // 현재 day 추출 (GameManager.CurrentDay 또는 기본 1)
            int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;

            // 메시지 수신 처리 (MessengerSystem가 ChatRoom 업데이트 + OnNewMessage 발화 → PhoneNotificationButton 자동 반응)
            MessengerSystem.ReceiveMessage(characterId, text, day);
            Log.Info($"[Flow] Message — '{characterId}' → \"{text}\"{(waitForResponse ? " (응답 대기)" : "")}");

            // 확인 필수 메시지: 폰 자동 오픈 + 사용자 응답 대기
            if (waitForResponse)
            {
                // Headless 자동화: 메시지는 이미 수신됐고, 응답 대기는 즉시 통과 (ADR §MessageFlowCommand).
                if (Headless.IsEnabled)
                {
                    Log.Info("[Flow] Message:wait — headless 즉시 통과");
                    return;
                }

                var phone = Services.TryGet<IPhone>();
                if (phone == null)
                {
                    Debug.LogWarning("[Flow] Message:wait — IPhone 서비스 없음, 대기 스킵");
                    return;
                }

                // 진동 효과 (2초) 후 자동 오픈 — PhoneNotificationButton의 폴링이 이미 처리하므로 직접 호출
                await UniTask.Delay(System.TimeSpan.FromSeconds(2), cancellationToken: ct);
                phone.OpenChat(characterId);

                // 사용자가 폰을 닫을 때까지 대기 (CancellationToken 자동 처리)
                await UniTask.WaitWhile(() => phone.IsOpen, cancellationToken: ct);
                Log.Info("[Flow] Message:wait — 사용자가 폰 닫음, 스토리 계속");
            }
        }
    }
}
