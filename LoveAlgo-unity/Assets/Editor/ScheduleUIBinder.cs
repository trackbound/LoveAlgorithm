using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LoveAlgo.Schedule;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// ScheduleUI 프리팹의 탭/컨테이너/슬롯 자동 바인딩
    /// 부족한 슬롯 인스턴스 자동 생성 포함
    /// </summary>
    public static class ScheduleUIBinder
    {
        const string ScheduleUIPrefabPath = "Assets/Prefabs/Schedule/ScheduleUI.prefab";
        const string ScheduleSlotPrefabPath = "Assets/Prefabs/Schedule/ScheduleSlot.prefab";

        [MenuItem("LoveAlgo/Tools/Bind ScheduleUI Tabs and Slots")]
        public static void Bind()
        {
            // ── 프리팹 로드 ──
            var scheduleUIPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScheduleUIPrefabPath);
            if (scheduleUIPrefab == null)
            {
                Debug.LogError($"[ScheduleUIBinder] ScheduleUI 프리팹 없음: {ScheduleUIPrefabPath}");
                return;
            }

            var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScheduleSlotPrefabPath);
            if (slotPrefab == null)
            {
                Debug.LogError($"[ScheduleUIBinder] ScheduleSlot 프리팹 없음: {ScheduleSlotPrefabPath}");
                return;
            }

            // 프리팹 에셋 편집 모드 진입
            string assetPath = AssetDatabase.GetAssetPath(scheduleUIPrefab);
            var root = PrefabUtility.LoadPrefabContents(assetPath);

            try
            {
                var ui = root.GetComponent<ScheduleUI>();
                if (ui == null)
                {
                    Debug.LogError("[ScheduleUIBinder] ScheduleUI 컴포넌트를 찾을 수 없음");
                    return;
                }

                var so = new SerializedObject(ui);

                // ── 1. 탭 버튼 바인딩 ──
                BindButton(so, "tabPartTime", root, "tab_parttime");
                BindButton(so, "tabExercise", root, "tab-exercise");
                BindButton(so, "tabStudy", root, "tab_study");

                // shopButton = btn_shop
                BindButton(so, "shopButton", root, "btn_shop");

                // ── 2. 카테고리 컨테이너 바인딩 ──
                var rightPanel = FindChild(root.transform, "RightPanel");

                var partTimeContainer = FindChild(rightPanel, "PartTime");
                var exerciseContainer = FindChild(rightPanel, "Exercise");
                var studyContainer = FindChild(rightPanel, "Study");

                SetGameObject(so, "containerPartTime", partTimeContainer);
                SetGameObject(so, "containerExercise", exerciseContainer);
                SetGameObject(so, "containerStudy", studyContainer);

                // ── 3. 카테고리 설명 텍스트 ──
                var descObj = FindChild(rightPanel, "txt_category_desc");
                if (descObj != null)
                {
                    var tmp = descObj.GetComponent<TMP_Text>();
                    so.FindProperty("categoryDescText").objectReferenceValue = tmp;
                }

                // ── 4. 부족한 슬롯 인스턴스 생성 ──
                // PartTime: 이미 3개 (P_1, P_2, P_3) — 확인만
                EnsureSlots(partTimeContainer, slotPrefab, 3, "P",
                    new[] { ScheduleType.PartTime_Store, ScheduleType.PartTime_Loading, ScheduleType.Invest });

                // Exercise: 2개 → 3개로 (E_3 추가)
                EnsureSlots(exerciseContainer, slotPrefab, 3, "E",
                    new[] { ScheduleType.Exercise_A, ScheduleType.Exercise_B, ScheduleType.Exercise_C });

                // Study: 0개 → 3개 (S_1, S_2, S_3)
                EnsureSlots(studyContainer, slotPrefab, 3, "S",
                    new[] { ScheduleType.Study_D, ScheduleType.Study_E, ScheduleType.Study_F });

                // ── 5. scheduleSlots 배열 재구성 (9개) ──
                var allSlots = root.GetComponentsInChildren<ScheduleSlot>(true);

                // 카테고리 순서대로 정렬: PartTime(3) → Exercise(3) → Study(3)
                var sorted = new System.Collections.Generic.List<ScheduleSlot>();
                AddSlotsFromContainer(sorted, partTimeContainer);
                AddSlotsFromContainer(sorted, exerciseContainer);
                AddSlotsFromContainer(sorted, studyContainer);

                var slotsProp = so.FindProperty("scheduleSlots");
                slotsProp.arraySize = sorted.Count;
                for (int i = 0; i < sorted.Count; i++)
                {
                    slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = sorted[i];
                }

                so.ApplyModifiedProperties();

                // ── 저장 ──
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                Debug.Log($"[ScheduleUIBinder] 바인딩 완료! 슬롯 {sorted.Count}개, 탭 3개 연결됨");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>컨테이너에 슬롯 수가 부족하면 프리팹 인스턴스 추가</summary>
        static void EnsureSlots(Transform container, GameObject slotPrefab, int targetCount,
            string prefix, ScheduleType[] types)
        {
            if (container == null) return;

            // 기존 슬롯 수 확인
            int existing = container.childCount;

            for (int i = 0; i < targetCount; i++)
            {
                ScheduleSlot slot;

                if (i < existing)
                {
                    // 기존 슬롯 — scheduleType만 갱신
                    slot = container.GetChild(i).GetComponent<ScheduleSlot>();
                }
                else
                {
                    // 새 슬롯 인스턴스 생성
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, container);
                    go.name = $"{prefix}_{i + 1}";
                    slot = go.GetComponent<ScheduleSlot>();
                }

                if (slot != null)
                {
                    var slotSo = new SerializedObject(slot);
                    slotSo.FindProperty("scheduleType").enumValueIndex = (int)types[i];
                    slotSo.ApplyModifiedProperties();
                }
            }
        }

        /// <summary>컨테이너 자식의 ScheduleSlot들을 순서대로 추가</summary>
        static void AddSlotsFromContainer(System.Collections.Generic.List<ScheduleSlot> list, Transform container)
        {
            if (container == null) return;
            for (int i = 0; i < container.childCount; i++)
            {
                var slot = container.GetChild(i).GetComponent<ScheduleSlot>();
                if (slot != null)
                    list.Add(slot);
            }
        }

        /// <summary>이름으로 Button 찾아 바인딩</summary>
        static void BindButton(SerializedObject so, string fieldName, GameObject root, string goName)
        {
            var t = FindChild(root.transform, goName);
            if (t != null)
            {
                var btn = t.GetComponent<Button>();
                so.FindProperty(fieldName).objectReferenceValue = btn;
            }
            else
            {
                Debug.LogWarning($"[ScheduleUIBinder] '{goName}' 오브젝트를 찾을 수 없음 (필드: {fieldName})");
            }
        }

        /// <summary>GameObject 직접 바인딩</summary>
        static void SetGameObject(SerializedObject so, string fieldName, Transform t)
        {
            so.FindProperty(fieldName).objectReferenceValue = t != null ? t.gameObject : null;
        }

        /// <summary>재귀적으로 자식 Transform 검색</summary>
        static Transform FindChild(Transform parent, string name)
        {
            if (parent == null) return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name)
                    return c;
                var found = FindChild(c, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
