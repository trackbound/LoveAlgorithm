using System.Collections.Generic;
using LoveAlgo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 모듈 루트(또는 단일 Button) 하위의 네이티브 <see cref="Button"/>에 <see cref="ButtonSpriteSwap"/>을 일괄
    /// 등록한다 — transition=ColorTint 보장(누름·비활성 틴트는 네이티브) + ButtonSpriteSwap 부착 + 현재 스프라이트
    /// 이름의 _hover/_disabled/_on 형제(<see cref="ButtonStateSpriteResolver"/>)를 자동 할당. "스프라이트 스왑을
    /// 루트에서 모듈 단위로 등록"의 실행 형태(루트 선택 후 1회 실행).
    ///
    /// <see cref="StyledButton"/>(프리팹 슬롯·단순/독립 버튼 전용)은 건드리지 않는다 — 두 방식의 분담을 툴이 강제.
    /// </summary>
    public static class ButtonSpriteSwapWireTool
    {
        [MenuItem("Tools/UI/Bind ButtonSpriteSwap to Selection (native Button + ColorTint)")]
        static void BindSelection()
        {
            var roots = Selection.gameObjects;
            if (roots == null || roots.Length == 0) return;

            int bound = 0, skipped = 0;
            var visited = new HashSet<GameObject>();
            foreach (var root in roots)
                foreach (var btn in root.GetComponentsInChildren<Button>(true))
                {
                    if (btn == null || !visited.Add(btn.gameObject)) continue;
                    if (Bind(btn)) bound++; else skipped++;
                }
            Debug.Log($"[ButtonSpriteSwapWireTool] 등록 {bound} · 스킵 {skipped}(StyledButton/프리팹인스턴스)");
        }

        [MenuItem("Tools/UI/Bind ButtonSpriteSwap to Selection (native Button + ColorTint)", true)]
        static bool BindSelectionValidate() => Selection.gameObjects != null && Selection.gameObjects.Length > 0;

        [MenuItem("CONTEXT/Button/Bind ButtonSpriteSwap (+auto sprites)")]
        static void BindOne(MenuCommand cmd) { if (cmd.context is Button btn) Bind(btn); }

        static bool Bind(Button btn)
        {
            if (btn == null) return false;

            // StyledButton(슬롯·단순 전용)은 별도 메커니즘 → 제외.
            if (btn is StyledButton)
            {
                Debug.LogWarning($"[ButtonSpriteSwapWireTool] StyledButton이라 스킵: {btn.name} (슬롯/단순 전용)");
                return false;
            }
            // 프리팹 인스턴스는 컴포넌트 추가/오버라이드가 취약 → 프리팹 에셋을 Prefab Mode에서.
            if (PrefabUtility.IsPartOfPrefabInstance(btn))
            {
                Debug.LogWarning($"[ButtonSpriteSwapWireTool] 프리팹 인스턴스라 스킵: {btn.name} (프리팹 에셋을 Prefab Mode에서)");
                return false;
            }

            Undo.RecordObject(btn, "Bind ButtonSpriteSwap");
            if (btn.transition != Selectable.Transition.ColorTint) // 누름·비활성 틴트가 동작하도록
                btn.transition = Selectable.Transition.ColorTint;

            var img = (btn.targetGraphic as Image) ?? btn.GetComponent<Image>() ?? btn.GetComponentInChildren<Image>(true);

            var swap = btn.GetComponent<ButtonSpriteSwap>();
            if (swap == null) swap = Undo.AddComponent<ButtonSpriteSwap>(btn.gameObject);

            swap.TargetImage = img;
            var r = ButtonStateSpriteResolver.Resolve(img); // normalSprite는 비워둠 → 런타임 OnEnable이 현재 Image로 캡처
            swap.HoverSprite = r.Hover;
            swap.DisabledSprite = r.Disabled;
            swap.OnSprite = r.On;

            EditorUtility.SetDirty(swap);
            if (!Application.isPlaying && btn.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(btn.gameObject.scene);
            return true;
        }
    }
}
