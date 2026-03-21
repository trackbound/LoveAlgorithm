using UnityEditor;
using UnityEngine;

namespace LoveAlgo.Core
{
    [CustomEditor(typeof(SceneBootstrapper))]
    sealed class SceneBootstrapperEditor : Editor
    {
        SerializedProperty targetsProperty;

        void OnEnable()
        {
            targetsProperty = serializedObject.FindProperty("targets");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "플레이 시 배열 순서대로 SetActive(true) 됩니다.\n" +
                "에디터에서 비활성화 상태로 배치해도 런타임에 자동 활성화됩니다.",
                MessageType.Info);

            EditorGUILayout.PropertyField(targetsProperty, true);

            // 비활성 오브젝트 현황 표시
            if (targetsProperty.arraySize > 0)
            {
                int inactive = 0;
                int missing = 0;
                for (int i = 0; i < targetsProperty.arraySize; i++)
                {
                    var obj = targetsProperty.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (obj == null) missing++;
                    else if (!obj.activeSelf) inactive++;
                }

                if (missing > 0)
                    EditorGUILayout.HelpBox($"⚠ null 슬롯 {missing}개 — 정리가 필요합니다.", MessageType.Warning);

                if (inactive > 0)
                    EditorGUILayout.LabelField($"현재 비활성: {inactive}개 (플레이 시 자동 활성화)",
                        EditorStyles.miniLabel);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
