#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor
{
    /// <summary>
    /// Resources 폴더 시각 자산 썸네일 브라우저.
    /// 메뉴: Tools > LoveAlgo > Resources > Browser
    ///
    /// 폴더 탭(BG / Characters / CG / SD / Overlay) 전환 + 썸네일 그리드.
    /// 클릭 시 Project 창에서 ping. 더블클릭 시 에디터로 열기.
    /// </summary>
    public class ResourceBrowserWindow : EditorWindow
    {
        static readonly string[] Folders =
        {
            "Assets/Resources/BG",
            "Assets/Resources/Characters",
            "Assets/Resources/CG",
            "Assets/Resources/SD",
            "Assets/Resources/Overlay",
        };

        int tab;
        string search = "";
        Vector2 scroll;
        float thumbSize = 96f;

        [MenuItem("Tools/LoveAlgo/Resources/Browser")]
        public static void Open()
        {
            var w = GetWindow<ResourceBrowserWindow>("Resource Browser");
            w.minSize = new Vector2(640, 480);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Resource Browser", EditorStyles.boldLabel);

            // 탭
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < Folders.Length; i++)
                {
                    var name = Path.GetFileName(Folders[i]);
                    var style = i == tab ? EditorStyles.toolbarButton : EditorStyles.toolbar;
                    var prev = GUI.backgroundColor;
                    if (i == tab) GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
                    if (GUILayout.Button(name, EditorStyles.toolbarButton)) tab = i;
                    GUI.backgroundColor = prev;
                }
            }

            // 검색 + 크기
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Search", GUILayout.Width(50));
                search = EditorGUILayout.TextField(search);
                EditorGUILayout.LabelField("Size", GUILayout.Width(36));
                thumbSize = EditorGUILayout.Slider(thumbSize, 48f, 240f);
            }

            EditorGUILayout.Space(4);
            DrawGrid(Folders[tab]);
        }

        void DrawGrid(string folder)
        {
            if (!Directory.Exists(folder))
            {
                EditorGUILayout.HelpBox($"폴더 없음: {folder}", MessageType.Warning);
                return;
            }

            var files = new List<string>(Directory.GetFiles(folder, "*.png"));
            files.Sort();
            if (!string.IsNullOrEmpty(search))
                files.RemoveAll(f => !Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains(search.ToLowerInvariant()));

            EditorGUILayout.LabelField($"{files.Count}개 (folder: {folder})", EditorStyles.miniLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 20f) / (thumbSize + 12f)));
            int i = 0;
            while (i < files.Count)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int c = 0; c < perRow && i < files.Count; c++, i++)
                        DrawItem(files[i]);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawItem(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var tex = sprite != null ? sprite.texture : AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(thumbSize + 8f)))
            {
                var rect = GUILayoutUtility.GetRect(thumbSize, thumbSize, GUILayout.Width(thumbSize), GUILayout.Height(thumbSize));
                if (tex != null) GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
                else EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

                var ev = Event.current;
                if (ev.type == EventType.MouseDown && rect.Contains(ev.mousePosition))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (ev.clickCount == 2) AssetDatabase.OpenAsset(obj);
                    else EditorGUIUtility.PingObject(obj);
                    ev.Use();
                }

                EditorGUILayout.LabelField(name, EditorStyles.miniLabel, GUILayout.Width(thumbSize));
            }
        }
    }
}
#endif
