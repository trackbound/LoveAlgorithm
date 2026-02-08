using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Reflection;
using System.Collections.Generic;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 씬 오브젝트의 중요 바인딩을 자동으로 설정하는 헬퍼
    /// </summary>
    public static class AutoBindingHelper
    {
        const string CharacterDatabasePath = "Assets/Data/CharacterDatabase.asset";
        const string AudioSettingsPath = "Assets/Data/AudioSettings.asset";

        [MenuItem("Tools/LoveAlgo/Auto Bind All References", false, 100)]
        public static void AutoBindAll()
        {
            int totalBound = 0;
            var results = new List<string>();

            // CharacterDatabase 바인딩
            var charDb = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CharacterDatabasePath);
            if (charDb != null)
            {
                totalBound += BindToAllComponents<LoveAlgo.Story.CharacterSlot>(charDb, "characterDatabase", results);
                totalBound += BindToAllComponents<LoveAlgo.Story.CharacterLayer>(charDb, "characterDatabase", results);
                totalBound += BindToAllComponents<LoveAlgo.Story.DialogueUI>(charDb, "characterDatabase", results);
            }
            else
            {
                results.Add($"⚠️ CharacterDatabase not found at {CharacterDatabasePath}");
            }

            // AudioSettings 바인딩
            var audioSettings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AudioSettingsPath);
            if (audioSettings != null)
            {
                totalBound += BindToAllComponents<LoveAlgo.Story.AudioManager>(audioSettings, "audioSettings", results);
            }
            else
            {
                results.Add($"ℹ️ AudioSettings not found at {AudioSettingsPath} (optional)");
            }

            // 결과 표시
            string message = $"✅ Auto Binding Complete!\n\n" +
                           $"Total bindings updated: {totalBound}\n\n" +
                           string.Join("\n", results);

            if (totalBound > 0)
            {
                // 씬 dirty 마킹
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            EditorUtility.DisplayDialog("Auto Binding Results", message, "OK");
            Debug.Log($"[AutoBindingHelper] {message.Replace("\n\n", " | ")}");
        }

        [MenuItem("Tools/LoveAlgo/Check Binding Status", false, 101)]
        public static void CheckBindingStatus()
        {
            var results = new List<string>();
            int missing = 0;
            int bound = 0;

            // CharacterDatabase 체크
            results.Add("=== CharacterDatabase ===");
            CheckComponentBindings<LoveAlgo.Story.CharacterSlot>("characterDatabase", results, ref bound, ref missing);
            CheckComponentBindings<LoveAlgo.Story.CharacterLayer>("characterDatabase", results, ref bound, ref missing);
            CheckComponentBindings<LoveAlgo.Story.DialogueUI>("characterDatabase", results, ref bound, ref missing);

            // AudioSettings 체크
            results.Add("\n=== AudioSettings ===");
            CheckComponentBindings<LoveAlgo.Story.AudioManager>("audioSettings", results, ref bound, ref missing);

            // 결과 표시
            string status = missing > 0 ? "⚠️ Some bindings missing!" : "✅ All bindings OK!";
            string message = $"{status}\n\n" +
                           $"Bound: {bound}, Missing: {missing}\n\n" +
                           string.Join("\n", results);

            EditorUtility.DisplayDialog("Binding Status", message, "OK");
        }

        /// <summary>
        /// 특정 타입의 모든 컴포넌트에 필드 바인딩
        /// </summary>
        static int BindToAllComponents<T>(Object asset, string fieldName, List<string> results) where T : Component
        {
            int count = 0;
            var components = Object.FindObjectsByType<T>(FindObjectsSortMode.None);

            foreach (var comp in components)
            {
                var field = typeof(T).GetField(fieldName, 
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                
                if (field != null)
                {
                    var currentValue = field.GetValue(comp);
                    if (currentValue == null || !currentValue.Equals(asset))
                    {
                        field.SetValue(comp, asset);
                        EditorUtility.SetDirty(comp);
                        results.Add($"  ✓ {typeof(T).Name} on '{comp.gameObject.name}' → {asset.name}");
                        count++;
                    }
                    else
                    {
                        results.Add($"  · {typeof(T).Name} on '{comp.gameObject.name}' (already bound)");
                    }
                }
            }

            if (components.Length == 0)
            {
                results.Add($"  - No {typeof(T).Name} found in scene");
            }

            return count;
        }

        /// <summary>
        /// 컴포넌트 바인딩 상태 체크
        /// </summary>
        static void CheckComponentBindings<T>(string fieldName, List<string> results, 
            ref int bound, ref int missing) where T : Component
        {
            var components = Object.FindObjectsByType<T>(FindObjectsSortMode.None);

            foreach (var comp in components)
            {
                var field = typeof(T).GetField(fieldName, 
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                
                if (field != null)
                {
                    var value = field.GetValue(comp);
                    string objName = comp.gameObject.name;
                    
                    if (value != null && value is Object unityObj && unityObj != null)
                    {
                        results.Add($"  ✓ {typeof(T).Name} on '{objName}': {unityObj.name}");
                        bound++;
                    }
                    else
                    {
                        results.Add($"  ✗ {typeof(T).Name} on '{objName}': MISSING");
                        missing++;
                    }
                }
            }

            if (components.Length == 0)
            {
                results.Add($"  - No {typeof(T).Name} in scene");
            }
        }

        [MenuItem("Tools/LoveAlgo/Create Missing Data Assets", false, 102)]
        public static void CreateMissingDataAssets()
        {
            var results = new List<string>();

            // CharacterDatabase
            if (!System.IO.File.Exists(CharacterDatabasePath))
            {
                var charDb = ScriptableObject.CreateInstance<LoveAlgo.Story.CharacterDatabase>();
                
                // 기본 Speaker 매핑 추가
                charDb.speakerMappings = new List<LoveAlgo.Story.SpeakerMapping>
                {
                    new() { speakerName = "로아", characterId = "Roa" },
                    new() { speakerName = "하예은", characterId = "Yeun" },
                    new() { speakerName = "서다은", characterId = "Daeun" },
                    new() { speakerName = "이봄", characterId = "Bom" },
                    new() { speakerName = "도희원", characterId = "Heewon" },
                };

                EnsureDirectoryExists(CharacterDatabasePath);
                AssetDatabase.CreateAsset(charDb, CharacterDatabasePath);
                results.Add($"✓ Created CharacterDatabase at {CharacterDatabasePath}");
            }
            else
            {
                results.Add($"· CharacterDatabase already exists");
            }

            // AudioSettings
            if (!System.IO.File.Exists(AudioSettingsPath))
            {
                var audioSettings = ScriptableObject.CreateInstance<LoveAlgo.Story.AudioSettings>();
                
                EnsureDirectoryExists(AudioSettingsPath);
                AssetDatabase.CreateAsset(audioSettings, AudioSettingsPath);
                results.Add($"✓ Created AudioSettings at {AudioSettingsPath}");
            }
            else
            {
                results.Add($"· AudioSettings already exists");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string message = "Data Asset Check:\n\n" + string.Join("\n", results);
            EditorUtility.DisplayDialog("Create Data Assets", message, "OK");
        }

        [MenuItem("Tools/LoveAlgo/Add Missing Speaker Mappings", false, 103)]
        public static void AddMissingSpeakerMappings()
        {
            var charDb = AssetDatabase.LoadAssetAtPath<LoveAlgo.Story.CharacterDatabase>(CharacterDatabasePath);
            if (charDb == null)
            {
                EditorUtility.DisplayDialog("Error", $"CharacterDatabase not found at {CharacterDatabasePath}", "OK");
                return;
            }

            var requiredMappings = new Dictionary<string, string>
            {
                { "로아", "Roa" },
                { "하예은", "Yeun" },
                { "서다은", "Daeun" },
                { "이봄", "Bom" },
                { "도희원", "Heewon" },
            };

            int added = 0;
            foreach (var kv in requiredMappings)
            {
                bool exists = charDb.speakerMappings.Exists(m => 
                    m.speakerName == kv.Key || m.characterId == kv.Value);
                
                if (!exists)
                {
                    charDb.speakerMappings.Add(new LoveAlgo.Story.SpeakerMapping
                    {
                        speakerName = kv.Key,
                        characterId = kv.Value
                    });
                    added++;
                }
            }

            if (added > 0)
            {
                EditorUtility.SetDirty(charDb);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Speaker Mappings", 
                    $"Added {added} missing speaker mappings.\n\nTotal mappings: {charDb.speakerMappings.Count}", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Speaker Mappings", 
                    "All speaker mappings already exist.", "OK");
            }
        }

        static void EnsureDirectoryExists(string assetPath)
        {
            string dir = System.IO.Path.GetDirectoryName(assetPath);
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
        }
    }
}
