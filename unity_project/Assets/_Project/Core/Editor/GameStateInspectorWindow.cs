#if UNITY_EDITOR
using LoveAlgo.Core;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.CoreEditor
{
    /// <summary>
    /// 플레이 모드에서 GameState 값을 즉시 보고 수정.
    /// 메뉴: Tools > LoveAlgo > Debug > GameState Inspector
    ///
    /// 스탯 5종 + 머니 + 캐릭터별 호감도(lovePoints) 슬라이더.
    /// </summary>
    public class GameStateInspectorWindow : EditorWindow
    {
        static readonly string[] Stats = { "Strength", "Intelligence", "Sociability", "Perseverance", "Fatigue" };
        static readonly string[] StatKeys = { "Str", "Int", "Soc", "Per", "Fatigue" };
        static readonly string[] Heroines = { "Roa", "SeoDaEun", "HaYeEun", "DoHeewon", "LeeBom" };

        Vector2 scroll;

        [MenuItem("Tools/LoveAlgo/Debug/GameState Inspector")]
        public static void Open()
        {
            var w = GetWindow<GameStateInspectorWindow>("GameState");
            w.minSize = new Vector2(440, 460);
            w.Show();
        }

        void OnInspectorUpdate() => Repaint();   // 1초마다 갱신 (외부 변경 반영)

        void OnGUI()
        {
            EditorGUILayout.LabelField("GameState Inspector", EditorStyles.boldLabel);

            var gs = GameState.Instance;
            if (!Application.isPlaying || gs == null)
            {
                EditorGUILayout.HelpBox("플레이 모드에서만 동작합니다.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            // 머니
            EditorGUILayout.LabelField("Money", EditorStyles.boldLabel);
            DrawIntRow("Money", gs.Money, 0, 1000000, v => gs.SetMoney(v));
            EditorGUILayout.Space(6);

            // 스탯
            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            for (int i = 0; i < Stats.Length; i++)
            {
                var key = StatKeys[i];
                DrawIntRow(Stats[i], gs.GetStat(key), 0, 100, v => gs.SetStat(key, v));
            }
            EditorGUILayout.Space(6);

            // 호감도
            EditorGUILayout.LabelField("Love Points", EditorStyles.boldLabel);
            foreach (var hero in Heroines)
                DrawIntRow(hero, gs.GetLove(hero), 0, 100, v => gs.SetLove(hero, v));

            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("All Stats → 50", GUILayout.Height(24)))
                    foreach (var k in StatKeys) gs.SetStat(k, 50);
                if (GUILayout.Button("All Love → 30", GUILayout.Height(24)))
                    foreach (var h in Heroines) gs.SetLove(h, 30);
                if (GUILayout.Button("Money +10000", GUILayout.Height(24)))
                    gs.AddMoney(10000);
            }

            EditorGUILayout.EndScrollView();
        }

        static void DrawIntRow(string label, int current, int min, int max, System.Action<int> apply)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(110));
                int newVal = EditorGUILayout.IntSlider(current, min, max);
                if (newVal != current) apply(newVal);
            }
        }
    }
}
#endif
