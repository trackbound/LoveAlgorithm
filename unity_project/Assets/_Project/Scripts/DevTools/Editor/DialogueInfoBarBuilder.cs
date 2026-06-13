using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.UI; // DialogueView, DialogueInfoBarView

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 대사창 인포 바 조립 도구(Tools ▸ Dialogue ▸ Build Info Bar In Open Scene) — DialogueLogPrefabBuilder 패턴 미러.
    /// 열린 씬의 DialogueView.Root 아래에 InfoBar(bg + 세이브/로그/오토/숨기기 버튼)를 조립하고 전 필드를 바인딩한다.
    /// 재실행 = 기존 InfoBar 제거 후 재조립(멱등). 위치/크기는 시작값 — 비주얼 튜닝은 감독 영역(🟢).
    /// </summary>
    public static class DialogueInfoBarBuilder
    {
        const string ArtDir = "Assets/Art/UI/Stage/Dialogue";

        [MenuItem("Tools/Dialogue/Build Info Bar In Open Scene")]
        public static void Build()
        {
            EnsureSpriteImports();

            DialogueView view = null;
            foreach (var v in Object.FindObjectsByType<DialogueView>(FindObjectsInactive.Include))
            {
                view = v;
                break;
            }
            if (view == null) { Debug.LogError("[DialogueInfoBarBuilder] 열린 씬에 DialogueView 없음."); return; }
            if (view.Root == null || view.Root == view.gameObject)
            {
                Debug.LogError("[DialogueInfoBarBuilder] DialogueView.Root 미바인딩(또는 자기 자신) — 비주얼 자식 root가 필요.");
                return;
            }

            // 멱등: 기존 바 제거 후 재조립
            var old = view.Root.transform.Find("InfoBar");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var bar = MessengerPrefabBuilder.Rect("InfoBar", view.Root.transform);
            var bg = Load("bg_dialogue_info_bar");
            var barImg = bar.AddComponent<Image>();
            barImg.sprite = bg;
            barImg.raycastTarget = false; // 바 배경이 대사 클릭을 먹지 않게 — 버튼만 레이캐스트
            var barRt = (RectTransform)bar.transform;
            // 시작 배치: 하단 중앙 앵커, 대사창 위쪽 띠(네이티브 크기). 감독 튜닝 영역.
            barRt.anchorMin = barRt.anchorMax = new Vector2(0.5f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.sizeDelta = bg != null ? bg.rect.size : new Vector2(1200f, 56f);
            barRt.anchoredPosition = new Vector2(0f, 320f);

            var barView = bar.AddComponent<DialogueInfoBarView>();

            // 버튼 행(우측 정렬) — 바 안에서 오른쪽 끝부터 배치
            var row = MessengerPrefabBuilder.Rect("Buttons", bar.transform);
            var rowRt = (RectTransform)row.transform;
            rowRt.anchorMin = new Vector2(1f, 0.5f);
            rowRt.anchorMax = new Vector2(1f, 0.5f);
            rowRt.pivot = new Vector2(1f, 0.5f);
            rowRt.anchoredPosition = new Vector2(-16f, 0f);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = row.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            barView.SaveButton = MakeButton(row.transform, "Save Button", "save", "save_hover", out _);
            barView.LogButton = MakeButton(row.transform, "Log Button", "log", "log_hover", out _);
            barView.AutoButton = MakeButton(row.transform, "Auto Button", "auto_off", "auto_hover", out var autoIcon);
            barView.HideButton = MakeButton(row.transform, "Hide Button", "hide", "hide_hover", out _);
            barView.AutoIcon = autoIcon;
            barView.AutoOnSprite = Load("auto_on");
            barView.AutoOffSprite = Load("auto_off");
            barView.DialogueView = view;

            EditorUtility.SetDirty(bar);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            Debug.Log("[DialogueInfoBarBuilder] InfoBar 조립 완료 → DialogueView.Root/InfoBar (버튼 4종+오토 스프라이트 바인딩). 씬 저장 필요.");
        }

        static Button MakeButton(Transform parent, string name, string sprite, string hoverSprite, out Image icon)
        {
            var go = MessengerPrefabBuilder.Rect(name, parent);
            icon = go.AddComponent<Image>();
            icon.sprite = Load(sprite);
            if (icon.sprite != null) ((RectTransform)go.transform).sizeDelta = icon.sprite.rect.size;
            else ((RectTransform)go.transform).sizeDelta = new Vector2(48f, 48f);

            var btn = go.AddComponent<Button>();
            var hover = Load(hoverSprite);
            if (hover != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                var st = btn.spriteState;
                st.highlightedSprite = hover;
                st.pressedSprite = hover;
                btn.spriteState = st;
            }
            return btn;
        }

        static Sprite Load(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");
            if (sprite == null) Debug.LogWarning($"[DialogueInfoBarBuilder] 스프라이트 없음: {ArtDir}/{name}.png");
            return sprite;
        }

        static void EnsureSpriteImports()
        {
            if (!Directory.Exists(ArtDir)) { Debug.LogWarning($"[DialogueInfoBarBuilder] 아트 폴더 없음: {ArtDir}"); return; }
            foreach (var file in Directory.GetFiles(ArtDir, "*.png"))
            {
                var path = file.Replace('\\', '/');
                if (AssetImporter.GetAtPath(path) is TextureImporter imp && imp.textureType != TextureImporterType.Sprite)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.SaveAndReimport();
                }
            }
        }
    }
}
