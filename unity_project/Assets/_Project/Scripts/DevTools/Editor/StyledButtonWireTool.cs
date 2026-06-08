using System.Collections.Generic;
using LoveAlgo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 선택한 UI 루트(또는 단일 Button) 하위의 <see cref="Button"/> 을 <see cref="StyledButton"/> 으로 일괄
    /// 전환하고, 현재 스프라이트 이름에서 네이밍 규약(<see cref="StyledButtonSpriteConvention"/>)의 형제
    /// (_hover/_disabled/_on)를 찾아 상태 필드에 자동 할당한다.
    ///
    /// 전환은 <c>m_Script</c> 리포인트로 수행 — 컴포넌트 fileID·상속 Button 필드(m_Colors/m_Navigation/
    /// m_TargetGraphic/m_OnClick)·외부 참조(TitleView.newGameButton 등)를 보존한다(모달 프리팹 선례와 동일).
    /// 시각 로직은 StyledButton(런타임)에 있고, 이 툴은 에디터 배선 편의일 뿐 — 런타임 코드 0.
    ///
    /// 안전: 자동 저장하지 않는다(감독 검수/undo 여지). 프리팹 *인스턴스*는 스킵(m_Script 오버라이드 취약 →
    /// 프리팹 에셋을 Prefab Mode에서 변환). 변경한 오브젝트의 *해당 씬만* dirty 표시(타 씬 무영향).
    /// </summary>
    public static class StyledButtonWireTool
    {
        // ResolveStyledButtonScript 폴백용(타입 탐색 실패 시).
        const string StyledButtonScriptGuid = "7de9977d3e6de4f44a6c76b982ca7ce0";

        enum ConvertResult { Converted, Reassigned, Skipped }

        [MenuItem("Tools/UI/Convert Selection to StyledButton (+auto sprites)")]
        static void ConvertSelection()
        {
            var roots = Selection.gameObjects;
            if (roots == null || roots.Length == 0) return;

            int converted = 0, reassigned = 0, skipped = 0;
            var visited = new HashSet<GameObject>(); // 중복 선택 루트(겹치는 계층) 가드 — 참조 기반
            foreach (var root in roots)
            {
                foreach (var btn in root.GetComponentsInChildren<Button>(true))
                {
                    if (btn == null || !visited.Add(btn.gameObject)) continue;
                    switch (ConvertButtonToStyledButton(btn))
                    {
                        case ConvertResult.Converted: converted++; break;
                        case ConvertResult.Reassigned: reassigned++; break;
                        default: skipped++; break;
                    }
                }
            }
            Log($"일괄배선 완료 — 전환 {converted} · 재할당 {reassigned} · 스킵 {skipped}");
        }

        [MenuItem("Tools/UI/Convert Selection to StyledButton (+auto sprites)", true)]
        static bool ConvertSelectionValidate() => Selection.gameObjects != null && Selection.gameObjects.Length > 0;

        [MenuItem("CONTEXT/Button/Convert to StyledButton (+auto sprites)")]
        static void ConvertOne(MenuCommand cmd)
        {
            if (cmd.context is Button btn) ConvertButtonToStyledButton(btn);
        }

        [MenuItem("CONTEXT/StyledButton/Re-assign State Sprites")]
        static void ReassignOne(MenuCommand cmd)
        {
            if (cmd.context is StyledButton styled)
            {
                AssignStateSprites(styled, overwrite: true);
                MarkDirty(styled);
            }
        }

        /// <summary>Button → StyledButton 전환 + 상태 스프라이트 자동 할당. 이미 StyledButton이면 (비파괴) 보강만.</summary>
        static ConvertResult ConvertButtonToStyledButton(Button btn)
        {
            if (btn == null) return ConvertResult.Skipped;

            // 프리팹 인스턴스의 컴포넌트는 m_Script 오버라이드가 취약 → 스킵(프리팹 에셋은 Prefab Mode에서).
            if (PrefabUtility.IsPartOfPrefabInstance(btn))
            {
                Debug.LogWarning($"[StyledButtonWireTool] 프리팹 인스턴스라 스킵: {HierarchyPath(btn.transform)} " +
                                 "(프리팹 에셋을 Prefab Mode에서 변환하세요)");
                return ConvertResult.Skipped;
            }

            // 이미 StyledButton(상속이라 GetComponentsInChildren<Button>에 잡힘) → 스프라이트만 비파괴 보강.
            if (btn is StyledButton already)
            {
                AssignStateSprites(already, overwrite: false);
                MarkDirty(already);
                return ConvertResult.Reassigned;
            }

            var go = btn.gameObject;
            Undo.RegisterCompleteObjectUndo(go, "Convert to StyledButton");

            // m_Script 리포인트(fileID·상속필드·외부참조 보존).
            var script = ResolveStyledButtonScript();
            if (script == null)
            {
                Debug.LogError("[StyledButtonWireTool] StyledButton MonoScript를 찾지 못했습니다.");
                return ConvertResult.Skipped;
            }
            var so = new SerializedObject(btn);
            so.FindProperty("m_Script").objectReferenceValue = script;
            so.ApplyModifiedProperties();

            // 리포인트 후 old btn 참조는 무효 → 재취득.
            var styled = go.GetComponent<StyledButton>();
            if (styled == null)
            {
                Debug.LogError($"[StyledButtonWireTool] 전환 실패(StyledButton 미발견): {HierarchyPath(go.transform)}");
                return ConvertResult.Skipped;
            }

            // pressed 틴트가 동작하도록 ColorTint 강제 + targetGraphic 보장.
            if (styled.transition != Selectable.Transition.ColorTint)
                styled.transition = Selectable.Transition.ColorTint;
            if (styled.targetGraphic == null)
                styled.targetGraphic = styled.GetComponent<Image>() != null
                    ? styled.GetComponent<Image>()
                    : styled.GetComponentInChildren<Image>(true);

            AssignStateSprites(styled, overwrite: false);
            MarkDirty(styled);
            return ConvertResult.Converted;
        }

        /// <summary>targetGraphic 스프라이트 이름 → 같은 폴더의 _hover/_disabled/_on 형제를 StyledButton 상태 필드에 할당(공유 resolver).</summary>
        static void AssignStateSprites(StyledButton styled, bool overwrite)
        {
            var r = ButtonStateSpriteResolver.Resolve(styled.targetGraphic as Image);
            if (r.Hover != null && (overwrite || styled.HighlightedSprite == null)) styled.HighlightedSprite = r.Hover;
            if (r.Disabled != null && (overwrite || styled.DisabledSprite == null)) styled.DisabledSprite = r.Disabled;
            if (r.On != null && (overwrite || styled.SelectedSprite == null)) styled.SelectedSprite = r.On;
        }

        static MonoScript ResolveStyledButtonScript()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript StyledButton"))
            {
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (ms != null && ms.GetClass() == typeof(StyledButton)) return ms;
            }
            return AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(StyledButtonScriptGuid));
        }

        static void MarkDirty(Component c)
        {
            EditorUtility.SetDirty(c);
            if (!Application.isPlaying && c.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        }

        static void Log(string msg) => Debug.Log($"[StyledButtonWireTool] {msg}");

        static string HierarchyPath(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
