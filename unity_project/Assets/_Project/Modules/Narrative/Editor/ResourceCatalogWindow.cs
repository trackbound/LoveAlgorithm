using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LoveAlgo.Story.Data;

namespace LoveAlgo.Story.EditorTools
{
    /// <summary>
    /// ResourceCatalogSO 편집 윈도우 — 작가/개발자가 한글 키와 에셋을 시각적으로 관리.
    ///
    /// 기능:
    ///   - 카테고리 탭 (BG/CG/SD/Overlay/BGM/SFX/Characters/Emotes)
    ///   - 추가/삭제/이름 변경/에셋 변경
    ///   - 검색 (키로 필터)
    ///   - Orphan 감지 (Resources에 있는데 SO에 없는 에셋)
    ///   - 미할당 감지 (SO에 있는데 에셋 null)
    ///   - 검증 결과 한눈에
    ///
    /// 메뉴: Tools > Story > Resource Catalog Editor
    /// </summary>
    public class ResourceCatalogWindow : EditorWindow
    {
        const string CatalogPath = "Assets/Resources/ResourceCatalog.asset";

        ResourceCatalogSO _catalog;
        SerializedObject _serialized;
        Vector2 _scroll;
        string _search = "";
        Tab _tab = Tab.BG;

        enum Tab { BG, CG, SD, Overlay, BGM, SFX, Characters, Emotes }

        [MenuItem("Tools/Story/Resource Catalog Editor %#&c")] // Ctrl+Alt+Shift+C
        public static void Open()
        {
            var win = GetWindow<ResourceCatalogWindow>("Resource Catalog");
            win.minSize = new Vector2(720, 480);
            win.LoadCatalog();
        }

        void OnEnable() => LoadCatalog();

        void LoadCatalog()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<ResourceCatalogSO>(CatalogPath);
            if (_catalog != null) _serialized = new SerializedObject(_catalog);
        }

