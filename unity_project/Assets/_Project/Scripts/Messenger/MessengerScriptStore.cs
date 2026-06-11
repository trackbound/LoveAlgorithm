using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using LoveAlgo.Common; // Log

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 메신저 시퀀스 CSV 로딩 + 파싱 캐시(런타임). <c>StreamingAssets/Messenger/{relPath}</c>에서 읽는다 —
    /// 스토리와 같은 작가 파이프라인(빌드에서 편집 가능, StoryAssetLoader idiom). 파싱 오류는 경고 로그 후
    /// 유효 줄만 반환(fail-open — 시퀀스 일부라도 재생). 테스트/도구는 <see cref="Preload"/>로 파일 I/O 없이 주입.
    /// </summary>
    public static class MessengerScriptStore
    {
        static readonly Dictionary<string, IReadOnlyList<MessengerLine>> _cache = new();

        static string PathFor(string relPath) =>
            Path.Combine(Application.streamingAssetsPath, "Messenger", relPath);

        /// <summary>시퀀스 라인 조회(캐시 우선). 파일 없음/실패 시 빈 목록(호출부 fail-open).</summary>
        public static IReadOnlyList<MessengerLine> Get(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return Array.Empty<MessengerLine>();
            if (_cache.TryGetValue(relPath, out var cached)) return cached;

            string csv = ReadFile(relPath);
            return Cache(relPath, csv);
        }

        /// <summary>CSV 텍스트를 직접 주입(파일 I/O 없이 — PlayMode 테스트/도구 라이브 미리보기).</summary>
        public static IReadOnlyList<MessengerLine> Preload(string relPath, string csv)
        {
            if (string.IsNullOrEmpty(relPath)) return Array.Empty<MessengerLine>();
            return Cache(relPath, csv);
        }

        /// <summary>캐시 비우기 — 작가가 CSV를 고친 뒤 재로딩할 때.</summary>
        public static void ClearCache() => _cache.Clear();

        // Reload Domain Off 가드 — PlayMode 진입 시 이전 실행의 캐시 잔존 방지(ScriptParser.Strict 선례).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticStateOnLoad() => ClearCache();

        static IReadOnlyList<MessengerLine> Cache(string relPath, string csv)
        {
            var parsed = MessengerScriptParser.Parse(csv);
            if (parsed.HasErrors)
                Log.Warn($"[MessengerScriptStore] {relPath} 파싱 오류 {parsed.Errors.Count}건: {string.Join(" / ", parsed.Errors)}");
            _cache[relPath] = parsed.Lines;
            return parsed.Lines;
        }

        static string ReadFile(string relPath)
        {
            var path = PathFor(relPath);
            if (!File.Exists(path)) { Log.Warn($"[MessengerScriptStore] CSV 없음: {path}"); return null; }
            try { return File.ReadAllText(path); }
            catch (Exception e) { Log.Error($"[MessengerScriptStore] 로드 실패({relPath}): {e}"); return null; }
        }
    }
}
