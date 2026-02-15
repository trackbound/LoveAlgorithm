using UnityEditor;
using UnityEngine;
using LoveAlgo.Schedule;

/// <summary>
/// ScheduleUI 프리팹 정리 도구
/// 메뉴: Tools > Fix Schedule Prefab
/// 
/// 1) scheduleSlots 배열을 5개(ScheduleType enum 수)로 맞춤
/// 2) 각 슬롯의 scheduleType을 올바른 값으로 설정
/// 3) 불필요한 슬롯 GameObject 비활성화 (수동 삭제용 표시)
/// 
/// ※ 실행 후 프리팹 열어서 비활성화된 슬롯 삭제 + 탭 오브젝트 삭제 권장
/// </summary>
public static class SchedulePrefabFixer
{
    const string PrefabPath = "Assets/Prefabs/Schedule/ScheduleUI.prefab";

    [MenuItem("Tools/Fix Schedule Prefab")]
    static void Fix()
    {
        // 프리팹 로드
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[SchedulePrefabFixer] 프리팹을 찾을 수 없습니다: {PrefabPath}");
            return;
        }

        // 프리팹 편집 모드 진입
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var ui = root.GetComponent<ScheduleUI>();
        if (ui == null)
        {
            Debug.LogError("[SchedulePrefabFixer] ScheduleUI 컴포넌트를 찾을 수 없습니다.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        // SerializedObject로 private 필드 접근
        var so = new SerializedObject(ui);
        var slotsProperty = so.FindProperty("scheduleSlots");

        if (slotsProperty == null)
        {
            Debug.LogError("[SchedulePrefabFixer] scheduleSlots 필드를 찾을 수 없습니다.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        int currentCount = slotsProperty.arraySize;
        Debug.Log($"[SchedulePrefabFixer] 현재 슬롯 수: {currentCount}");

        // 원하는 ScheduleType 순서
        ScheduleType[] desiredTypes = {
            ScheduleType.PartTime_Store,    // 0: 편의점 알바
            ScheduleType.PartTime_Loading,  // 1: 상하차 알바
            ScheduleType.Exercise,          // 2: 운동
            ScheduleType.Study,             // 3: 공부
            ScheduleType.Invest             // 4: 투자
        };

        // ── Step 1: 기존 슬롯들의 scheduleType 확인 ──
        Debug.Log("── 기존 슬롯 목록 ──");
        for (int i = 0; i < currentCount; i++)
        {
            var slotRef = slotsProperty.GetArrayElementAtIndex(i);
            var slotObj = slotRef.objectReferenceValue as ScheduleSlot;
            if (slotObj != null)
            {
                var slotSo = new SerializedObject(slotObj);
                var typeProp = slotSo.FindProperty("scheduleType");
                Debug.Log($"  [{i}] {slotObj.gameObject.name} - scheduleType: {(ScheduleType)typeProp.enumValueIndex}");
            }
            else
            {
                Debug.Log($"  [{i}] (null)");
            }
        }

        // ── Step 2: 5개 슬롯에 타입 할당 ──
        // 처음 5개 슬롯만 사용, 나머지는 비활성화
        int targetCount = desiredTypes.Length;

        for (int i = 0; i < Mathf.Min(currentCount, targetCount); i++)
        {
            var slotRef = slotsProperty.GetArrayElementAtIndex(i);
            var slotObj = slotRef.objectReferenceValue as ScheduleSlot;
            if (slotObj != null)
            {
                var slotSo = new SerializedObject(slotObj);
                var typeProp = slotSo.FindProperty("scheduleType");
                typeProp.enumValueIndex = (int)desiredTypes[i];
                slotSo.ApplyModifiedProperties();
                Debug.Log($"  슬롯 [{i}] → {desiredTypes[i]}");
            }
        }

        // ── Step 3: 초과 슬롯 비활성화 + 배열에서 제거 ──
        for (int i = currentCount - 1; i >= targetCount; i--)
        {
            var slotRef = slotsProperty.GetArrayElementAtIndex(i);
            var slotObj = slotRef.objectReferenceValue as ScheduleSlot;
            if (slotObj != null)
            {
                slotObj.gameObject.name = $"[DELETE_ME] {slotObj.gameObject.name}";
                slotObj.gameObject.SetActive(false);
                Debug.Log($"  비활성화: {slotObj.gameObject.name}");
            }
        }

        // 배열 크기 축소
        slotsProperty.arraySize = targetCount;
        so.ApplyModifiedProperties();

        // ── Step 4: 탭 관련 오브젝트 찾아서 비활성화 ──
        string[] tabNames = { "PartTimeTab", "ExerciseTab", "StudyTab",
                              "PartTimePanel", "ExercisePanel", "StudyPanel" };
        foreach (var name in tabNames)
        {
            var found = FindChildRecursive(root.transform, name);
            if (found != null)
            {
                found.gameObject.name = $"[DELETE_ME] {found.gameObject.name}";
                found.gameObject.SetActive(false);
                Debug.Log($"  탭 비활성화: {found.gameObject.name}");
            }
        }

        // 저장
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log($"[SchedulePrefabFixer] ✅ 완료! 슬롯 {currentCount} → {targetCount}개");
        Debug.Log("[SchedulePrefabFixer] 프리팹을 열어 [DELETE_ME] 오브젝트들을 수동 삭제해주세요.");
    }

    static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Contains(name))
                return child;
            var found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
