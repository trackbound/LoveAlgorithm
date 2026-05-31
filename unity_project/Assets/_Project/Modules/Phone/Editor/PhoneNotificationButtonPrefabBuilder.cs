using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Phone;

namespace LoveAlgo.Phone.EditorTools
{
    /// <summary>
    /// PhoneNotificationButton prefab 자동 생성/재구성 도구.
    ///
    /// 구조 (한 PNG 통이미지 + 핸들 스왑):
    ///   PhoneNotificationButton (root)
    ///   └ HandleVisuals (slideContainer) — 호버 시 좌측 슬라이드
    ///     ├ NormalImage     (btn_phone)      — 평시 핸들 (메시지 0)
    ///     ├ NewMessageImage (btn_phone_new)  — 새 메시지 시 (N 통합 PNG), 기본 비활성
    ///     └ ClickButton                       — 전체 영역 클릭 (메신저 오픈)
    ///
    /// PhoneNotificationButton 컴포넌트의 expandedView/labelText는 None — PNG 통이미지라 불필요.
    ///
    /// 메뉴: Tools > Phone > Create or Rebuild PhoneNotificationButton Prefab
    /// </summary>
    public static class PhoneNotificationButtonPrefabBuilder
    {
        const string PrefabPath = "Assets/_Project/Modules/Narrative/Prefabs/PhoneNotificationButton.prefab";
        const string PrefabDir  = "Assets/_Project/Modules/Narrative/Prefabs";

        // 스프라이트 후보 경로 (첫 번째 발견 사용)
        static readonly string[] NormalSpriteCandidates =
        {
            "Assets/Art/btn_phone.png",
            "Assets/_Project/Modules/Phone/Art/PhoneButton/btn_phone.png",
        };
        static readonly string[] NewSpriteCandidates =
        {
            "Assets/Art/btn_phone_new.png",
            "Assets/_Project/Modules/Phone/Art/PhoneButton/btn_phone_new.png",
        };

        [MenuItem("Tools/Phone/Create or Rebuild PhoneNotificationButton Prefab")]
        public static void CreateOrRebuild()
        {
            // 1. 스프라이트 로드
            var normalSprite = FindFirstSprite(NormalSpriteCandidates);
            var newSprite    = FindFirstSprite(NewSpriteCandidates);
            if (normalSprite == null)
            {
                EditorUtility.DisplayDialog("스프라이트 누락",
                    "btn_phone.png 못 찾음. 후보 경로:\n" + string.Join("\n", NormalSpriteCandidates),
                    "OK");
                return;
            }
            if (newSprite == null)
            {
                EditorUtility.DisplayDialog("스프라이트 누락",
                    "btn_phone_new.png 못 찾음. 후보 경로:\n" + string.Join("\n", NewSpriteCandidates),
                    "OK");
                return;
            }

            // 2. 기존 prefab 있으면 확인 다이얼로그
            bool existed = File.Exists(PrefabPath);
            if (existed)
            {
                bool ok = EditorUtility.DisplayDialog("Prefab 재구성",
                    $"기존 prefab을 덮어씁니다:\n{PrefabPath}\n\n" +
                    "씬에 배치된 인스턴스의 prefab override 일부가 손실될 수 있습니다.\n계속할까요?",
                    "재구성", "취소");
                if (!ok) return;
            }

            // 3. 임시 GameObject 트리 구성
            var root = new GameObject("PhoneNotificationButton", typeof(RectTransform));
            var rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = new Vector2(1, 1);
            rootRT.anchorMax = new Vector2(1, 1);
            rootRT.pivot     = new Vector2(1, 1);
            rootRT.sizeDelta        = new Vector2(180, 80);
            rootRT.anchoredPosition = new Vector2(-20, -20);

            // HandleVisuals (slideContainer)
            var handle = new GameObject("HandleVisuals", typeof(RectTransform));
            handle.transform.SetParent(root.transform, false);
            var handleRT = handle.GetComponent<RectTransform>();
            handleRT.anchorMin = handleRT.anchorMax = handleRT.pivot = new Vector2(0.5f, 0.5f);
            handleRT.sizeDelta        = new Vector2(180, 80);
            handleRT.anchoredPosition = Vector2.zero;

            // NormalImage
            var normalGO = MakeImage("NormalImage", handle.transform, normalSprite, fill: true);
            // NewMessageImage (기본 비활성)
            var newGO    = MakeImage("NewMessageImage", handle.transform, newSprite, fill: true);
            newGO.SetActive(false);
            // ClickButton (투명 raycast)
            var clickGO = new GameObject("ClickButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            clickGO.transform.SetParent(handle.transform, false);
            var clickRT = clickGO.GetComponent<RectTransform>();
            clickRT.anchorMin = Vector2.zero; clickRT.anchorMax = Vector2.one;
            clickRT.offsetMin = Vector2.zero; clickRT.offsetMax = Vector2.zero;
            var clickImg = clickGO.GetComponent<Image>();
            clickImg.color = new Color(1, 1, 1, 0); // 투명 — raycast만
            var clickBtn = clickGO.GetComponent<Button>();
            clickBtn.targetGraphic = clickImg;

            // 4. PhoneNotificationButton 컴포넌트 + 필드 바인딩
            var component = root.AddComponent<PhoneNotificationButton>();
            var so = new SerializedObject(component);
            SetRef(so, "slideContainer",   handleRT);
            SetRef(so, "normalImage",      normalGO);
            SetRef(so, "newMessageImage",  newGO);
            SetRef(so, "openButton",       clickBtn);
            SetRef(so, "expandedView",     null);   // PNG 통이미지 — 사용 안 함
            SetRef(so, "labelText",        null);   // PNG에 텍스트 포함 — 사용 안 함
            so.ApplyModifiedProperties();

            // 5. Prefab 저장 + 임시 GO 제거
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
            Debug.Log($"[PhonePrefabBuilder] {(existed ? "재구성" : "신규 생성")} 완료: {PrefabPath}\n" +
                      $"  ├ HandleVisuals (slideContainer)\n" +
                      $"  │  ├ NormalImage ← {AssetDatabase.GetAssetPath(normalSprite)}\n" +
                      $"  │  ├ NewMessageImage ← {AssetDatabase.GetAssetPath(newSprite)} (비활성)\n" +
                      $"  │  └ ClickButton (투명 raycast)\n" +
                      $"  └ expandedView/labelText = None");
        }

        // ── 헬퍼 ──────────────────────────────────────────

        static Sprite FindFirstSprite(string[] paths)
        {
            foreach (var p in paths)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (s != null) return s;
            }
            return null;
        }

        static GameObject MakeImage(string name, Transform parent, Sprite sprite, bool fill)
        {
            var go = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (fill)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false; // ClickButton만 raycast
            img.preserveAspect = true;
            return go;
        }

        static void SetRef(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[PhonePrefabBuilder] '{fieldName}' 필드를 PhoneNotificationButton에서 못 찾음 — 스킵");
                return;
            }
            prop.objectReferenceValue = value;
        }
    }
}
