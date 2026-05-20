#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.SaveEditor
{
    /// <summary>
    /// 세이브 데이터 + PlayerPrefs 관리 헬퍼.
    /// 메뉴: Tools > LoveAlgo > Save > Admin
    ///
    /// 다루는 영역:
    /// 1. {persistentDataPath}/Saves/ — save_NN.json + save_NN_thumb.png + save_pending_thumb.png
    /// 2. PlayerPrefs — 설정값(볼륨, 텍스트 속도, Voice_*, 풀스크린 등)
    /// </summary>
    public class SaveAdminWindow : EditorWindow
    {
        const string SaveFolderName = "Saves";

        Vector2 scroll;
        SlotInfo[] slots;
        long totalBytes;
        int totalCount;

        [MenuItem("Tools/LoveAlgo/Save/Admin")]
        public static void Open()
        {
            var w = GetWindow<SaveAdminWindow>("Save Admin");
            w.minSize = new Vector2(480, 520);
            w.Refresh();
            w.Show();
        }

        void OnEnable() => Refresh();

        string SaveDir => Path.Combine(Application.persistentDataPath, SaveFolderName);

        void OnGUI()
        {
            EditorGUILayout.LabelField("Save Data Admin", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ─── 경로 표시 + Open 버튼 ───
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(SaveDir, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Open", GUILayout.Width(60))) OpenSaveFolder();
                if (GUILayout.Button("Refresh", GUILayout.Width(70))) Refresh();
            }

            EditorGUILayout.LabelField($"파일 {totalCount}개 · {FormatBytes(totalBytes)}");
            EditorGUILayout.Space(8);

            // ─── 슬롯 목록 ───
            EditorGUILayout.LabelField("Save Slots", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(200));
            if (slots == null || slots.Length == 0)
            {
                EditorGUILayout.HelpBox("저장된 슬롯이 없습니다.", MessageType.Info);
            }
            else
            {
                foreach (var s in slots) DrawSlot(s);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);

            // ─── 일괄 작업 ───
            EditorGUILayout.LabelField("일괄 작업", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (RedButton("전체 세이브 삭제", "Saves 폴더 전체"))
                    ConfirmAndDeleteAllSaves();
                if (RedButton("PlayerPrefs 전체 삭제", "설정 등"))
                    ConfirmAndDeletePlayerPrefs();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (RedButton("모두 삭제 (세이브 + PlayerPrefs)", "공장 초기화"))
                    ConfirmAndNuke();
                if (GUILayout.Button("Pending 썸네일 삭제", GUILayout.Height(28)))
                    DeletePendingThumb();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("플레이 모드 중에는 변경이 즉시 반영되지 않을 수 있습니다. 안전을 위해 플레이 모드를 빠진 상태에서 실행하세요.", MessageType.None);
        }

        // ─── 슬롯 그리기 ────────────────────────────────────────
        void DrawSlot(SlotInfo s)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string label = s.slot == 0 ? $"Auto (slot {s.slot})" : $"Slot {s.slot}";
                EditorGUILayout.LabelField(label, GUILayout.Width(90));

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField($"json: {(s.hasJson ? FormatBytes(s.jsonBytes) : "—")}    thumb: {(s.hasThumb ? "✓" : "—")}");
                    if (!string.IsNullOrEmpty(s.preview))
                        EditorGUILayout.LabelField(s.preview, EditorStyles.miniLabel);
                }

                if (GUILayout.Button("Delete", GUILayout.Width(72), GUILayout.Height(24)))
                {
                    if (EditorUtility.DisplayDialog("삭제 확인", $"슬롯 {s.slot} 의 세이브와 썸네일을 삭제하시겠습니까?", "삭제", "취소"))
                    {
                        DeleteSlot(s.slot);
                        Refresh();
                    }
                }
            }
        }

        // ─── 데이터 수집 ───────────────────────────────────────
        void Refresh()
        {
            totalBytes = 0;
            totalCount = 0;
            if (!Directory.Exists(SaveDir))
            {
                slots = new SlotInfo[0];
                return;
            }

            var list = new System.Collections.Generic.List<SlotInfo>();
            foreach (var file in Directory.GetFiles(SaveDir))
            {
                totalCount++;
                totalBytes += new FileInfo(file).Length;
            }

            // 슬롯 추출 (0~30 정도 스캔)
            for (int i = 0; i <= 30; i++)
            {
                var jsonPath = Path.Combine(SaveDir, $"save_{i:D2}.json");
                var thumbPath = Path.Combine(SaveDir, $"save_{i:D2}_thumb.png");
                bool hasJson = File.Exists(jsonPath);
                bool hasThumb = File.Exists(thumbPath);
                if (!hasJson && !hasThumb) continue;

                var s = new SlotInfo { slot = i, hasJson = hasJson, hasThumb = hasThumb };
                if (hasJson)
                {
                    s.jsonBytes = new FileInfo(jsonPath).Length;
                    s.preview = ExtractPreview(jsonPath);
                }
                list.Add(s);
            }

            slots = list.ToArray();
        }

        static string ExtractPreview(string jsonPath)
        {
            try
            {
                // 가벼운 발췌 — 정식 파서 안 쓰고 키 기반 grep
                var text = File.ReadAllText(jsonPath);
                string day = Grep(text, "\"day\"");
                string phase = Grep(text, "\"phase\"");
                string chapter = Grep(text, "\"chapterName\"");
                string saved = Grep(text, "\"savedAtIso\"");
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(chapter)) parts.Add($"\"{chapter}\"");
                if (!string.IsNullOrEmpty(day)) parts.Add($"day {day}");
                if (!string.IsNullOrEmpty(phase)) parts.Add($"phase {phase}");
                if (!string.IsNullOrEmpty(saved)) parts.Add(saved);
                return string.Join("  ·  ", parts);
            }
            catch
            {
                return "(parse failed)";
            }
        }

        static string Grep(string text, string keyQuoted)
        {
            int i = text.IndexOf(keyQuoted, System.StringComparison.Ordinal);
            if (i < 0) return null;
            int colon = text.IndexOf(':', i);
            if (colon < 0) return null;
            int start = colon + 1;
            while (start < text.Length && (text[start] == ' ' || text[start] == '"')) start++;
            int end = start;
            while (end < text.Length && text[end] != ',' && text[end] != '"' && text[end] != '\n' && text[end] != '}') end++;
            return text.Substring(start, end - start).Trim();
        }

        // ─── 삭제 동작 ───────────────────────────────────────
        void DeleteSlot(int slot)
        {
            var json = Path.Combine(SaveDir, $"save_{slot:D2}.json");
            var thumb = Path.Combine(SaveDir, $"save_{slot:D2}_thumb.png");
            if (File.Exists(json)) File.Delete(json);
            if (File.Exists(thumb)) File.Delete(thumb);
            Debug.Log($"[SaveAdmin] 슬롯 {slot} 삭제됨.");
        }

        void ConfirmAndDeleteAllSaves()
        {
            if (!EditorUtility.DisplayDialog("전체 세이브 삭제",
                $"{SaveDir}\n폴더의 모든 파일을 삭제합니다. 계속하시겠습니까?", "삭제", "취소")) return;
            DeleteAllSaves();
            Refresh();
        }

        void DeleteAllSaves()
        {
            if (!Directory.Exists(SaveDir)) return;
            foreach (var file in Directory.GetFiles(SaveDir))
                File.Delete(file);
            Debug.Log($"[SaveAdmin] 전체 세이브 삭제됨: {SaveDir}");
        }

        void ConfirmAndDeletePlayerPrefs()
        {
            if (!EditorUtility.DisplayDialog("PlayerPrefs 삭제",
                "PlayerPrefs(설정 등)를 전부 삭제합니다. 계속하시겠습니까?", "삭제", "취소")) return;
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[SaveAdmin] PlayerPrefs 삭제됨.");
        }

        void ConfirmAndNuke()
        {
            if (!EditorUtility.DisplayDialog("공장 초기화",
                "세이브 파일과 PlayerPrefs를 모두 삭제합니다. 정말 진행하시겠습니까?", "삭제", "취소")) return;
            DeleteAllSaves();
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Refresh();
            Debug.Log("[SaveAdmin] 공장 초기화 완료 (Saves + PlayerPrefs).");
        }

        void DeletePendingThumb()
        {
            var p = Path.Combine(SaveDir, "save_pending_thumb.png");
            if (File.Exists(p))
            {
                File.Delete(p);
                Debug.Log($"[SaveAdmin] pending 썸네일 삭제: {p}");
                Refresh();
            }
        }

        void OpenSaveFolder()
        {
            if (!Directory.Exists(SaveDir))
                Directory.CreateDirectory(SaveDir);
            EditorUtility.RevealInFinder(SaveDir);
        }

        // ─── UI 헬퍼 ────────────────────────────────────────
        static bool RedButton(string label, string tooltip)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            bool clicked = GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(28));
            GUI.backgroundColor = prev;
            return clicked;
        }

        static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1024.0 / 1024.0:F2} MB";
        }

        struct SlotInfo
        {
            public int slot;
            public bool hasJson;
            public bool hasThumb;
            public long jsonBytes;
            public string preview;
        }
    }
}
#endif
