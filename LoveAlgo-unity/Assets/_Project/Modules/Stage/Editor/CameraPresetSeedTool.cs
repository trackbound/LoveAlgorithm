using System.IO;
using UnityEditor;
using UnityEngine;
using LoveAlgo.Stage;

namespace LoveAlgo.Stage.EditorTools
{
    /// <summary>
    /// 카메라 프리셋 시드 자동 생성 도구 (Phase D14).
    /// 메뉴: Tools/Camera/Generate Default Presets
    ///
    /// Resources/Data/CameraPresets.asset 생성 — 디자이너가 처음 시작할 때 유용.
    /// 이미 존재하면 덮어쓰기 확인 대화창. CameraPresetSeed.BuildSeedEntries()를 그대로 사용.
    /// 자산이 만들어지면 CameraPresetTable이 다음 도메인 리로드에 자동 로드 (SO 우선 [D5]).
    /// </summary>
    public static class CameraPresetSeedTool
    {
        const string ResourcesFolder = "Assets/Resources/Data";
        const string AssetPath = "Assets/Resources/Data/CameraPresets.asset";

        [MenuItem("Tools/Camera/Generate Default Presets")]
        public static void GenerateDefaultPresets()
        {
            if (File.Exists(AssetPath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Camera Presets 덮어쓰기",
                    $"{AssetPath}\n이미 존재합니다. 시드로 덮어쓸까요?\n(기존 사용자 추가분이 사라집니다.)",
                    "덮어쓰기",
                    "취소");
                if (!overwrite) return;
            }

            EnsureFolder(ResourcesFolder);

            var so = ScriptableObject.CreateInstance<CameraPresetSO>();
            var entries = CameraPresetSeed.BuildSeedEntries();

            // SO의 private List에 SerializedObject로 주입
            // (entries는 [SerializeField] private이라 reflection 대신 SerializedProperty가 안전)
            var soWrapper = new SerializedObject(so);
            var entriesProp = soWrapper.FindProperty("entries");
            if (entriesProp == null)
            {
                Debug.LogError("[CameraPresetSeedTool] SO에 'entries' SerializedProperty 없음 — CameraPresetSO 구조 변경 의심");
                Object.DestroyImmediate(so);
                return;
            }
            entriesProp.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var elem = entriesProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("id").stringValue = e.id;
                elem.FindPropertyRelative("notes").stringValue = e.notes ?? "";

                var stepsProp = elem.FindPropertyRelative("steps");
                stepsProp.arraySize = e.steps.Count;
                for (int s = 0; s < e.steps.Count; s++)
                {
                    var stepElem = stepsProp.GetArrayElementAtIndex(s);
                    stepElem.FindPropertyRelative("command").stringValue = e.steps[s].command ?? "";
                    stepElem.FindPropertyRelative("delaySec").floatValue = e.steps[s].delaySec;
                    stepElem.FindPropertyRelative("waitForCompletion").boolValue = e.steps[s].waitForCompletion;
                }
            }
            soWrapper.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(so, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(so);
            Selection.activeObject = so;
            Debug.Log($"[CameraPresetSeedTool] {AssetPath} 생성 — {entries.Count}개 프리셋 등록.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
