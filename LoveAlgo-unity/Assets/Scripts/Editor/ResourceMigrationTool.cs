// ═══════════════════════════════════════════════════════════════════
// 리소스 매핑 단순화를 위한 일회성 마이그레이션 도구
// Unity Editor에서 LoveAlgo > Tools > Migrate Resources 실행
// 완료 후 이 스크립트를 삭제하세요.
// ═══════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.Editor
{
    public static class ResourceMigrationTool
    {
        [MenuItem("LoveAlgo/Tools/Migrate Resources (1회성)", priority = 300)]
        public static void MigrateAll()
        {
            if (!EditorUtility.DisplayDialog(
                "리소스 마이그레이션",
                "BG 서브폴더 플랫화 + 캐릭터 표정 리네임을 실행합니다.\n\n" +
                "• Backgrounds/ 하위 서브폴더의 모든 PNG를 루트로 이동\n" +
                "• Characters/ 하위 01_Default.png → Default.png 등 리네임\n\n" +
                "계속하시겠습니까?",
                "실행", "취소"))
            {
                return;
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                int bgMoved = FlattenBackgrounds();
                int charRenamed = RenameCharacterEmotes();

                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();

                Debug.Log($"[Migration] ✅ 완료 — BG {bgMoved}개 이동, 표정 {charRenamed}개 리네임");
                EditorUtility.DisplayDialog("완료",
                    $"BG {bgMoved}개 이동, 표정 {charRenamed}개 리네임 완료.\n" +
                    "이 스크립트(ResourceMigrationTool.cs)를 삭제해도 됩니다.",
                    "확인");
            }
            catch (System.Exception ex)
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
                Debug.LogError($"[Migration] ❌ 오류: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Backgrounds/ 서브폴더의 모든 PNG를 루트로 이동 (GUID 보존)
        /// </summary>
        static int FlattenBackgrounds()
        {
            const string root = "Assets/Resources/Backgrounds";
            int moved = 0;

            var subfolders = AssetDatabase.GetSubFolders(root);
            foreach (var folder in subfolders)
            {
                var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
                foreach (var guid in guids)
                {
                    var oldPath = AssetDatabase.GUIDToAssetPath(guid);
                    var fileName = Path.GetFileName(oldPath);
                    var newPath = $"{root}/{fileName}";

                    if (oldPath == newPath) continue;

                    if (File.Exists(newPath))
                    {
                        Debug.LogWarning($"[Migration] 이름 충돌 건너뜀: {fileName}");
                        continue;
                    }

                    var result = AssetDatabase.MoveAsset(oldPath, newPath);
                    if (string.IsNullOrEmpty(result))
                    {
                        moved++;
                        Debug.Log($"[Migration] BG 이동: {oldPath} → {newPath}");
                    }
                    else
                    {
                        Debug.LogError($"[Migration] BG 이동 실패: {oldPath} → {result}");
                    }
                }
            }

            // 빈 서브폴더 삭제
            foreach (var folder in subfolders)
            {
                var remaining = AssetDatabase.FindAssets("", new[] { folder });
                if (remaining.Length == 0)
                {
                    AssetDatabase.DeleteAsset(folder);
                    Debug.Log($"[Migration] 빈 폴더 삭제: {folder}");
                }
            }

            return moved;
        }

        /// <summary>
        /// Characters/ 하위 표정 파일 리네임: 01_Default.png → Default.png (GUID 보존)
        /// </summary>
        static int RenameCharacterEmotes()
        {
            const string root = "Assets/Resources/Characters";
            int renamed = 0;

            var characterFolders = AssetDatabase.GetSubFolders(root);
            foreach (var charFolder in characterFolders)
            {
                var guids = AssetDatabase.FindAssets("t:Sprite", new[] { charFolder });
                foreach (var guid in guids)
                {
                    var oldPath = AssetDatabase.GUIDToAssetPath(guid);
                    var oldName = Path.GetFileNameWithoutExtension(oldPath);
                    var ext = Path.GetExtension(oldPath);

                    // "01_Default" → "Default", "02_EyeSmile" → "EyeSmile" 등
                    // 패턴: 숫자_이름
                    var underscoreIdx = oldName.IndexOf('_');
                    if (underscoreIdx < 0) continue;

                    var prefix = oldName.Substring(0, underscoreIdx);
                    if (!int.TryParse(prefix, out _)) continue;

                    var newName = oldName.Substring(underscoreIdx + 1);
                    if (newName == oldName) continue;

                    var newPath = $"{charFolder}/{newName}{ext}";

                    if (File.Exists(newPath))
                    {
                        Debug.LogWarning($"[Migration] 이름 충돌 건너뜀: {newPath}");
                        continue;
                    }

                    var result = AssetDatabase.MoveAsset(oldPath, newPath);
                    if (string.IsNullOrEmpty(result))
                    {
                        renamed++;
                        Debug.Log($"[Migration] 표정 리네임: {oldPath} → {newPath}");
                    }
                    else
                    {
                        Debug.LogError($"[Migration] 표정 리네임 실패: {oldPath} → {result}");
                    }
                }
            }

            return renamed;
        }
    }
}
