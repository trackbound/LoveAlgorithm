using System.Diagnostics;
using LoveAlgo.Events; // ShowDevToastCommand, DevToastSeverity

namespace LoveAlgo.Common
{
    /// <summary>
    /// 개발 디버그용 화면 토스트(우측 상단). <see cref="Log"/>와 동일하게 <c>[Conditional]</c>이라 에디터/개발
    /// (테스트) 빌드에서만 호출이 컴파일되고 릴리즈 빌드에선 호출 자체가 제거된다 — 프로덕션 사용자에겐 절대 안 뜸.
    /// 게임용 토스트(<c>ShowToastCommand</c>/ToastView)와 분리된 별도 채널: 표시는 <c>DevToastView</c>가 담당.
    ///
    /// 색 구분: <see cref="Info"/>=초록(단순 알림·로그), <see cref="Warn"/>=노랑(경고), <see cref="Error"/>=빨강(위험).
    /// 사용: <c>DevToast.Warn($"표정 없음: {char}/{emote} → 기본")</c>. 화면 알림이 필요 없는 순수 콘솔 로그는 <see cref="Log"/>.
    /// </summary>
    public static class DevToast
    {
        const string EDITOR = "UNITY_EDITOR";
        const string DEV = "DEVELOPMENT_BUILD";

        [Conditional(EDITOR), Conditional(DEV)]
        public static void Info(string message) => EventBus.Publish(new ShowDevToastCommand(message, DevToastSeverity.Info));

        [Conditional(EDITOR), Conditional(DEV)]
        public static void Warn(string message) => EventBus.Publish(new ShowDevToastCommand(message, DevToastSeverity.Warn));

        [Conditional(EDITOR), Conditional(DEV)]
        public static void Error(string message) => EventBus.Publish(new ShowDevToastCommand(message, DevToastSeverity.Error));

        [Conditional(EDITOR), Conditional(DEV)]
        public static void Show(string message, DevToastSeverity severity, float duration = 0f)
            => EventBus.Publish(new ShowDevToastCommand(message, severity, duration));
    }
}
