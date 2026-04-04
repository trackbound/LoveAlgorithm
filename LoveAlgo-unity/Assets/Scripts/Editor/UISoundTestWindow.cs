using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// UI 사운드 테스트 에디터 윈도우 — Play 모드 없이 AudioClip 미리듣기
    /// </summary>
    public class UISoundTestWindow : EditorWindow
    {
        [MenuItem("Tools/LoveAlgo/UI Sound Test")]
        static void Open() => GetWindow<UISoundTestWindow>("UI Sound Test");

        Vector2 scroll;
        AudioClip customClip;

        // 에디터에서 오디오 재생/정지 (내부 API 리플렉션)
        static void PlayClipInEditor(AudioClip clip)
        {
            if (clip == null) return;
            StopAllClipsInEditor();
            var asm = typeof(AudioImporter).Assembly;
            var type = asm.GetType("UnityEditor.AudioUtil");
            var method = type?.GetMethod("PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            method?.Invoke(null, new object[] { clip, 0, false });
        }

        static void StopAllClipsInEditor()
        {
            var asm = typeof(AudioImporter).Assembly;
            var type = asm.GetType("UnityEditor.AudioUtil");
            var method = type?.GetMethod("StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public);
            method?.Invoke(null, null);
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("UI 사운드 미리듣기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Play 모드 없이 AudioClip을 재생합니다.\nAssets/Resources/Audio/ 경로의 클립을 자동 탐색합니다.", MessageType.Info);

            EditorGUILayout.Space(6);

            // ── UI SFX 클립 ──
            DrawSection("UI SFX", new[]
            {
                ("Hover",          "Audio/UI/ui_hover"),
                ("Click",          "Audio/UI/ui_click"),
                ("Typing",         "Audio/UI/vn_type"),
                ("Dialogue Next",  "Audio/UI/ui_dialogue_next"),
                ("Choice Select",  "Audio/UI/ui_choice_select"),
                ("Choice Hover",   "Audio/UI/ui_choice_hover"),
                ("Choice Appear",  "Audio/UI/ui_choice_appear"),
                ("Popup Open",     "Audio/UI/ui_popup_open"),
                ("Popup Close",    "Audio/UI/ui_popup_close"),
                ("Save Complete",  "Audio/UI/ui_save"),
                ("Load Complete",  "Audio/UI/ui_load"),
            });

            EditorGUILayout.Space(6);

            // ── SFX ──
            DrawSection("Game SFX", "Audio/SFX");

            EditorGUILayout.Space(6);

            // ── BGM ──
            DrawSection("BGM", "Audio/BGM");

            EditorGUILayout.Space(10);

            // ── 커스텀 클립 ──
            EditorGUILayout.LabelField("커스텀 클립", EditorStyles.boldLabel);
            customClip = (AudioClip)EditorGUILayout.ObjectField("AudioClip", customClip, typeof(AudioClip), false);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ Play") && customClip != null) PlayClipInEditor(customClip);
            if (GUILayout.Button("■ Stop")) StopAllClipsInEditor();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        void DrawSection(string label, (string name, string path)[] clips)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            foreach (var (name, path) in clips)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(name, GUILayout.Width(120));
                var clip = Resources.Load<AudioClip>(path);
                if (clip != null)
                {
                    if (GUILayout.Button("▶", GUILayout.Width(30))) PlayClipInEditor(clip);
                    EditorGUILayout.LabelField(clip.name, EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField($"({path} 없음)", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawSection(string label, string folder)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            var clips = Resources.LoadAll<AudioClip>(folder);
            if (clips.Length == 0)
            {
                EditorGUILayout.LabelField($"  ({folder}/ 에 클립 없음)", EditorStyles.miniLabel);
                return;
            }
            foreach (var clip in clips)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("▶", GUILayout.Width(30))) PlayClipInEditor(clip);
                EditorGUILayout.LabelField(clip.name);
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
