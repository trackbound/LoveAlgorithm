using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 캐릭터 폴더 하위의 스프라이트(PNG) Importer의 Pixels Per Unit을 일괄 변경하는 에디터 윈도우
    /// - 메뉴: LoveAlgo/Tools/Set Character Sprite PPU
    /// - 기능: 선택 캐릭터 또는 전체에 대해 지정한 PPU를 적용, 변경 전 PPU를 백업/리포트 생성
    /// </summary>
    public class CharacterPPUSetter : EditorWindow
    {
        static readonly string[] Characters = { "Roa", "Yeun", "Daeun", "Bom", "Heewon" };
        Vector2 scrollPos;
        int ppu = 200;
        bool applyToAll = true;
        bool includeSubfolders = true;
        bool overwriteAll = true;
        bool backupBefore = true;
        string reportPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "character_ppu_report.txt");
        string backupPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "character_ppu_backup.json");
        bool[] selected;

        [MenuItem("LoveAlgo/Tools/Set Character Sprite PPU", priority = 310)]
        public static void ShowWindow()
        {
            var win = GetWindow<CharacterPPUSetter>("Character PPU Setter");
            win.minSize = new Vector2(420, 320);
            win.selected = new bool[Characters.Length];
            win.Show();
        }

        void OnGUI()
        {
            GUILayout.Label("Character Sprite PPU Setter", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            ppu = EditorGUILayout.IntField(new GUIContent("Pixels Per Unit", "적용할 PPU 값"), ppu);
            includeSubfolders = EditorGUILayout.Toggle(new GUIContent("Include Subfolders", "하위 폴더 PNG 포함 여부"), includeSubfolders);
            overwriteAll = EditorGUILayout.Toggle(new GUIContent("Overwrite All", "이미 PPU가 설정되어 있어도 덮어쓸지"), overwriteAll);
            backupBefore = EditorGUILayout.Toggle(new GUIContent("Backup Before Apply", "적용 전 현재 PPU 값을 백업 파일에 저장"), backupBefore);

            EditorGUILayout.Space();
            applyToAll = EditorGUILayout.Toggle(new GUIContent("Apply To All Characters", "모든 캐릭터에 적용할지"), applyToAll);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Characters", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(120));
            for (int i = 0; i < Characters.Length; i++)
            {
                if (selected == null || selected.Length != Characters.Length) selected = new bool[Characters.Length];
                selected[i] = EditorGUILayout.ToggleLeft($"{Characters[i]}", selected[i]);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan and Report")) ScanAndReport();
            if (GUILayout.Button("Apply PPU")) ApplyPPUConfirmed();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("사용법: PPU값 입력 → Apply To All 체크 또는 개별 캐릭터 선택 → Apply PPU 버튼 클릭\n변경 전 backup 파일이 생성되며, 처리 결과는 'character_ppu_report.txt'에 기록됩니다.", MessageType.Info);
        }

        void ScanAndReport()
        {
            var report = new List<string>();
            report.Add("Character PPU Scan Report");
            report.Add("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.Add("");

            int total = 0;

            IEnumerable<string> targets = GetTargetCharacters();
            foreach (var c in targets)
            {
                string folder = Path.Combine("Assets/Resources/Characters", c);
                report.Add($"-- {c} --");
                if (!Directory.Exists(folder))
                {
                    report.Add("  (folder missing)");
                    report.Add("");
                    continue;
                }

                var pngs = Directory.GetFiles(folder, "*.png", includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                if (pngs.Length == 0)
                {
                    report.Add("  (no png files)");
                    report.Add("");
                    continue;
                }

                foreach (var f in pngs)
                {
                    total++;
                    string assetPath = f.Replace(Path.DirectorySeparatorChar, '/');
                    string resourcePath = assetPath.Substring(assetPath.IndexOf("Assets/"));
                    TextureImporter ti = AssetImporter.GetAtPath(resourcePath) as TextureImporter;
                    if (ti == null)
                    {
                        report.Add($"  Skipped: {resourcePath} (not TextureImporter)");
                        continue;
                    }

                    report.Add($"  {Path.GetFileName(resourcePath)} : PPU={ti.spritePixelsPerUnit}");
                }

                report.Add("");
            }

            report.Add($"Total files scanned: {total}");
            File.WriteAllLines(reportPath, report);
            Debug.Log($"[CharacterPPUSetter] Report written: {reportPath}");
            EditorUtility.RevealInFinder(reportPath);
        }

        void ApplyPPUConfirmed()
        {
            if (!EditorUtility.DisplayDialog("Confirm Apply PPU", $"Apply PPU={ppu} to selected targets?", "Apply", "Cancel")) return;
            ApplyPPU();
        }

        void ApplyPPU()
        {
            var backupList = new List<PPUBackupEntry>();
            var report = new List<string>();
            report.Add("Character PPU Apply Report");
            report.Add("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.Add("");

            int total = 0, changed = 0;

            IEnumerable<string> targets = GetTargetCharacters();
            foreach (var c in targets)
            {
                string folder = Path.Combine("Assets/Resources/Characters", c);
                report.Add($"-- {c} --");
                if (!Directory.Exists(folder))
                {
                    report.Add("  (folder missing)");
                    report.Add("");
                    continue;
                }

                var pngs = Directory.GetFiles(folder, "*.png", includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                foreach (var f in pngs)
                {
                    total++;
                    string assetPath = f.Replace(Path.DirectorySeparatorChar, '/');
                    string resourcePath = assetPath.Substring(assetPath.IndexOf("Assets/"));
                    TextureImporter ti = AssetImporter.GetAtPath(resourcePath) as TextureImporter;
                    if (ti == null)
                    {
                        report.Add($"  Skipped: {resourcePath} (not TextureImporter)");
                        continue;
                    }

                    float oldPPU = ti.spritePixelsPerUnit;
                    if (Mathf.Approximately(oldPPU, ppu) && !overwriteAll)
                    {
                        report.Add($"  Unchanged: {Path.GetFileName(resourcePath)} : already {oldPPU}");
                        continue;
                    }

                    backupList.Add(new PPUBackupEntry { Path = resourcePath, PPU = oldPPU });

                    ti.spritePixelsPerUnit = ppu;
                    ti.SaveAndReimport();
                    changed++;
                    report.Add($"  Changed: {Path.GetFileName(resourcePath)} : {oldPPU} -> {ppu}");
                }

                report.Add("");
            }

            report.Add($"Total files processed: {total}");
            report.Add($"Total changed: {changed}");

            // Backup
            if (backupBefore)
            {
                var wrapper = new PPUBackupWrapper { Entries = backupList.ToArray() };
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(backupPath, json);
                report.Add($"Backup written: {backupPath}");
            }

            File.WriteAllLines(reportPath, report);
            Debug.Log($"[CharacterPPUSetter] Apply finished. Report: {reportPath}");
            EditorUtility.RevealInFinder(reportPath);
        }

        IEnumerable<string> GetTargetCharacters()
        {
            if (applyToAll) return Characters;
            var list = new List<string>();
            for (int i = 0; i < Characters.Length; i++) if (selected != null && selected.Length == Characters.Length && selected[i]) list.Add(Characters[i]);
            return list;
        }

        [Serializable]
        public class PPUBackupEntry { public string Path; public float PPU; }
        [Serializable]
        public class PPUBackupWrapper { public PPUBackupEntry[] Entries; }
    }
}