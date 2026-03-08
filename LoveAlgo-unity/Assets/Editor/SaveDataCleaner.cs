using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 세이브 데이터 정리 에디터 도구
    /// 테스트 시 이전 세션의 잔여 세이브 파일을 삭제할 때 사용
    /// </summary>
    public static class SaveDataCleaner
    {
        const string SaveFolder = "Saves";

        [MenuItem("LoveAlgo/세이브 데이터 전체 삭제", false, 100)]
        static void DeleteAllSaveData()
        {
            string folder = Path.Combine(Application.persistentDataPath, SaveFolder);

            if (!Directory.Exists(folder))
            {
                Debug.Log("[SaveDataCleaner] 세이브 폴더가 존재하지 않습니다.");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "세이브 데이터 삭제",
                $"모든 세이브 파일을 삭제합니다.\n경로: {folder}\n\n이 작업은 되돌릴 수 없습니다.",
                "삭제", "취소");

            if (!confirm) return;

            Directory.Delete(folder, recursive: true);
            Debug.Log($"[SaveDataCleaner] 세이브 폴더 삭제 완료: {folder}");
        }

        [MenuItem("LoveAlgo/세이브 폴더 열기", false, 101)]
        static void OpenSaveFolder()
        {
            string folder = Path.Combine(Application.persistentDataPath, SaveFolder);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            EditorUtility.RevealInFinder(folder);
        }
    }
}
