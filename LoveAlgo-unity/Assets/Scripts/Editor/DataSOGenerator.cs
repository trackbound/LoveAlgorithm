using UnityEditor;
using UnityEngine;
using LoveAlgo.Shop;
using LoveAlgo.Schedule;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 하드코딩 기본 데이터로부터 ScriptableObject 에셋 자동 생성
    /// Tools → LoveAlgo 메뉴에서 실행
    /// </summary>
    public static class DataSOGenerator
    {
        const string ItemCatalogPath = "Assets/Resources/Data/ItemCatalog.asset";
        const string ScheduleDataPath = "Assets/Resources/Data/ScheduleData.asset";

        [MenuItem("Tools/LoveAlgo/Generate Item Catalog SO")]
        static void GenerateItemCatalog()
        {
            EnsureDirectory("Assets/Resources/Data");

            var existing = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(ItemCatalogPath);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("덮어쓰기 확인",
                    "ItemCatalog.asset이 이미 존재합니다. 덮어쓰시겠습니까?",
                    "덮어쓰기", "취소"))
                    return;

                AssetDatabase.DeleteAsset(ItemCatalogPath);
            }

            // 하드코딩 데이터 로드를 위해 강제 리로드
            ItemDatabase.Reload();

            var catalog = ScriptableObject.CreateInstance<ItemCatalogSO>();

            // SerializedObject를 통해 items 리스트에 접근
            var so = new SerializedObject(catalog);
            var itemsProp = so.FindProperty("items");

            var allItems = ItemDatabase.GetAll();
            itemsProp.arraySize = allItems.Count;

            for (int i = 0; i < allItems.Count; i++)
            {
                var src = allItems[i];
                var element = itemsProp.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("Id").stringValue = src.Id;
                element.FindPropertyRelative("Name").stringValue = src.Name;
                element.FindPropertyRelative("Description").stringValue = src.Description;
                element.FindPropertyRelative("Price").intValue = src.Price;
                element.FindPropertyRelative("Category").enumValueIndex = (int)src.Category;
                element.FindPropertyRelative("EffectValue").intValue = src.EffectValue;
                element.FindPropertyRelative("IconPath").stringValue = src.IconPath ?? "";
                element.FindPropertyRelative("IconSprite").objectReferenceValue = src.IconSprite;
                element.FindPropertyRelative("DetailSprite").objectReferenceValue = src.DetailSprite;
                element.FindPropertyRelative("Availability").enumValueIndex = (int)src.Availability;
                element.FindPropertyRelative("EffectStat").stringValue = src.EffectStat ?? "";
                element.FindPropertyRelative("DuplicateTag").stringValue = src.DuplicateTag ?? "";
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(catalog, ItemCatalogPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DataSOGenerator] ItemCatalog 생성 완료: {allItems.Count}개 아이템 → {ItemCatalogPath}");
        }

        [MenuItem("Tools/LoveAlgo/Generate Schedule Data SO")]
        static void GenerateScheduleData()
        {
            EnsureDirectory("Assets/Resources/Data");

            var existing = AssetDatabase.LoadAssetAtPath<ScheduleDataSO>(ScheduleDataPath);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("덮어쓰기 확인",
                    "ScheduleData.asset이 이미 존재합니다. 덮어쓰시겠습니까?",
                    "덮어쓰기", "취소"))
                    return;

                AssetDatabase.DeleteAsset(ScheduleDataPath);
            }

            // 하드코딩 데이터 로드를 위해 강제 리로드
            ScheduleTable.Reload();

            var data = ScriptableObject.CreateInstance<ScheduleDataSO>();
            var so = new SerializedObject(data);

            // 스케줄 엔트리
            var schedulesProp = so.FindProperty("schedules");
            var types = System.Enum.GetValues(typeof(ScheduleType));
            schedulesProp.arraySize = types.Length;

            int idx = 0;
            foreach (ScheduleType t in types)
            {
                var effect = ScheduleTable.Get(t);
                var element = schedulesProp.GetArrayElementAtIndex(idx++);
                element.FindPropertyRelative("type").enumValueIndex = (int)t;

                var effectProp = element.FindPropertyRelative("effect");
                effectProp.FindPropertyRelative("displayName").stringValue = effect.displayName;
                effectProp.FindPropertyRelative("description").stringValue = effect.description;
                effectProp.FindPropertyRelative("moneyChange").intValue = effect.moneyChange;
                effectProp.FindPropertyRelative("strengthChange").intValue = effect.strengthChange;
                effectProp.FindPropertyRelative("intelligenceChange").intValue = effect.intelligenceChange;
                effectProp.FindPropertyRelative("socialChange").intValue = effect.socialChange;
                effectProp.FindPropertyRelative("perseveranceChange").intValue = effect.perseveranceChange;
                effectProp.FindPropertyRelative("fatigueChange").intValue = effect.fatigueChange;
            }

            // 카테고리 정보
            var categoriesProp = so.FindProperty("categories");
            var cats = System.Enum.GetValues(typeof(ScheduleCategory));
            categoriesProp.arraySize = cats.Length;

            idx = 0;
            foreach (ScheduleCategory c in cats)
            {
                var element = categoriesProp.GetArrayElementAtIndex(idx++);
                element.FindPropertyRelative("category").enumValueIndex = (int)c;
                element.FindPropertyRelative("displayName").stringValue = ScheduleTable.GetCategoryName(c);
                element.FindPropertyRelative("description").stringValue = ScheduleTable.GetCategoryDescription(c);
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, ScheduleDataPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DataSOGenerator] ScheduleData 생성 완료: {types.Length}개 스케줄 → {ScheduleDataPath}");
        }

        static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }
    }
}
