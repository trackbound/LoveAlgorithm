using System;
using System.Collections.Generic;

namespace LoveAlgo.Data
{
    /// <summary>
    /// BG 이름을 Resources 경로 후보들로 정규화한다.
    /// 디렉터리 스캔 없이 정적 규칙으로만 후보를 생성한다.
    /// </summary>
    public static class BgPathResolver
    {
        static readonly Dictionary<string, string> FolderByPrefix = new(StringComparer.OrdinalIgnoreCase)
        {
            { "BG_MyRoom_", "MyRoom" },
            { "BG_Engineering_", "Engineering" },
            { "BG_StudentCenter_", "StudentCenter" },
            { "BG_ClubRoom_", "ClubRoom" },
            { "BG_Campus_Street", "CampusStreet" },
        };

        static readonly string[] KnownTimeSuffixes =
        {
            "_Day_Cherry",
            "_Night_LightOn",
            "_Day",
            "_Night",
        };

        /// <summary>
        /// 입력 이름에서 로드 가능한 경로 후보를 우선순위 순서로 반환한다.
        /// </summary>
        public static IReadOnlyList<string> ResolvePaths(string bgName)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(bgName))
                return result;

            string normalized = Normalize(bgName);
            string aliased = ApplyLegacyAlias(normalized);

            AddCandidate(result, seen, BgPathMapping.GetPath(aliased));
            AddFromName(result, seen, aliased);

            // _Day / _Night 등 접미사 유무 차이 보정
            string noSuffix = RemoveKnownTimeSuffix(aliased);
            if (!string.Equals(noSuffix, aliased, StringComparison.OrdinalIgnoreCase))
            {
                AddFromName(result, seen, noSuffix);
            }
            else
            {
                AddFromName(result, seen, aliased + "_Day");
                AddFromName(result, seen, aliased + "_Night");
            }

            return result;
        }

        static string Normalize(string bgName)
        {
            string s = bgName.Trim().Replace('\\', '/');
            const string prefix = "Backgrounds/";
            if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(prefix.Length);
            }
            return s;
        }

        static string ApplyLegacyAlias(string name)
        {
            if (BgPathMapping.LegacyNames.TryGetValue(name, out var mapped))
            {
                return mapped;
            }
            return name;
        }

        static void AddFromName(List<string> result, HashSet<string> seen, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            string normalized = Normalize(name);

            // 이미 폴더가 포함된 경우
            if (normalized.Contains("/"))
            {
                AddCandidate(result, seen, "Backgrounds/" + normalized);
                return;
            }

            // 생성된 맵 우선
            if (BgPathMapping.Paths.TryGetValue(normalized, out var mappedPath))
            {
                AddCandidate(result, seen, mappedPath);
            }

            // BG 접두어 기반 정적 폴더 추론
            string folder = GuessFolder(normalized);
            if (!string.IsNullOrEmpty(folder))
            {
                AddCandidate(result, seen, $"Backgrounds/{folder}/{normalized}");
            }

            // 루트 폴더 폴백
            AddCandidate(result, seen, $"Backgrounds/{normalized}");
        }

        static string GuessFolder(string bgName)
        {
            foreach (var kv in FolderByPrefix)
            {
                if (bgName.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }
            return null;
        }

        static string RemoveKnownTimeSuffix(string bgName)
        {
            foreach (var suffix in KnownTimeSuffixes)
            {
                if (bgName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return bgName.Substring(0, bgName.Length - suffix.Length);
                }
            }
            return bgName;
        }

        static void AddCandidate(List<string> result, HashSet<string> seen, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            string normalized = path.Trim().Replace('\\', '/');
            if (seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }
    }
}
