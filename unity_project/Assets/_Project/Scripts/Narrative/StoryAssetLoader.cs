using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 런타임 스토리 CSV 파일 I/O. StreamingAssets/Story 폴더에서 읽고 쓴다 — 데스크톱 standalone 빌드에선
    /// 실제 폴더라 쓰기 가능(작가 편집), 에디터에선 <c>Assets/StreamingAssets/Story</c>. <c>JsonSaveStore</c>와
    /// 같은 idiom(System.IO + try/catch). 엔진은 이 텍스트를 <c>PlayScriptCommand(inlineCsv)</c>로 재생한다 —
    /// Resources/TextAsset 의존을 떼어 빌드에서도 편집 가능하게 한다.
    /// </summary>
    public static class StoryAssetLoader
    {
        static string Dir => Path.Combine(Application.streamingAssetsPath, "Story");
        static string PathFor(string relPath) => Path.Combine(Dir, relPath);

        /// <summary>StreamingAssets/Story/{relPath} 텍스트. 없거나 실패 시 null(호출부 fail-open).</summary>
        public static string Read(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return null;
            var path = PathFor(relPath);
            if (!File.Exists(path)) { Debug.LogWarning($"[StoryAssetLoader] CSV 없음: {path}"); return null; }
            try { return File.ReadAllText(path); }
            catch (Exception e) { Debug.LogError($"[StoryAssetLoader] 로드 실패({relPath}): {e}"); return null; }
        }

        /// <summary>{relPath}에 텍스트 기록(UTF-8 no-BOM, 폴더 자동 생성). 실패 시 false + 에러.</summary>
        public static bool Write(string relPath, string text)
        {
            if (string.IsNullOrEmpty(relPath)) return false;
            try
            {
                var full = PathFor(relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full)); // relPath 서브폴더까지 생성
                File.WriteAllText(full, text ?? "", new UTF8Encoding(false));
                return true;
            }
            catch (Exception e) { Debug.LogError($"[StoryAssetLoader] 저장 실패({relPath}): {e}"); return false; }
        }

        /// <summary>Story 폴더 톱레벨의 .csv 파일명 목록(닷폴더/.bak 백업 제외). 폴더 없으면 빈 목록.</summary>
        public static List<string> List()
        {
            var result = new List<string>();
            if (!Directory.Exists(Dir)) return result;
            try
            {
                foreach (var full in Directory.GetFiles(Dir, "*.csv", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(full);
                    if (IsListableCsv(name)) result.Add(name);
                }
            }
            catch (Exception e) { Debug.LogError($"[StoryAssetLoader] 목록 실패: {e}"); }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        /// <summary>목록에 보일 스토리 CSV인가(순수): .csv로 끝나고(Windows quirk 방어) 백업(.bak)이 아님.</summary>
        public static bool IsListableCsv(string name) =>
            !string.IsNullOrEmpty(name)
            && name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            && name.IndexOf(".bak", StringComparison.OrdinalIgnoreCase) < 0;
    }
}
