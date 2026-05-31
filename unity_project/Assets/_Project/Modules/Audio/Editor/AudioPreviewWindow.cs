#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.AudioEditor
{
    /// <summary>
    /// BGM/SFX 클립 즉시 미리 듣기.
    /// 메뉴: Tools > LoveAlgo > Audio > Preview
    ///
    /// Resources/Audio/BGM, Resources/Audio/SFX 의 클립을 표 형태로 나열.
    /// Play 버튼 → 에디터 내부 AudioUtil로 재생 (게임 플레이 모드 불필요).
    /// </summary>
    public class AudioPreviewWindow : EditorWindow
    {
        static readonly string[] Folders =
        {
            "Assets/Resources/Audio/BGM",
            "Assets/Resources/Audio/SFX",
            "Assets/Resources/Audio/UI",
        };

        int tab;
        string search = "";
        Vector2 scroll;
        List<string> clipPaths = new();

        [MenuItem("Tools/LoveAlgo/Audio/Preview")]
        public static void Open()
        {
            var w = GetWindow<AudioPreviewWindow>("Audio Preview");
            w.minSize = new Vector2(520, 480);
            w.Refresh();
            w.Show();
        }

        void OnEnable() => Refresh();

        void OnGUI()
        {
            EditorGUILayout.LabelField("Audio Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < Folders.Length; i++)
                {
                    var name = Path.GetFileName(Folders[i]);
                    if (GUILayout.Toggle(i == tab, name, EditorStyles.toolbarButton) && i != tab)
                    {
                        tab = i;
                        Refresh();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Search", GUILayout.Width(50));
                var newSearch = EditorGUILayout.TextField(search);
                if (newSearch != search) { search = newSearch; }
                if (GUILayout.Button("Stop All", GUILayout.Width(80))) StopAll();
                if (GUILayout.Button("Refresh", GUILayout.Width(80))) Refresh();
            }

            EditorGUILayout.LabelField($"{FilteredPaths().Count}개 클립", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var path in FilteredPaths())
                DrawClipRow(path);
            EditorGUILayout.EndScrollView();
        }

        List<string> FilteredPaths()
        {
            if (string.IsNullOrEmpty(search)) return clipPaths;
            var s = search.ToLowerInvariant();
            return clipPaths.FindAll(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(s));
        }

        void Refresh()
        {
            clipPaths.Clear();
            var dir = Folders[tab];
            if (!Directory.Exists(dir)) return;
            foreach (var ext in new[] { "*.mp3", "*.wav", "*.ogg" })
                clipPaths.AddRange(Directory.GetFiles(dir, ext));
            clipPaths.Sort();
        }

        void DrawClipRow(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("▶", GUILayout.Width(28), GUILayout.Height(22)))
                {
                    StopAll();
                    if (clip != null) PlayClip(clip);
                }
                if (GUILayout.Button("■", GUILayout.Width(28), GUILayout.Height(22))) StopAll();

                var name = Path.GetFileNameWithoutExtension(path);
                EditorGUILayout.LabelField(name, GUILayout.Width(180));
                if (clip != null)
                    EditorGUILayout.LabelField($"{clip.length:F1}s · {clip.channels}ch · {clip.frequency}Hz", EditorStyles.miniLabel);

                if (GUILayout.Button("Ping", GUILayout.Width(50)))
                    EditorGUIUtility.PingObject(clip);
            }
        }

        // ─── Unity 내부 AudioUtil 리플렉션 ─────────────
        // 패키지화되지 않은 비공개 API. 에디터 전용이라 안전하게 사용.
        static MethodInfo MiPlay() => GetMi("PlayPreviewClip", typeof(AudioClip), typeof(int), typeof(bool))
                                   ?? GetMi("PlayClip", typeof(AudioClip), typeof(int), typeof(bool))
                                   ?? GetMi("PlayClip", typeof(AudioClip));
        static MethodInfo MiStop() => GetMi("StopAllPreviewClips") ?? GetMi("StopAllClips");

        static MethodInfo GetMi(string name, params System.Type[] argTypes)
        {
            var t = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");
            if (t == null) return null;
            return t.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, argTypes, null);
        }

        static void PlayClip(AudioClip clip)
        {
            try
            {
                var mi = MiPlay();
                if (mi == null) { Debug.LogWarning("[AudioPreview] AudioUtil 메서드를 찾지 못했습니다 (Unity 버전 차이)"); return; }
                var ps = mi.GetParameters();
                if (ps.Length == 3) mi.Invoke(null, new object[] { clip, 0, false });
                else mi.Invoke(null, new object[] { clip });
            }
            catch (System.Exception e) { Debug.LogWarning($"[AudioPreview] 재생 실패: {e.Message}"); }
        }

        static void StopAll()
        {
            try { MiStop()?.Invoke(null, null); }
            catch (System.Exception e) { Debug.LogWarning($"[AudioPreview] 정지 실패: {e.Message}"); }
        }
    }
}
#endif
