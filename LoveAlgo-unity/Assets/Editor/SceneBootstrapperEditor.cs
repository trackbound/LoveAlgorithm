using UnityEditor;
using UnityEngine;

namespace LoveAlgo.Core
{
    [CustomEditor(typeof(SceneBootstrapper))]
    sealed class SceneBootstrapperEditor : UnityEditor.Editor
    {
        // 새 버전은 activateOnPlay / deactivateOnPlay
        // 이전에는 targets로 하나만 관리되었음
        SerializedProperty activateProperty;
        SerializedProperty deactivateProperty;
        bool showActivateList = true;
        bool showDeactivateList = true;

        static readonly Color colorInactive = new(1f, 0.6f, 0.6f);   // 빨간 톤 — 비활성
        static readonly Color colorActive   = new(0.6f, 1f, 0.6f);   // 초록 톤 — 활성

        void OnEnable()
        {
            activateProperty = serializedObject.FindProperty("activateOnPlay");
            if (activateProperty == null)
                activateProperty = serializedObject.FindProperty("targets");

            deactivateProperty = serializedObject.FindProperty("deactivateOnPlay");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (activateProperty == null && deactivateProperty == null)
            {
                EditorGUILayout.HelpBox(
                    "SceneBootstrapper 에디터: activateOnPlay/deactivateOnPlay(이전 targets) 배열 프로퍼티를 찾을 수 없습니다.\n" +
                    "스크립트가 변경되었거나, 변수 명이 잘못된 상태일 수 있습니다.\n" +
                    "기본 인스펙터를 사용하거나 파일을 재생성해주세요.",
                    MessageType.Warning);

                DrawDefaultInspector();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.HelpBox(
                "플레이 시 배열 순서대로 SetActive(true) 됩니다.\n" +
                "에디터에서 비활성화해도 런타임에 자동 활성화됩니다.\n" +
                "순서 권장: 싱글톤(ScreenFX, Popup) → UI 요소(Stage, Dialogue, Title)",
                MessageType.Info);

            // 커스텀 리스트 — 항목별 활성 상태 색상 표시
            DrawTargetList(activateProperty, ref showActivateList, "Activate On Play");
            DrawTargetList(deactivateProperty, ref showDeactivateList, "Deactivate On Play");

            EditorGUILayout.Space(8);

            // 에디터 편의 버튼
            if (activateProperty != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Activate 리스트: 모두 활성화 (에디터)"))
                    SetAllActive(activateProperty, true);
                if (GUILayout.Button("Activate 리스트: 모두 비활성화 (에디터)"))
                    SetAllActive(activateProperty, false);
                EditorGUILayout.EndHorizontal();
            }

            if (deactivateProperty != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Deactivate 리스트: 모두 비활성화 (에디터)"))
                    SetAllActive(deactivateProperty, false);
                if (GUILayout.Button("Deactivate 리스트: 모두 활성화 (에디터)"))
                    SetAllActive(deactivateProperty, true);
                EditorGUILayout.EndHorizontal();
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawTargetList(SerializedProperty property, ref bool showList, string title)
        {
            if (property == null)
                return;

            showList = EditorGUILayout.Foldout(showList, $"{title} ({property.arraySize})", true);
            if (!showList)
                return;

            EditorGUI.indentLevel++;
            int inactive = 0;
            int missing = 0;

            for (int i = 0; i < property.arraySize; i++)
            {
                var elem = property.GetArrayElementAtIndex(i);
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
                property.InsertArrayElementAtIndex(property.arraySize);
            if (property.arraySize > 0 && GUILayout.Button("-", GUILayout.Width(24)))
                property.DeleteArrayElementAtIndex(property.arraySize - 1);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 요약
            if (missing > 0)
                EditorGUILayout.HelpBox($"⚠ null 슬롯 {missing}개 — 정리 필요", MessageType.Warning);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                $"현재 OFF: {inactive}개  |  ON: {property.arraySize - inactive - missing}개",
                EditorStyles.centeredGreyMiniLabel);
        }

        void SetAllActive(SerializedProperty property, bool active)
        {
            if (property == null)
                return;

            for (int i = 0; i < property.arraySize; i++)
            {
                var go = property.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (go == null || go.activeSelf == active) continue;

                Undo.RecordObject(go, active ? "Bootstrapper: 모두 활성화" : "Bootstrapper: 모두 비활성화");
                go.SetActive(active);
                EditorUtility.SetDirty(go);
            }
        }
    }
}
