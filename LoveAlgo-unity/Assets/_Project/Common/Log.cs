using System.Diagnostics;
using UnityEngine;
using UDebug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace LoveAlgo.Common
{
    /// <summary>
    /// Conditional 래퍼 로거. 에디터/개발 빌드에서만 출력되도록 컴파일러가 호출 자체를 제거 →
    /// 릴리즈 빌드에서 문자열 보간/콘솔 I/O 비용 없음.
    /// - Info/Warn: UNITY_EDITOR 또는 DEVELOPMENT_BUILD에서만 출력
    /// - Error/Exception: 항상 출력 (사용자 보고에 필요)
    /// 사용: Log.Info($"[Foo] bar={bar}"); — 핫팟(Update/코루틴 루프)에서는 Debug.Log 대신 이걸 사용.
    /// </summary>
    public static class Log
    {
        const string EDITOR = "UNITY_EDITOR";
        const string DEV = "DEVELOPMENT_BUILD";

        [Conditional(EDITOR), Conditional(DEV)]
        public static void Info(string message) => UDebug.Log(message);

        [Conditional(EDITOR), Conditional(DEV)]
        public static void Info(string message, Object context) => UDebug.Log(message, context);

        [Conditional(EDITOR), Conditional(DEV)]
        public static void Warn(string message) => UDebug.LogWarning(message);

        [Conditional(EDITOR), Conditional(DEV)]
        public static void Warn(string message, Object context) => UDebug.LogWarning(message, context);

        public static void Error(string message) => UDebug.LogError(message);
        public static void Error(string message, Object context) => UDebug.LogError(message, context);

        public static void Exception(System.Exception e) => UDebug.LogException(e);
        public static void Exception(System.Exception e, Object context) => UDebug.LogException(e, context);
    }
}
