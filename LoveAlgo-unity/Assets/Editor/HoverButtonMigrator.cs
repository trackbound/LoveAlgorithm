using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.UI;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// HoverButton → ButtonEX 일괄 마이그레이션
    /// - HoverButton이 있는 프리팹: 필드 복사 → ButtonEX 추가 → HoverButton 제거
    /// - HoverButton이 없는 버튼: ButtonEX.Simple 추가
    /// - Button.Transition → None 강제
    /// - TitleUI 프리팹 제외
    /// </summary>
    public static class HoverButtonMigrator
    {
        const string TitleUIPrefabPath = "Assets/Prefabs/UI/Main/TitleUI.prefab";

        [MenuItem("LoveAlgo/Tools/Migrate HoverButton → ButtonEX")]
        public static void Migrate()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
            int migrated = 0;
            int added = 0;
            int transitionFixed = 0;
            int skipped = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                // TitleUI 제외
                if (path == TitleUIPrefabPath)
                {
                    skipped++;
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                bool dirty = false;

                using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    var root = scope.prefabContentsRoot;

                    // ── 1) HoverButton → ButtonEX 변환 ──
                    var hovers = root.GetComponentsInChildren<HoverButton>(true);
                    foreach (var hb in hovers)
                    {
                        MigrateOne(hb);
                        migrated++;
                        dirty = true;
                    }

                    // ── 2) ButtonEX 없는 Button에 Simple 추가 ──
                    var buttons = root.GetComponentsInChildren<Button>(true);
                    foreach (var btn in buttons)
                    {
                        if (btn.GetComponent<ButtonEX>() == null)
                        {
                            var ex = btn.gameObject.AddComponent<ButtonEX>();
                            SetMode(ex, ButtonEX.ButtonMode.Simple);
                            added++;
                            dirty = true;
                        }

                        // Transition 강제 None
                        if (btn.transition != Selectable.Transition.None)
                        {
                            btn.transition = Selectable.Transition.None;
                            transitionFixed++;
                            dirty = true;
                        }
                    }
                }

                if (dirty)
                    Debug.Log($"[Migration] {path}: HoverButton 변환 완료");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Migration 완료] 변환: {migrated}, 신규 Simple: {added}, " +
                      $"Transition 수정: {transitionFixed}, 제외: {skipped}");
        }

        static void MigrateOne(HoverButton hb)
        {
            var go = hb.gameObject;

            // ── HoverButton SerializedObject에서 필드 읽기 ──
            var soOld = new SerializedObject(hb);
            soOld.Update();

            var oldMode = (HoverButton.HoverMode)soOld.FindProperty("hoverMode").enumValueIndex;
            var oldTargetImage = soOld.FindProperty("targetImage").objectReferenceValue as Image;
            var oldNormalSprite = soOld.FindProperty("normalSprite").objectReferenceValue as Sprite;
            var oldHoverSprite = soOld.FindProperty("hoverSprite").objectReferenceValue as Sprite;
            var oldPressedSprite = soOld.FindProperty("pressedSprite").objectReferenceValue as Sprite;
            var oldNormalChild = soOld.FindProperty("normalChild").objectReferenceValue as GameObject;
            var oldHoverChild = soOld.FindProperty("hoverChild").objectReferenceValue as GameObject;
            var oldPressedChild = soOld.FindProperty("pressedChild").objectReferenceValue as GameObject;
            var oldUseScale = soOld.FindProperty("useScaleEffect").boolValue;
            var oldHoverScale = soOld.FindProperty("hoverScale").floatValue;
            var oldPressedScale = soOld.FindProperty("pressedScale").floatValue;
            var oldScaleDuration = soOld.FindProperty("scaleDuration").floatValue;

            // ── HoverButton 제거 ──
            Object.DestroyImmediate(hb, true);

            // ── ButtonEX 추가 ──
            var ex = go.AddComponent<ButtonEX>();
            var soNew = new SerializedObject(ex);
            soNew.Update();

            // 모드 매핑
            ButtonEX.ButtonMode newMode;
            switch (oldMode)
            {
                case HoverButton.HoverMode.None:
                    newMode = ButtonEX.ButtonMode.Simple;
                    break;
                case HoverButton.HoverMode.SpriteSwap:
                    newMode = ButtonEX.ButtonMode.Hover;
                    break;
                case HoverButton.HoverMode.ChildSwap:
                    newMode = ButtonEX.ButtonMode.ChildSwap;
                    break;
                case HoverButton.HoverMode.Both:
                    newMode = ButtonEX.ButtonMode.ChildSwap;
                    Debug.LogWarning($"[Migration] '{go.name}': Both 모드 → ChildSwap. " +
                                    $"SpriteSwap 스프라이트가 유실될 수 있음. 확인 필요.");
                    break;
                default:
                    newMode = ButtonEX.ButtonMode.Simple;
                    break;
            }

            soNew.FindProperty("mode").enumValueIndex = (int)newMode;

            // 이미지 (명시 설정된 경우만)
            soNew.FindProperty("overrideTargetImage").objectReferenceValue = oldTargetImage;

            // SpriteSwap 필드
            soNew.FindProperty("normalSprite").objectReferenceValue = oldNormalSprite;
            soNew.FindProperty("hoverSprite").objectReferenceValue = oldHoverSprite;
            // pressedSprite는 ButtonEX에 없음 (PressedTint로 대체) → 로그
            if (oldPressedSprite != null)
            {
                Debug.LogWarning($"[Migration] '{go.name}': pressedSprite 존재 → ButtonEX에서 PressedTint로 대체됨. " +
                                $"원본 스프라이트: {oldPressedSprite.name}");
            }

            // ChildSwap 필드
            soNew.FindProperty("normalChild").objectReferenceValue = oldNormalChild;
            soNew.FindProperty("hoverChild").objectReferenceValue = oldHoverChild;
            soNew.FindProperty("pressedChild").objectReferenceValue = oldPressedChild;

            // 스케일 효과
            soNew.FindProperty("useScaleEffect").boolValue = oldUseScale;
            soNew.FindProperty("hoverScale").floatValue = oldHoverScale;
            soNew.FindProperty("pressedScale").floatValue = oldPressedScale;
            soNew.FindProperty("scaleDuration").floatValue = oldScaleDuration;

            soNew.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetMode(ButtonEX ex, ButtonEX.ButtonMode mode)
        {
            var so = new SerializedObject(ex);
            so.Update();
            so.FindProperty("mode").enumValueIndex = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
