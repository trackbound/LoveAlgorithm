using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 시나리오 CSV 로딩·저장 단일 진입점.
    ///
    /// 이전엔 Resources/Story 였지만 빌드 후 외부 편집 불가 → StreamingAssets/Story로 이전.
    /// StreamingAssets는 빌드 폴더에 .csv 그대로 노출 → 외부 텍스트 에디터·내부 편집기 모두 가능.
    ///
    /// 플랫폼별 분기:
    ///   - PC/Mac/에디터: File.ReadAllText, File.WriteAllText 직접 사용
    ///   - Android/iOS: StreamingAssets는 jar/번들 내부 → UnityWebRequest로만 읽기 가능, 쓰기 불가
    ///
    /// 본 프로젝트는 PC 빌드 우선이라 쓰기는 데스크톱에서만 동작.
    /// </summary>
    public static class StoryAssetLoader
    {
        const string StorySubdir = "Story";

        /// <summary>스토리 CSV 폴더 절대경로.</summary>
        public static string StoryDir => Path.Combine(Application.streamingAssetsPath, StorySubdir);

        /// <summary>현재 플랫폼에서 SaveCsv 가능 여부 (Win/Mac/Editor=true, 모바일=false).</summary>
        public static bool IsWritable
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>스크립트 이름(확장자 제외) → 절대경로.</summary>
        public static string GetCsvPath(string scriptName)
        {
            return Path.Combine(StoryDir, scriptName + ".csv");
        }

        /// <summary>
        /// CSV 텍스트 비동기 로드.
        /// 모바일에서도 동작하도록 UnityWebRequest 사용 (PC에선 file:// 프로토콜 동일 작동).
        /// </summary>
        public static async UniTask<string> LoadCsvAsync(string scriptName)
        {
            string path = GetCsvPath(scriptName);

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            // 데스크톱: 직접 파일 읽기 (가장 빠르고 단순)
            if (!File.Exists(path))
            {
                Debug.LogError($"[StoryAssetLoader] CSV 없음: {path}");
                return null;
            }
            try
            {
                return await UniTask.RunOnThreadPool(() => File.ReadAllText(path, System.Text.Encoding.UTF8));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StoryAssetLoader] CSV 읽기 실패 '{scriptName}': {e.Message}");
                return null;
            }
#else
            // 모바일/WebGL: UnityWebRequest 필수
            string uri = path;
            if (!uri.Contains("://")) uri = "file://" + uri;
            using (var req = UnityWebRequest.Get(uri))
            {
                await req.SendWebRequest().ToUniTask();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[StoryAssetLoader] CSV 로드 실패 '{scriptName}': {req.error}");
                    return null;
                }
                return req.downloadHandler.text;
            }
#endif
        }

        /// <summary>
        /// CSV 텍스트를 원본 파일에 덮어쓰기. 데스크톱·에디터에서만 동작.
        /// 호출 전 BackupManager로 백업 권장.
        /// </summary>
        public static void SaveCsv(string scriptName, string csv)
        {
            if (!IsWritable)
            {
                Debug.LogWarning($"[StoryAssetLoader] 현재 플랫폼에서 쓰기 불가 — {scriptName} 저장 무시");
                return;
            }

            string path = GetCsvPath(scriptName);
            try
            {
                Directory.CreateDirectory(StoryDir);
                File.WriteAllText(path, csv, new System.Text.UTF8Encoding(false));
                Debug.Log($"[StoryAssetLoader] 저장: {path} ({csv.Length} chars)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StoryAssetLoader] CSV 저장 실패 '{scriptName}': {e.Message}");
            }
        }

        /// <summary>해당 스크립트 파일의 마지막 수정 시각 (외부 변경 감지용). 없으면 DateTime.MinValue.</summary>
        public static System.DateTime GetLastWriteTime(string scriptName)
        {
            string path = GetCsvPath(scriptName);
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : System.DateTime.MinValue;
        }
    }
}
