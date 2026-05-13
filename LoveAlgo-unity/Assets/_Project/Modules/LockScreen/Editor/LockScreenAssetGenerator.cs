#if UNITY_EDITOR
using System.IO;
using LoveAlgo.LockScreen.Data;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.LockScreen.EditorTools
{
    /// <summary>
    /// PC잠금 기획서 17p 기준 ToDo 33개 + ToDoListSO + LockScreenContentSO 자동 생성.
    /// 메뉴: LoveAlgo > LockScreen > Generate Default Assets
    /// </summary>
    public static class LockScreenAssetGenerator
    {
        const string OutputFolder = "Assets/Resources/Data/LockScreen";
        const string ToDoFolder = OutputFolder + "/ToDo";

        // PDF 17p — 33개 (26 + 7)
        static readonly (string id, string text)[] ToDoEntries =
        {
            ("water_2l",          "물 2L 마시기"),
            ("read_mech",         "<기계구조론> 독서"),
            ("hw_compstruct",     "컴퓨터구조 과제"),
            ("hw_humanphil",      "인간철학사 과제"),
            ("hw_probrand",       "확률랜덤변수 과제"),
            ("hw_datastruct",     "자료구조 과제"),
            ("hw_psych",          "심리학기초 과제"),
            ("hw_postmodern",     "포스트모더니즘 과제"),
            ("club_log",          "동아리 활동일지 작성"),
            ("club_plan",         "동아리 활동계획서 작성"),
            ("club_funds",        "동아리 지원금 사용내역서 작성"),
            ("buy_textbook",      "교재구매"),
            ("download_pdf",      "자료 PDF 다운로드"),
            ("mail_prof_kim",     "김교수님 메일 작성"),
            ("debug_roa",         "로아 디버깅_ref"),
            ("club_dinner",       "동아리 회식 예약"),
            ("monitor_preorder",  "모니터 사전구매 예약"),
            ("club_cleanup",      "동아리방 정리"),
            ("bind_textbook",     "교재 제본"),
            ("download_slides",   "강의자료 다운로드"),
            ("lib_reserve",       "도서관책 예약"),
            ("lib_return",        "도서관책 반납"),
            ("backup_roa",        "로아 파일 백업"),
            ("hw_deadlines",      "과제 마감일 정리"),
            ("laundry",           "세탁"),
            ("hw_rubric",         "과제 채점 기준(루브릭) 확인"),
            ("reissue_id",        "학생증 재발급"),
            ("laptop_charger",    "노트북 충전기 챙기기"),
            ("clean_kbm",         "키보드/마우스 닦기"),
            ("lib_seat",          "도서관 자리 예약"),
            ("club_announce",     "동아리 단톡 알림 공지"),
            ("umbrella",          "우산 챙기기"),
            ("quiz_scope",        "내일 퀴즈 범위 확인"),
        };

        [MenuItem("LoveAlgo/LockScreen/Generate Default Assets")]
        public static void Generate()
        {
            EnsureFolder(OutputFolder);
            EnsureFolder(ToDoFolder);

            // 1) ToDo 33개
            var items = new ToDoItemSO[ToDoEntries.Length];
            for (int i = 0; i < ToDoEntries.Length; i++)
            {
                var (id, text) = ToDoEntries[i];
                string assetPath = $"{ToDoFolder}/ToDo_{(i + 1):D2}_{id}.asset";
                var so = AssetDatabase.LoadAssetAtPath<ToDoItemSO>(assetPath);
                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<ToDoItemSO>();
                    AssetDatabase.CreateAsset(so, assetPath);
                }
                so.id = id;
                so.text = text;
                so.defaultChecked = false;
                EditorUtility.SetDirty(so);
                items[i] = so;
            }

            // 2) ToDoListSO
            string listPath = $"{OutputFolder}/ToDoList.asset";
            var list = AssetDatabase.LoadAssetAtPath<ToDoListSO>(listPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<ToDoListSO>();
                AssetDatabase.CreateAsset(list, listPath);
            }
            list.items.Clear();
            list.items.AddRange(items);
            EditorUtility.SetDirty(list);

            // 3) LockScreenContentSO
            string contentPath = $"{OutputFolder}/LockScreenContent.asset";
            var content = AssetDatabase.LoadAssetAtPath<LockScreenContentSO>(contentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<LockScreenContentSO>();
                AssetDatabase.CreateAsset(content, contentPath);
                EditorUtility.SetDirty(content);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[LockScreenAssetGenerator] 생성 완료: ToDo {items.Length}개 + ToDoList + LockScreenContent → {OutputFolder}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = list;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
