using UnityEditor;
using UnityEngine;

namespace LoveAlgo.Core
{
    [CustomEditor(typeof(SceneBootstrapper))]
    sealed class SceneBootstrapperEditor : UnityEditor.Editor
    {
        SerializedProperty targetsProperty;
        bool showList = true;

        static readonly Color colorInactive = new(1f, 0.6f, 0.6f);   // 빨간 톤 — 비활성
        static readonly Color colorActive   = new(0.6f, 1f, 0.6f);   // 초록 톤 — 활성

        void OnEnable()
        {
            targetsProperty = serializedObject.FindProperty("targets");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "플레이 시 배열 순서대로 SetActive(true) 됩니다.\n" +
                "에디터에서 비활성화해도 런타임에 자동 활성화됩니다.\n" +
                "순서 권장: 싱글톤(ScreenFX, Popup) → UI 요소(Stage, Dialogue, Title)",
                MessageType.Info);

            // 커스텀 리스트 — 항목별 활성 상태 색상 표시
            showList = EditorGUILayout.Foldout(showList, $"Targets ({targetsProperty.arraySize})", true);
            if (showList)
            {
                EditorGUI.indentLevel++;
                int inactive = 0;
                int missing = 0;

                for (int i = 0; i < targetsProperty.arraySize; i++)
                {
                    var elem = targetsProperty.GetArrayElementAtIndex(i);
                    var go = elem.objectReferenceValue as GameObject;

                    EditorGUILayout.BeginHorizontal();

                    // 순서 번호
                    EditorGUILayout.LabelField($"[{i}]", GUILayout.Width(28));

                    // 상태 표시 색상
                    var prevColor = GUI.backgroundColor;
                    if (go == null)
                    {
                        missing++;
                        GUI.backgroundColor = Color.yellow;
                    }
                    else if (!go.activeSelf)
                    {
                        inactive++;
                        GUI.backgroundColor = colorInactive;
                    }
                    else
                    {
                        GUI.backgroundColor = colorActive;
                    }

                    EditorGUILayout.PropertyField(elem, GUIContent.none);
                    GUI.backgroundColor = prevColor;

                    // 상태 라벨
                    if (go == null)
                        EditorGUILayout.LabelField("null", EditorStyles.miniLabel, GUILayout.Width(40));
                    else if (!go.activeSelf)
                        EditorGUILayout.LabelField("OFF", EditorStyles.boldLabel, GUILayout.Width(30));
                    else
                        EditorGUILayout.LabelField("ON", EditorStyles.miniLabel, GUILayout.Width(30));

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;

                // 배열 크기 조절 버튼
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+", GUILayout.Width(24)))
                    targetsProperty.InsertArrayElementAtIndex(targetsProperty.arraySize);
                if (targetsProperty.arraySize > 0 && GUILayout.Button("-", GUILayout.Width(24)))
                    targetsProperty.DeleteArrayElementAtIndex(targetsProperty.arraySize - 1);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // 요약
                if (missing > 0)
                    EditorGUILayout.HelpBox($"⚠ null 슬롯 {missing}개 — 정리 필요", MessageType.Warning);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(
                    $"현재 OFF: {inactive}개  |  ON: {targetsProperty.arraySize - inactive - missing}개",
                    EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.Space(8);

            // 에디터 편의 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("모두 비활성화 (에디터)"))
                SetAllActive(false);
            if (GUILayout.Button("모두 활성화 (에디터)"))
                SetAllActive(true);
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>에디터에서 타겟들을 일괄 활성화/비활성화</summary>
        void SetAllActive(bool active)
        {
            for (int i = 0; i < targetsProperty.arraySize; i++)
            {
                var go = targetsProperty.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (go == null || go.activeSelf == active) continue;

                Undo.RecordObject(go, active ? "Bootstrapper: 모두 활성화" : "Bootstrapper: 모두 비활성화");
                go.SetActive(active);
                EditorUtility.SetDirty(go);
            }
        }
    }
}
