#if UNITY_EDITOR
using LoveAlgo.Contracts;
using LoveAlgo.Core;
using LoveAlgo.Modules.Affinity;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.AffinityEditor
{
    /// <summary>
    /// 플레이 모드에서 히로인별 포인트(카테고리별) 조정 + 임계치 비교.
    /// 메뉴: Tools > LoveAlgo > Debug > Affinity Tuner
    ///
    /// HeroinePointTracker가 static이라 직접 호출. AddPoint(delta)로 적용.
    /// </summary>
    public class AffinityTunerWindow : EditorWindow
    {
        Vector2 scroll;
        // 입력 캐시: heroineId+category → delta
        readonly System.Collections.Generic.Dictionary<string, int> deltaCache = new();

        [MenuItem("Tools/LoveAlgo/Debug/Affinity Tuner")]
        public static void Open()
        {
            var w = GetWindow<AffinityTunerWindow>("Affinity");
            w.minSize = new Vector2(560, 480);
            w.Show();
        }

        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            EditorGUILayout.LabelField("Affinity Tuner", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서만 동작합니다.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (var id in GameConstants.HeroineIds)
                DrawHeroine(id);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Print Summary to Console", GUILayout.Height(24)))
            {
                foreach (var id in GameConstants.HeroineIds)
                    Debug.Log($"[Affinity] {id}: total={HeroinePointTracker.GetTotalPoint(id)} (E={HeroinePointTracker.GetPoint(id, PointCategory.Event)}, D={HeroinePointTracker.GetPoint(id, PointCategory.Dialogue)}, G={HeroinePointTracker.GetPoint(id, PointCategory.Gift)}, M={HeroinePointTracker.GetPoint(id, PointCategory.MiniGame)})");
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawHeroine(string heroineId)
        {
            bool hasCfg = GameConstants.HeroineById.TryGetValue(heroineId, out var cfg);
            int total = HeroinePointTracker.GetTotalPoint(heroineId);
            int threshold = hasCfg ? cfg.EndingThreshold : 30;
            string displayName = hasCfg ? cfg.DisplayName : heroineId;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{displayName} ({heroineId})", EditorStyles.boldLabel, GUILayout.Width(150));
                    var thresholdColor = total >= threshold ? "✓" : "✗";
                    EditorGUILayout.LabelField($"Total: {total} / {threshold} {thresholdColor}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Event x{HeroinePointTracker.GetEventSelectionCount(heroineId)}", EditorStyles.miniLabel, GUILayout.Width(80));
                }

                foreach (PointCategory cat in System.Enum.GetValues(typeof(PointCategory)))
                    DrawCategoryRow(heroineId, cat);
            }
        }

        void DrawCategoryRow(string heroineId, PointCategory cat)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int cur = HeroinePointTracker.GetPoint(heroineId, cat);
                EditorGUILayout.LabelField($"  {cat}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"= {cur}", GUILayout.Width(40));

                var key = heroineId + "_" + cat;
                if (!deltaCache.TryGetValue(key, out var delta)) delta = 1;
                delta = EditorGUILayout.IntField(delta, GUILayout.Width(50));
                deltaCache[key] = delta;

                if (GUILayout.Button("+", GUILayout.Width(28)))
                    HeroinePointTracker.AddPoint(heroineId, cat, delta);
                if (GUILayout.Button("−", GUILayout.Width(28)))
                    HeroinePointTracker.AddPoint(heroineId, cat, -delta);
            }
        }
    }
}
#endif
