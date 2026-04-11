using UnityEngine;
using UnityEditor;

namespace LoveAlgo.Shop.Editor
{
    /// <summary>
    /// ShopItemFilter 커스텀 인스펙터
    /// 체크박스 + 아이템 이름으로 깔끔하게 표시
    /// </summary>
    [CustomEditor(typeof(ShopItemFilter))]
    public class ShopItemFilterEditor : UnityEditor.Editor
    {
        SerializedProperty itemsProp;

        void OnEnable()
        {
            if (target == null) return;
            itemsProp = serializedObject.FindProperty("items");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "체크된 아이템만 상점에 노출됩니다.\n" +
                "항목이 비어있으면 필터 없이 전체 표시.\n" +
                "[아이템 목록 동기화] 버튼으로 ItemDatabase와 동기화하세요.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            // 전체 선택/해제 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 선택", GUILayout.Height(24)))
            {
                SetAll(true);
            }
            if (GUILayout.Button("전체 해제", GUILayout.Height(24)))
            {
                SetAll(false);
            }
            if (GUILayout.Button("아이템 목록 동기화", GUILayout.Height(24)))
            {
                // ContextMenu 호출
                var method = target.GetType().GetMethod("SyncFromDatabase",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(target, null);
                serializedObject.Update();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 아이템 리스트
            int enabledCount = 0;
            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                var element = itemsProp.GetArrayElementAtIndex(i);
                var idProp = element.FindPropertyRelative("id");
                var nameProp = element.FindPropertyRelative("name");
                var enabledProp = element.FindPropertyRelative("enabled");

                var noProp = element.FindPropertyRelative("csvNo");
                string label = string.IsNullOrEmpty(nameProp.stringValue)
                    ? idProp.stringValue
                    : nameProp.stringValue;

                int csvNo = noProp != null ? noProp.intValue : i + 1;
                string displayLabel = $"  {csvNo:D2}. {label}";

                EditorGUILayout.BeginHorizontal();
                enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(20));
                EditorGUILayout.LabelField(displayLabel);
                EditorGUILayout.EndHorizontal();

                if (enabledProp.boolValue) enabledCount++;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"활성: {enabledCount} / {itemsProp.arraySize}",
                EditorStyles.miniLabel);

            serializedObject.ApplyModifiedProperties();
        }

        void SetAll(bool value)
        {
            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                var element = itemsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("enabled").boolValue = value;
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
