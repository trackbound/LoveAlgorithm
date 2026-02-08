using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 하이어라키 정리 헬퍼 (1회 실행용)
    /// 메뉴: Tools > Setup Hierarchy Groups
    /// </summary>
    public static class HierarchySetup
    {
        [MenuItem("Tools/Setup Hierarchy Groups")]
        static void SetupGroups()
        {
            // Cvs_Main/Main 하위에 그룹 생성
            var main = GameObject.Find("Cvs_Main")?.transform.Find("Main");
            if (main == null)
            {
                Debug.LogError("[HierarchySetup] Cvs_Main/Main을 찾을 수 없습니다.");
                return;
            }

            // 1. Gameplay 그룹
            var gameplay = CreateRectGroup("Gameplay", main);
            MoveChild(main, "DialoguePanel", gameplay);
            MoveChild(main, "ChoicePanel", gameplay);
            MoveChild(main, "ShowBtn", gameplay);

            // 2. Menus 그룹
            var menus = CreateRectGroup("Menus", main);
            MoveChild(main, "Title", menus);      // 타이틀 화면 (비활성일 수 있음)
            MoveChild(main, "Username", menus);    // 유저네임 입력 (비활성일 수 있음)
            MoveChild(main, "Schedule", menus);    // 스케줄 (비활성일 수 있음)

            // 3. Debug 그룹
            var debug = CreateRectGroup("Debug", main);
            MoveChild(main, "DebugJumpHelper", debug);
            MoveChild(main, "Mockup", debug);

            // 비활성 오브젝트 이름 변경
            RenameInactive(menus, "Title", "TitlePanel");
            RenameInactive(menus, "Username", "UsernamePanel");
            RenameInactive(menus, "Schedule", "SchedulePanel");
            RenameInactive(debug, "Mockup", "MockupOverlay");

            // EyeEffect 그룹화 → Cvs_Stage 직하 (Stage 밖, 흔들림 X)
            var stage = GameObject.Find("Cvs_Stage")?.transform.Find("Stage");
            var cvsStage = GameObject.Find("Cvs_Stage")?.transform;
            if (stage != null && cvsStage != null)
            {
                // Overlay → VirtualBGOverlay 이름 변경 + 순서 정리
                // sibling 순서: Background(0) → VirtualBGOverlay(1) → Characters(2) → CG(3)
                RenameInactive(stage, "Overlay", "VirtualBGOverlay");
                SetSiblingByName(stage, "Background", 0);
                SetSiblingByName(stage, "VirtualBGOverlay", 1);
                SetSiblingByName(stage, "Characters", 2);
                SetSiblingByName(stage, "CG", 3);

                // EyeEffect 그룹 생성 → Cvs_Stage 직하 (Stage 밖)
                var eyeEffect = CreateRectGroup("EyeEffect", cvsStage);
                for (int i = 0; i < stage.childCount; i++)
                {
                    var child = stage.GetChild(i);
                    if (child.name == "EyeTop")
                    {
                        child.name = "Top";
                        child.SetParent(eyeEffect, false);
                        i--;
                    }
                    else if (child.name == "EyeBottom")
                    {
                        child.name = "Bottom";
                        child.SetParent(eyeEffect, false);
                        i--;
                    }
                }
            }

            // Manager 순서 정리
            var managers = GameObject.Find("== Managers ==")?.transform;
            if (managers != null)
            {
                SetSiblingByName(managers, "GameManager", 0);
                SetSiblingByName(managers, "GameState", 1);
                SetSiblingByName(managers, "StorySystem", 2);
                SetSiblingByName(managers, "StageManager", 3);
                SetSiblingByName(managers, "UIManager", 4);
                SetSiblingByName(managers, "AudioManager", 5);
            }

            EditorUtility.SetDirty(main.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("[HierarchySetup] 하이어라키 정리 완료!");
        }

        static RectTransform CreateRectGroup(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            // 풀스트레치
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return rt;
        }

        static void MoveChild(Transform parent, string childName, Transform newParent)
        {
            // 비활성 포함 찾기
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == childName)
                {
                    child.SetParent(newParent, false);
                    return;
                }
            }
            Debug.LogWarning($"[HierarchySetup] '{childName}'을 찾을 수 없습니다.");
        }

        static void RenameInactive(Transform parent, string oldName, string newName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == oldName)
                {
                    child.name = newName;
                    return;
                }
            }
        }

        static void SetSiblingByName(Transform parent, string childName, int index)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == childName)
                {
                    child.SetSiblingIndex(index);
                    return;
                }
            }
        }
    }
}
