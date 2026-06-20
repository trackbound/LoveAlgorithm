using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using LoveAlgo.Core; // JsonSaveStore, SaveData, MetaProgressStore
using LoveAlgo.UI;   // FirstLaunchFlag

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 개발 중 테스트로 쌓인 세이브 슬롯을 한눈에 보고 정리하는 편의 에디터 창
    /// (Tools/Debug/Save Slot Manager). persistentDataPath/saves 폴더를 직접 스캔해
    /// 슬롯별 라벨·저장시각·크기를 표시하고, 개별/전체 삭제를 제공한다. 세이브와 분리된
    /// 앱 영속 플래그(첫실행·프롤로그 회차 카운터)도 같은 창에서 초기화한다.
    /// 모든 파괴적 동작은 확인 다이얼로그로 보호. 에디터 전용(빌드 런타임 무관).
    /// </summary>
    public class SaveSlotManagerWindow : EditorWindow
    {
        // save_{n}.json 파일명에서 슬롯 번호 추출.
        static readonly Regex SlotPattern = new(@"^save_(\d+)\.json$", RegexOptions.Compiled);

        struct SlotInfo
        {
            public int slot;
            public string label;     // chapterLabel
            public string savedAt;   // 로컬 시각 표시 문자열
            public long bytes;
            public bool hasThumbnail;
            public string filePath;
        }

        List<SlotInfo> _slots = new();
        Vector2 _scroll;

        static string SavesDir => Path.Combine(Application.persistentDataPath, "saves");

        [MenuItem("Tools/Debug/Save Slot Manager (세이브 슬롯 관리)")]
        public static void Open()
        {
            var win = GetWindow<SaveSlotManagerWindow>(false, "Save Slots", true);
            win.minSize = new Vector2(460, 420);
            win.Refresh();
            win.Show();
        }

        void OnEnable() => Refresh();

        void Refresh()
        {
            _slots.Clear();
            var dir = SavesDir;
            if (!Directory.Exists(dir)) return;

            foreach (var path in Directory.GetFiles(dir, "save_*.json"))
            {
                var m = SlotPattern.Match(Path.GetFileName(path));
                if (!m.Success) continue;
                if (!int.TryParse(m.Groups[1].Value, out int slot)) continue;

                var info = new SlotInfo { slot = slot, filePath = path };
                try
                {
                    var fi = new FileInfo(path);
                    info.bytes = fi.Length;
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    if (data != null)
                    {
                        info.label = string.IsNullOrEmpty(data.chapterLabel) ? "(라벨 없음)" : data.chapterLabel;
                        info.savedAt = FormatUtc(data.savedAtUtc);
                    }
                    else info.label = "(파싱 실패 — 손상?)";
                }
                catch (Exception e)
                {
                    info.label = "(읽기 실패: " + e.Message + ")";
                }

                var thumb = Path.Combine(dir, JsonSaveStore.ThumbnailFileFor(slot));
                info.hasThumbnail = File.Exists(thumb);
                _slots.Add(info);
            }

            _slots = _slots.OrderBy(s => s.slot).ToList();
        }

        static string FormatUtc(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "-";
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            return iso;
        }

        static string HumanSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024f).ToString("F1") + " KB";
            return (bytes / (1024f * 1024f)).ToString("F1") + " MB";
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("세이브 슬롯 관리 (개발용)", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(SavesDir, EditorStyles.miniLabel, GUILayout.Height(16));
                if (GUILayout.Button("폴더 열기", GUILayout.Width(80)))
                    RevealSavesFolder();
                if (GUILayout.Button("새로고침", GUILayout.Width(70)))
                    Refresh();
            }

            EditorGUILayout.Space(4);

            // ── 슬롯 목록 ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"세이브 파일 ({_slots.Count})", EditorStyles.miniBoldLabel);

                if (!Directory.Exists(SavesDir))
                {
                    EditorGUILayout.HelpBox("saves 폴더가 아직 없습니다(저장 기록 없음).", MessageType.Info);
                }
                else if (_slots.Count == 0)
                {
                    EditorGUILayout.HelpBox("세이브 슬롯이 없습니다.", MessageType.Info);
                }
                else
                {
                    foreach (var s in _slots)
                        DrawSlotRow(s);
                }
            }

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(_slots.Count == 0))
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
                if (GUILayout.Button($"모든 세이브 슬롯 삭제 ({_slots.Count}개)", GUILayout.Height(30)))
                    DeleteAllSlots();
                GUI.backgroundColor = prev;
            }

            EditorGUILayout.Space(10);

            // ── 앱 영속 플래그(세이브와 분리) ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("앱 영속 플래그 (세이브 삭제와 무관)", EditorStyles.miniBoldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"첫실행 인트로 본 적: {(FirstLaunchFlag.Seen ? "예" : "아니오")}");
                    if (GUILayout.Button("초기화", GUILayout.Width(70)))
                    {
                        FirstLaunchFlag.Reset();
                        Debug.Log("[SaveSlots] 첫실행 플래그 초기화 — 다음 실행은 인트로를 표시합니다.");
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    int clears = MetaProgressStore.GetInt(MetaProgressStore.PrologueClears);
                    EditorGUILayout.LabelField($"프롤로그 회차(완주) 카운터: {clears}");
                    if (GUILayout.Button("초기화", GUILayout.Width(70)))
                    {
                        MetaProgressStore.DeleteKey(MetaProgressStore.PrologueClears);
                        Debug.Log("[SaveSlots] 프롤로그 회차 카운터 초기화.");
                    }
                }
            }

            EditorGUILayout.Space(6);

            var prev2 = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.85f, 0.3f, 0.3f);
            if (GUILayout.Button("모든 PlayerPrefs 삭제 (설정·플래그 전부)", GUILayout.Height(26)))
                DeleteAllPlayerPrefs();
            GUI.backgroundColor = prev2;
            EditorGUILayout.HelpBox(
                "주의: 세이브 파일은 PlayerPrefs가 아니므로 위 버튼으로 지워지지 않습니다. " +
                "이 버튼은 설정·첫실행·회차 카운터 등 모든 PlayerPrefs를 한 번에 비웁니다.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        void DrawSlotRow(SlotInfo s)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                bool isAuto = s.slot == JsonSaveStore.AutoSaveSlot;
                string name = isAuto ? "슬롯 0 (자동저장)" : $"슬롯 {s.slot}";
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        $"{s.label}   ·   {s.savedAt}   ·   {HumanSize(s.bytes)}" +
                        (s.hasThumbnail ? "   ·   썸네일 있음" : ""),
                        EditorStyles.miniLabel);
                }
                if (GUILayout.Button("삭제", GUILayout.Width(60), GUILayout.Height(32)))
                    DeleteSlot(s.slot);
            }
        }

        void DeleteSlot(int slot)
        {
            if (!EditorUtility.DisplayDialog("세이브 슬롯 삭제",
                    $"슬롯 {slot}의 세이브 파일과 썸네일을 삭제할까요?\n되돌릴 수 없습니다.",
                    "삭제", "취소"))
                return;

            DeleteSlotFiles(slot);
            Debug.Log($"[SaveSlots] 슬롯 {slot} 삭제 완료.");
            Refresh();
        }

        void DeleteAllSlots()
        {
            int n = _slots.Count;
            if (!EditorUtility.DisplayDialog("모든 세이브 슬롯 삭제",
                    $"{n}개의 세이브 슬롯(파일·썸네일)을 모두 삭제할까요?\n되돌릴 수 없습니다.",
                    $"{n}개 모두 삭제", "취소"))
                return;

            foreach (var s in _slots.ToList())
                DeleteSlotFiles(s.slot);
            Debug.Log($"[SaveSlots] 세이브 슬롯 {n}개 전체 삭제 완료.");
            Refresh();
        }

        // 슬롯 JSON + 규약 썸네일 파일을 함께 제거.
        static void DeleteSlotFiles(int slot)
        {
            JsonSaveStore.Delete(slot);
            try
            {
                var thumb = Path.Combine(SavesDir, JsonSaveStore.ThumbnailFileFor(slot));
                if (File.Exists(thumb)) File.Delete(thumb);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSlots] 슬롯 {slot} 썸네일 삭제 실패: {e.Message}");
            }
        }

        void DeleteAllPlayerPrefs()
        {
            if (!EditorUtility.DisplayDialog("모든 PlayerPrefs 삭제",
                    "설정·첫실행·회차 카운터 등 모든 PlayerPrefs를 삭제할까요?\n되돌릴 수 없습니다.\n(세이브 파일은 영향 없음)",
                    "모두 삭제", "취소"))
                return;

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[SaveSlots] 모든 PlayerPrefs 삭제 완료.");
            Repaint();
        }

        void RevealSavesFolder()
        {
            var dir = SavesDir;
            if (Directory.Exists(dir))
                EditorUtility.RevealInFinder(dir);
            else
                Debug.LogWarning($"[SaveSlots] saves 폴더가 아직 없습니다: {dir}");
        }
    }
}
