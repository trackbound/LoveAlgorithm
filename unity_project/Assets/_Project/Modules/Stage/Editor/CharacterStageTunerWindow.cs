#if UNITY_EDITOR
using LoveAlgo.Story;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.StageEditor
{
    /// <summary>
    /// 5명 캐릭터의 spriteScale / offsetX / offsetY / pivotY 를 한 화면 슬라이더로 조정.
    /// 메뉴: Tools > LoveAlgo > Stage > Character Tuner
    ///
    /// CharacterStageDatabase.asset 을 직접 편집. Inspector 펼치지 않고도 빠르게 튜닝.
    /// </summary>
    public class CharacterStageTunerWindow : EditorWindow
    {
        CharacterStageDatabase db;
        Vector2 scroll;

        [MenuItem("Tools/LoveAlgo/Stage/Character Tuner")]
        public static void Open()
        {
            var w = GetWindow<CharacterStageTunerWindow>("Character Tuner");
            w.minSize = new Vector2(520, 480);
            w.AutoBind();
            w.Show();
        }

        void OnEnable() => AutoBind();

        void AutoBind()
        {
            if (db == null)
            {
                // 우선순위: Stage/Data → Resources/Data
                db = AssetDatabase.LoadAssetAtPath<CharacterStageDatabase>("Assets/_Project/Modules/Stage/Data/CharacterStageDatabase.asset")
                  ?? AssetDatabase.LoadAssetAtPath<CharacterStageDatabase>("Assets/Resources/Data/CharacterStageDatabase.asset");
            }
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Character Stage Tuner", EditorStyles.boldLabel);
            db = (CharacterStageDatabase)EditorGUILayout.ObjectField("Database", db, typeof(CharacterStageDatabase), false);

            if (db == null)
            {
                EditorGUILayout.HelpBox("CharacterStageDatabase 에셋을 바인딩하세요.", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Asset", GUILayout.Height(24))) Save();
                if (GUILayout.Button("Ping Asset", GUILayout.Width(100), GUILayout.Height(24)))
                    EditorGUIUtility.PingObject(db);
            }

            EditorGUILayout.Space(6);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (int i = 0; i < db.entries.Count; i++)
                DrawEntry(db.entries[i]);

            EditorGUILayout.EndScrollView();
        }

        void DrawEntry(CharacterStageEntry e)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(e.characterId, EditorStyles.boldLabel, GUILayout.Width(60));
                    var displayName = StoryMappings.CharacterIdToDisplayName(e.characterId);
                    EditorGUILayout.LabelField($"({displayName})", EditorStyles.miniLabel);
                }

                EditorGUI.BeginChangeCheck();
                e.spriteScale = EditorGUILayout.Slider("Scale",   e.spriteScale, 0.5f, 2.5f);
                e.offsetX     = EditorGUILayout.Slider("Offset X", e.offsetX,    -800f, 800f);
                e.offsetY     = EditorGUILayout.Slider("Offset Y", e.offsetY,    -3000f, 0f);
                e.pivotY      = EditorGUILayout.Slider("Pivot Y",  e.pivotY,     0f, 1f);
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(db);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reset", GUILayout.Width(60)))
                    {
                        e.spriteScale = 1f; e.offsetX = 0f; e.offsetY = 0f; e.pivotY = 0f;
                        EditorUtility.SetDirty(db);
                    }
                    if (GUILayout.Button("Apply to Live Slot (Play 모드)", GUILayout.Height(20)))
                        ApplyLive(e);
                }
            }
        }

        void ApplyLive(CharacterStageEntry e)
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Tuner] 플레이 모드에서만 라이브 적용 가능"); return; }
            // CharacterLayer 의 현재 슬롯에 강제 transform 재적용은 별도 API 없음 — 슬롯 강제 갱신.
            var layer = Stage.StageModule.Instance?.Character;
            if (layer == null) { Debug.LogWarning("[Tuner] CharacterLayer 없음"); return; }
            Debug.Log($"[Tuner] {e.characterId} 변경 적용 — 등장 중인 슬롯에 영향. 필요 시 캐릭터 재등장.");
            EditorUtility.SetDirty(db);
        }

        void Save()
        {
            if (db == null) return;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssetIfDirty(db);
            Debug.Log($"[CharacterStageTuner] 저장: {AssetDatabase.GetAssetPath(db)}");
        }
    }
}
#endif
