using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 시나리오 CSV 저장 전 자동 백업.
    /// 경로: {StreamingAssets/Story}/.backups/{scriptName}.{yyyyMMdd_HHmmss}.csv
    ///
    /// 정책:
    ///   - 저장 호출 시마다 Snapshot 호출 → 그 시점 원본 복사본 생성
    ///   - keep=20 롤링 — 가장 오래된 것부터 자동 삭제
    ///   - 외부 텍스트 에디터로 열어서 잘못된 변경 복구 가능
    /// </summary>
    public static class BackupManager
    {
        const string BackupSubdir = ".backups";
        const int DefaultKeep = 20;

        public static string BackupDir => Path.Combine(StoryAssetLoader.StoryDir, BackupSubdir);

        /// <summary>현재 원본 CSV를 타임스탬프 파일로 백업. 원본 없으면 무시.</summary>
        public static string Snapshot(string scriptName, int keep = DefaultKeep)
        {
            if (string.IsNullOrEmpty(scriptName)) return null;
            if (!StoryAssetLoader.IsWritable) return null;

            string srcPath = StoryAssetLoader.GetCsvPath(scriptName);
            if (!File.Exists(srcPath))
            {
                Debug.LogWarning($"[Backup] 원본 없음 — 백업 스킵: {srcPath}");
                return null;
            }

            try
            {
                Directory.CreateDirectory(BackupDir);
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string dstPath = Path.Combine(BackupDir, $"{scriptName}.{ts}.csv");

                // 같은 초에 중복되면 ms 추가
                if (File.Exists(dstPath))
                    dstPath = Path.Combine(BackupDir, $"{scriptName}.{ts}_{DateTime.Now.Millisecond:D3}.csv");

                File.Copy(srcPath, dstPath, overwrite: false);
                Debug.Log($"[Backup] saved → {dstPath}");

                TrimOld(scriptName, keep);
                return dstPath;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Backup] snapshot fail: {e.Message}");
                return null;
            }
        }

        /// <summary>오래된 백업 정리 — keep 최신만 유지.</summary>
        public static void TrimOld(string scriptName, int keep = DefaultKeep)
        {
            if (!Directory.Exists(BackupDir)) return;
            try
            {
                var matches = Directory.GetFiles(BackupDir, $"{scriptName}.*.csv")
                    .OrderByDescending(p => File.GetLastWriteTimeUtc(p))
                    .ToArray();
                if (matches.Length <= keep) return;
                for (int i = keep; i < matches.Length; i++)
                {
                    File.Delete(matches[i]);
                    Debug.Log($"[Backup] trimmed: {Path.GetFileName(matches[i])}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Backup] trim fail: {e.Message}");
            }
        }

        /// <summary>해당 스크립트의 백업 목록 (최신순).</summary>
        public static string[] ListBackups(string scriptName)
        {
            if (!Directory.Exists(BackupDir)) return Array.Empty<string>();
            return Directory.GetFiles(BackupDir, $"{scriptName}.*.csv")
                .OrderByDescending(p => File.GetLastWriteTimeUtc(p))
                .ToArray();
        }
    }
}