        void OnGUI()
        {
            if (_catalog == null)
            {
                EditorGUILayout.HelpBox($"{CatalogPath} 없음.", MessageType.Warning);
                if (GUILayout.Button("Default Catalog 생성"))
                {
                    ResourceCatalogMigrator.CreateDefaultCatalog();
                    LoadCatalog();
                }
                return;
            }

            _serialized.Update();

            DrawToolbar();
            DrawTabs();
            EditorGUILayout.Space(4);
            DrawTabContent();

            _serialized.ApplyModifiedProperties();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("🔍", GUILayout.Width(20));
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    ShowValidation();
                if (GUILayout.Button("Scan Orphans", EditorStyles.toolbarButton, GUILayout.Width(100)))
                    ScanOrphans();
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    SaveCatalog();
                if (GUILayout.Button("Migrate", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    ResourceCatalogMigrator.Migrate();
            }
        }

        void DrawTabs()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (Tab t in System.Enum.GetValues(typeof(Tab)))
                {
                    int count = GetCount(t);
                    string label = $"{t} ({count})";
                    if (GUILayout.Toggle(_tab == t, label, EditorStyles.miniButton))
                        _tab = t;
                }
            }
        }

        int GetCount(Tab t)
        {
            switch (t)
            {
                case Tab.BG: return _catalog.BG.Count;
                case Tab.CG: return _catalog.CG.Count;
                case Tab.SD: return _catalog.SD.Count;
                case Tab.Overlay: return _catalog.Overlays.Count;
                case Tab.BGM: return _catalog.BGM.Count;
                case Tab.SFX: return _catalog.SFX.Count;
                case Tab.Characters: return _catalog.Characters.Count;
                case Tab.Emotes: return _catalog.Emotes.Count;
                default: return 0;
            }
        }

        void DrawTabContent()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.BG:         DrawSpriteList(_catalog.BG, "BG"); break;
                case Tab.CG:         DrawSpriteList(_catalog.CG, "CG"); break;
                case Tab.SD:         DrawSpriteList(_catalog.SD, "SD"); break;
                case Tab.Overlay:    DrawSpriteList(_catalog.Overlays, "Overlay"); break;
                case Tab.BGM:        DrawAudioList(_catalog.BGM, "BGM"); break;
                case Tab.SFX:        DrawAudioList(_catalog.SFX, "SFX"); break;
                case Tab.Characters: DrawCharacterList(); break;
                case Tab.Emotes:     DrawEmoteList(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        // ── Sprite list (Id + Aliases 2컬럼) ──
        void DrawSpriteList(List<ResourceCatalogSO.SpriteEntry> list, string cat)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (!MatchesSpriteSearch(e)) continue;
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    GUILayout.Label($"#{i:D2}", GUILayout.Width(34));
                    EditorGUI.BeginChangeCheck();
                    string newId = EditorGUILayout.TextField(e.Id ?? "", GUILayout.Width(140));
                    string aliasJoined = e.Aliases != null ? string.Join(",", e.Aliases) : "";
                    string newAliasJoined = EditorGUILayout.TextField(aliasJoined, GUILayout.MinWidth(180));
                    var newSprite = (Sprite)EditorGUILayout.ObjectField(e.Sprite, typeof(Sprite), false, GUILayout.Width(160));
                    string newNote = EditorGUILayout.TextField(e.Note ?? "", GUILayout.MinWidth(100));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_catalog, "Edit Catalog Entry");
                        e.Id = newId; e.Sprite = newSprite; e.Note = newNote;
                        e.Aliases = SplitAliases(newAliasJoined);
                        EditorUtility.SetDirty(_catalog);
                    }

                    DrawSpriteBadge(e);

                    if (GUILayout.Button("－", GUILayout.Width(24)))
                    {
                        if (EditorUtility.DisplayDialog("삭제", $"'{e.Id}' 삭제할까요?", "삭제", "취소"))
                        {
                            Undo.RecordObject(_catalog, "Delete Catalog Entry");
                            list.RemoveAt(i);
                            EditorUtility.SetDirty(_catalog);
                            return;
                        }
                    }
                }
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button($"＋ Add {cat} Entry", GUILayout.Height(28)))
            {
                Undo.RecordObject(_catalog, "Add Catalog Entry");
                list.Add(new ResourceCatalogSO.SpriteEntry { Id = "(새Id)", Aliases = new string[0] });
                EditorUtility.SetDirty(_catalog);
            }
        }

        void DrawSpriteBadge(ResourceCatalogSO.SpriteEntry e)
        {
            var prev = GUI.color;
            if (string.IsNullOrEmpty(e.Id)) { GUI.color = Color.red; GUILayout.Label("Empty Id", GUILayout.Width(80)); }
            else if (e.Sprite == null) { GUI.color = new Color(1f, 0.7f, 0f); GUILayout.Label("No Sprite", GUILayout.Width(80)); }
            else { GUI.color = Color.green; GUILayout.Label("✓", GUILayout.Width(80)); }
            GUI.color = prev;
        }

        // ── Audio list (Id + Aliases 2컬럼) ──
        void DrawAudioList(List<ResourceCatalogSO.AudioEntry> list, string cat)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (!MatchesAudioSearch(e)) continue;
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    GUILayout.Label($"#{i:D2}", GUILayout.Width(34));
                    EditorGUI.BeginChangeCheck();
                    string newId = EditorGUILayout.TextField(e.Id ?? "", GUILayout.Width(140));
                    string aliasJoined = e.Aliases != null ? string.Join(",", e.Aliases) : "";
                    string newAliasJoined = EditorGUILayout.TextField(aliasJoined, GUILayout.MinWidth(180));
                    var newClip = (AudioClip)EditorGUILayout.ObjectField(e.Clip, typeof(AudioClip), false, GUILayout.Width(160));
                    string newNote = EditorGUILayout.TextField(e.Note ?? "", GUILayout.MinWidth(100));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_catalog, "Edit Catalog Entry");
                        e.Id = newId; e.Clip = newClip; e.Note = newNote;
                        e.Aliases = SplitAliases(newAliasJoined);
                        EditorUtility.SetDirty(_catalog);
                    }

                    var prev = GUI.color;
                    if (string.IsNullOrEmpty(e.Id)) { GUI.color = Color.red; GUILayout.Label("Empty Id", GUILayout.Width(80)); }
                    else if (e.Clip == null) { GUI.color = new Color(1f, 0.7f, 0f); GUILayout.Label("No Clip", GUILayout.Width(80)); }
                    else { GUI.color = Color.green; GUILayout.Label("✓", GUILayout.Width(80)); }
                    GUI.color = prev;

                    if (GUILayout.Button("－", GUILayout.Width(24)))
                    {
                        if (EditorUtility.DisplayDialog("삭제", $"'{e.Id}' 삭제할까요?", "삭제", "취소"))
                        {
                            Undo.RecordObject(_catalog, "Delete Catalog Entry");
                            list.RemoveAt(i);
                            EditorUtility.SetDirty(_catalog);
                            return;
                        }
                    }
                }
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button($"＋ Add {cat} Entry", GUILayout.Height(28)))
            {
                Undo.RecordObject(_catalog, "Add Catalog Entry");
                list.Add(new ResourceCatalogSO.AudioEntry { Id = "(새Id)", Aliases = new string[0] });
                EditorUtility.SetDirty(_catalog);
            }
        }

        static string[] SplitAliases(string joined)
            => string.IsNullOrEmpty(joined)
                ? new string[0]
                : joined.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();

        bool MatchesSpriteSearch(ResourceCatalogSO.SpriteEntry e)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            if (e == null) return false;
            if (MatchesSearch(e.Id)) return true;
            if (e.Aliases != null) foreach (var a in e.Aliases) if (MatchesSearch(a)) return true;
            return false;
        }
        bool MatchesAudioSearch(ResourceCatalogSO.AudioEntry e)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            if (e == null) return false;
            if (MatchesSearch(e.Id)) return true;
            if (e.Aliases != null) foreach (var a in e.Aliases) if (MatchesSearch(a)) return true;
            return false;
        }

        // ── Characters ──
        void DrawCharacterList()
        {
            for (int i = 0; i < _catalog.Characters.Count; i++)
            {
                var c = _catalog.Characters[i];
                if (!MatchesSearch(c.Id) && !MatchesSearch(c.DisplayName)) continue;
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    GUILayout.Label($"#{i:D2}", GUILayout.Width(34));
                    EditorGUI.BeginChangeCheck();
                    string id = EditorGUILayout.TextField(c.Id ?? "", GUILayout.Width(80));
                    string name = EditorGUILayout.TextField(c.DisplayName ?? "", GUILayout.Width(120));
                    string aliases = EditorGUILayout.TextField(c.Aliases != null ? string.Join(",", c.Aliases) : "",
                        GUILayout.MinWidth(180));
                    string note = EditorGUILayout.TextField(c.Note ?? "", GUILayout.MinWidth(120));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_catalog, "Edit Character");
                        c.Id = id; c.DisplayName = name;
                        c.Aliases = aliases.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                        c.Note = note;
                        EditorUtility.SetDirty(_catalog);
                    }

                    if (GUILayout.Button("－", GUILayout.Width(24)))
                    {
                        if (EditorUtility.DisplayDialog("삭제", $"'{c.DisplayName}' ({c.Id}) 삭제할까요?", "삭제", "취소"))
                        {
                            Undo.RecordObject(_catalog, "Delete Character");
                            _catalog.Characters.RemoveAt(i);
                            EditorUtility.SetDirty(_catalog);
                            return;
                        }
                    }
                }
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("＋ Add Character", GUILayout.Height(28)))
            {
                Undo.RecordObject(_catalog, "Add Character");
                _catalog.Characters.Add(new ResourceCatalogSO.CharacterEntry { Id = "c??", DisplayName = "(이름)" });
                EditorUtility.SetDirty(_catalog);
            }
        }

        // ── Emotes (Id=코드 + Aliases) ──
        void DrawEmoteList()
        {
            for (int i = 0; i < _catalog.Emotes.Count; i++)
            {
                var e = _catalog.Emotes[i];
                if (!MatchesEmoteSearch(e)) continue;
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    GUILayout.Label($"#{i:D2}", GUILayout.Width(34));
                    EditorGUI.BeginChangeCheck();
                    string id = EditorGUILayout.TextField(e.Id ?? "", GUILayout.Width(80));
                    string aliasJoined = e.Aliases != null ? string.Join(",", e.Aliases) : "";
                    string newAliasJoined = EditorGUILayout.TextField(aliasJoined, GUILayout.MinWidth(220));
                    string note = EditorGUILayout.TextField(e.Note ?? "", GUILayout.MinWidth(120));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_catalog, "Edit Emote");
                        e.Id = id; e.Note = note;
                        e.Aliases = SplitAliases(newAliasJoined);
                        EditorUtility.SetDirty(_catalog);
                    }

                    if (GUILayout.Button("－", GUILayout.Width(24)))
                    {
                        if (EditorUtility.DisplayDialog("삭제", $"'{e.Id}' 삭제할까요?", "삭제", "취소"))
                        {
                            Undo.RecordObject(_catalog, "Delete Emote");
                            _catalog.Emotes.RemoveAt(i);
                            EditorUtility.SetDirty(_catalog);
                            return;
                        }
                    }
                }
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("＋ Add Emote", GUILayout.Height(28)))
            {
                Undo.RecordObject(_catalog, "Add Emote");
                _catalog.Emotes.Add(new ResourceCatalogSO.EmoteEntry { Id = "_00", Aliases = new string[0] });
                EditorUtility.SetDirty(_catalog);
            }
        }

        bool MatchesEmoteSearch(ResourceCatalogSO.EmoteEntry e)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            if (e == null) return false;
            if (MatchesSearch(e.Id)) return true;
            if (e.Aliases != null) foreach (var a in e.Aliases) if (MatchesSearch(a)) return true;
            return false;
        }

        bool MatchesSearch(string s)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            if (string.IsNullOrEmpty(s)) return false;
            return s.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ── Toolbar actions ──
        void SaveCatalog()
        {
            EditorUtility.SetDirty(_catalog);
            AssetDatabase.SaveAssets();
            ResourceCatalogSO.ResetInstance();
            Debug.Log("[ResourceCatalog] 저장 완료");
        }

        void ShowValidation()
        {
            var issues = _catalog.Validate();
            string msg = issues.Count == 0
                ? "✓ 모든 검증 통과"
                : $"문제 {issues.Count}개:\n\n" + string.Join("\n", issues);
            EditorUtility.DisplayDialog("Resource Catalog 검증", msg, "확인");
        }

        void ScanOrphans()
        {
            var orphans = new List<string>();
            ScanFolder("Assets/Resources/BG", _catalog.BG.Select(e => e.Sprite).Where(s => s != null), orphans, "BG");
            ScanFolder("Assets/Resources/CG", _catalog.CG.Select(e => e.Sprite).Where(s => s != null), orphans, "CG");
            ScanFolder("Assets/Resources/SD", _catalog.SD.Select(e => e.Sprite).Where(s => s != null), orphans, "SD");
            ScanFolder("Assets/Resources/Overlay", _catalog.Overlays.Select(e => e.Sprite).Where(s => s != null), orphans, "Overlay");
            ScanAudioFolder("Assets/Resources/Audio/BGM", _catalog.BGM.Select(e => e.Clip).Where(c => c != null), orphans, "BGM");
            ScanAudioFolder("Assets/Resources/Audio/SFX", _catalog.SFX.Select(e => e.Clip).Where(c => c != null), orphans, "SFX");

            string msg = orphans.Count == 0
                ? "✓ Orphan 없음 — 모든 Resources 에셋이 SO에 등록됨"
                : $"카탈로그에 없는 에셋 {orphans.Count}개:\n\n" + string.Join("\n", orphans);
            EditorUtility.DisplayDialog("Orphan 스캔", msg, "확인");
        }

        void ScanFolder(string folder, IEnumerable<Sprite> referenced, List<string> orphans, string cat)
        {
            if (!Directory.Exists(folder)) return;
            var refSet = new HashSet<string>(referenced.Select(s => AssetDatabase.GetAssetPath(s)));
            var assets = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!refSet.Contains(path)) orphans.Add($"[{cat}] {path}");
            }
        }

        void ScanAudioFolder(string folder, IEnumerable<AudioClip> referenced, List<string> orphans, string cat)
        {
            if (!Directory.Exists(folder)) return;
            var refSet = new HashSet<string>(referenced.Select(c => AssetDatabase.GetAssetPath(c)));
            var assets = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!refSet.Contains(path)) orphans.Add($"[{cat}] {path}");
            }
        }
    }
}
