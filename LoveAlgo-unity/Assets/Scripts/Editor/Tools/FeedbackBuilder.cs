using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MoreMountains.Feedbacks;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 비주얼 노벨용 FEEL 피드백 빌더 에디터 윈도우
    /// 피드백 프리셋 생성, 씬 배치, 매핑까지 원클릭으로 처리
    /// </summary>
    public class FeedbackBuilder : EditorWindow
    {
        #region 피드백 프리셋 정의

        /// <summary>
        /// 비주얼 노벨에 적합한 피드백 프리셋 목록
        /// </summary>
        public enum FeedbackPreset
        {
            // ─────────────────────────────────────────
            // 화면 효과
            // ─────────────────────────────────────────
            [InspectorName("화면/카메라 흔들림 (약함)")]
            CameraShake_Light,
            
            [InspectorName("화면/카메라 흔들림 (강함)")]
            CameraShake_Heavy,
            
            [InspectorName("화면/화면 플래시 (흰색)")]
            ScreenFlash_White,
            
            [InspectorName("화면/화면 플래시 (빨간색)")]
            ScreenFlash_Red,
            
            // ─────────────────────────────────────────
            // 포스트 프로세싱 (URP)
            // ─────────────────────────────────────────
            [InspectorName("포스트/비네팅 펄스")]
            Vignette_Pulse,
            
            [InspectorName("포스트/비네팅 (긴장)")]
            Vignette_Tension,
            
            [InspectorName("포스트/블룸 (로맨틱)")]
            Bloom_Romantic,
            
            [InspectorName("포스트/색수차 (충격)")]
            ChromaticAberration_Shock,
            
            [InspectorName("포스트/피사계 심도 (집중)")]
            DepthOfField_Focus,
            
            // ─────────────────────────────────────────
            // 캐릭터 효과
            // ─────────────────────────────────────────
            [InspectorName("캐릭터/스케일 펀치 (강조)")]
            Character_ScalePunch,
            
            [InspectorName("캐릭터/점프 (기쁨)")]
            Character_Jump,
            
            [InspectorName("캐릭터/흔들림 (놀람)")]
            Character_Shake,
            
            [InspectorName("캐릭터/페이드 인")]
            Character_FadeIn,
            
            [InspectorName("캐릭터/페이드 아웃")]
            Character_FadeOut,
            
            // ─────────────────────────────────────────
            // 복합 효과 (여러 피드백 조합)
            // ─────────────────────────────────────────
            [InspectorName("복합/충격 (Shake + Flash + Chromatic)")]
            Combo_Shock,
            
            [InspectorName("복합/하트비트 (Scale + Vignette + Sound)")]
            Combo_Heartbeat,
            
            [InspectorName("복합/고백 (Bloom + Vignette + TimeScale)")]
            Combo_Confession,
            
            [InspectorName("복합/분노 (Shake + Vignette + Flash Red)")]
            Combo_Anger,
            
            [InspectorName("복합/슬픔 (Vignette + Saturation Down)")]
            Combo_Sadness,
            
            // ─────────────────────────────────────────
            // UI 효과
            // ─────────────────────────────────────────
            [InspectorName("UI/버튼 클릭")]
            UI_ButtonClick,
            
            [InspectorName("UI/팝업 등장")]
            UI_PopupIn,
            
            [InspectorName("UI/팝업 퇴장")]
            UI_PopupOut,
            
            // ─────────────────────────────────────────
            // 타임 효과
            // ─────────────────────────────────────────
            [InspectorName("시간/슬로우 모션 (짧음)")]
            Time_SlowMotion_Short,
            
            [InspectorName("시간/프리즈 프레임")]
            Time_FreezeFrame,
        }

        /// <summary>
        /// 프리셋별 설정 데이터
        /// </summary>
        [Serializable]
        public class PresetConfig
        {
            public string displayName;
            public string csvName;           // CSV에서 사용할 이름
            public string description;
            public float duration;
            public float intensity;
            public FeedbackType[] feedbackTypes;
        }

        /// <summary>
        /// FEEL 피드백 타입
        /// </summary>
        public enum FeedbackType
        {
            CameraShake,
            Flash,
            Vignette_URP,
            Bloom_URP,
            ChromaticAberration_URP,
            DepthOfField_URP,
            ColorAdjustments_URP,
            Scale,
            Position,
            PositionShake,
            SpriteRendererAlpha,
            Image,
            TimescaleModifier,
            FreezeFrame,
            Sound,
        }

        #endregion

        #region 윈도우 상태

        Vector2 scrollPosition;
        FeedbackPreset selectedPreset = FeedbackPreset.CameraShake_Light;
        string customName = "";
        
        // 프리셋별 상세 설정
        float duration = 0.3f;
        float intensity = 1.0f;
        bool autoRegister = true;
        Transform targetTransform;
        
        // FeedbackManager 참조
        Core.FeedbackManager feedbackManager;

        #endregion

        #region 메뉴

        [MenuItem("LoveAlgo/FEEL/Feedback Builder", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<FeedbackBuilder>("Feedback Builder");
            window.minSize = new Vector2(400, 600);
        }

        #endregion

        #region GUI

        void OnEnable()
        {
            FindFeedbackManager();
        }

        void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawPresetSelector();
            EditorGUILayout.Space(10);
            
            DrawPresetSettings();
            EditorGUILayout.Space(10);
            
            DrawCreateButton();
            EditorGUILayout.Space(20);
            
            DrawRegisteredFeedbacks();

            EditorGUILayout.EndScrollView();
        }

        void DrawHeader()
        {
            EditorGUILayout.LabelField("🎮 FEEL Feedback Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "비주얼 노벨에 최적화된 FEEL 피드백 프리셋을 생성합니다.\n" +
                "생성된 피드백은 자동으로 FeedbackManager에 등록됩니다.",
                MessageType.Info
            );
        }

        void DrawPresetSelector()
        {
            EditorGUILayout.LabelField("프리셋 선택", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                selectedPreset = (FeedbackPreset)EditorGUILayout.EnumPopup("프리셋", selectedPreset);
                
                var config = GetPresetConfig(selectedPreset);
                EditorGUILayout.LabelField("설명", config.description, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("CSV 이름", config.csvName);
                EditorGUILayout.LabelField("기본 시간", $"{config.duration:F2}초");
            }
        }

        void DrawPresetSettings()
        {
            EditorGUILayout.LabelField("설정", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var config = GetPresetConfig(selectedPreset);
                
                customName = EditorGUILayout.TextField("이름 (비우면 기본값)", customName);
                duration = EditorGUILayout.FloatField("Duration (초)", duration);
                intensity = EditorGUILayout.Slider("Intensity", intensity, 0.1f, 3.0f);
                
                // 캐릭터 효과일 경우 타겟 선택
                if (IsCharacterPreset(selectedPreset))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("캐릭터 타겟", EditorStyles.miniLabel);
                    targetTransform = (Transform)EditorGUILayout.ObjectField(
                        "Target Transform", targetTransform, typeof(Transform), true);
                }
                
                EditorGUILayout.Space(5);
                autoRegister = EditorGUILayout.Toggle("FeedbackManager에 자동 등록", autoRegister);
            }
        }

        void DrawCreateButton()
        {
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("📦 피드백 생성", GUILayout.Height(40)))
            {
                CreateFeedback();
            }
            
            if (GUILayout.Button("🔄 새로고침", GUILayout.Height(40), GUILayout.Width(80)))
            {
                FindFeedbackManager();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        void DrawRegisteredFeedbacks()
        {
            EditorGUILayout.LabelField("등록된 피드백", EditorStyles.boldLabel);
            
            if (feedbackManager == null)
            {
                EditorGUILayout.HelpBox(
                    "FeedbackManager를 찾을 수 없습니다.\n" +
                    "씬에 FeedbackManager를 추가해주세요.",
                    MessageType.Warning
                );
                
                if (GUILayout.Button("FeedbackManager 생성"))
                {
                    CreateFeedbackManager();
                }
                return;
            }
            
            EditorGUILayout.ObjectField("FeedbackManager", feedbackManager, typeof(Core.FeedbackManager), true);
            
            // SerializedObject를 통해 entries 배열 표시
            var so = new SerializedObject(feedbackManager);
            var entriesProp = so.FindProperty("entries");
            
            if (entriesProp != null && entriesProp.isArray)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"등록된 피드백: {entriesProp.arraySize}개");
                    
                    for (int i = 0; i < entriesProp.arraySize; i++)
                    {
                        var entry = entriesProp.GetArrayElementAtIndex(i);
                        var nameProp = entry.FindPropertyRelative("name");
                        var playerProp = entry.FindPropertyRelative("player");
                        
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(nameProp?.stringValue ?? "(없음)", GUILayout.Width(150));
                            
                            bool hasPlayer = playerProp?.objectReferenceValue != null;
                            GUI.color = hasPlayer ? Color.green : Color.yellow;
                            EditorGUILayout.LabelField(hasPlayer ? "✓ 연결됨" : "⚠ 미연결", GUILayout.Width(80));
                            GUI.color = Color.white;
                            
                            if (GUILayout.Button("선택", GUILayout.Width(50)))
                            {
                                if (playerProp?.objectReferenceValue != null)
                                {
                                    Selection.activeObject = playerProp.objectReferenceValue;
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region 피드백 생성

        void CreateFeedback()
        {
            var config = GetPresetConfig(selectedPreset);
            string feedbackName = string.IsNullOrEmpty(customName) ? config.csvName : customName;
            
            // 피드백 GameObject 생성
            var feedbackGO = new GameObject($"Feedback_{feedbackName}");
            
            // FeedbackManager 하위에 배치
            if (feedbackManager != null)
            {
                feedbackGO.transform.SetParent(feedbackManager.transform);
            }
            
            // MMF_Player 추가
            var player = feedbackGO.AddComponent<MMF_Player>();
            player.InitializationMode = MMF_Player.InitializationModes.Awake;
            
            // 프리셋에 따른 피드백 추가
            AddFeedbacksToPlayer(player, config);
            
            // FeedbackManager에 등록
            if (autoRegister && feedbackManager != null)
            {
                RegisterToFeedbackManager(feedbackName, player);
            }
            
            // 선택
            Selection.activeGameObject = feedbackGO;
            
            Debug.Log($"[FeedbackBuilder] '{feedbackName}' 피드백 생성 완료!");
            EditorUtility.SetDirty(feedbackManager);
        }

        void AddFeedbacksToPlayer(MMF_Player player, PresetConfig config)
        {
            foreach (var type in config.feedbackTypes)
            {
                AddFeedback(player, type, config);
            }
        }

        void AddFeedback(MMF_Player player, FeedbackType type, PresetConfig config)
        {
            switch (type)
            {
                case FeedbackType.CameraShake:
                    var shake = (MMF_CameraShake)player.AddFeedback(typeof(MMF_CameraShake));
                    shake.CameraShakeProperties.Duration = duration;
                    shake.CameraShakeProperties.Amplitude = intensity * 2f;
                    shake.CameraShakeProperties.Frequency = 25f;
                    break;
                    
                case FeedbackType.Flash:
                    var flash = (MMF_Flash)player.AddFeedback(typeof(MMF_Flash));
                    flash.FlashDuration = duration;
                    break;
                    
                case FeedbackType.Scale:
                    var scale = (MMF_Scale)player.AddFeedback(typeof(MMF_Scale));
                    scale.AnimateScaleDuration = duration;
                    scale.RemapCurveOne = 1f + (intensity * 0.2f);
                    if (targetTransform != null)
                        scale.AnimateScaleTarget = targetTransform;
                    break;
                    
                case FeedbackType.Position:
                    var pos = (MMF_Position)player.AddFeedback(typeof(MMF_Position));
                    pos.AnimatePositionDuration = duration;
                    break;
                    
                case FeedbackType.PositionShake:
                    var posShake = (MMF_PositionShake)player.AddFeedback(typeof(MMF_PositionShake));
                    posShake.Duration = duration;
                    posShake.ShakeSpeed = 20f;
                    posShake.ShakeRange = intensity * 10f;
                    break;
                    
                case FeedbackType.TimescaleModifier:
                    var timescale = (MMF_TimescaleModifier)player.AddFeedback(typeof(MMF_TimescaleModifier));
                    timescale.TimeScaleDuration = duration;
                    timescale.TimeScale = 0.1f;
                    break;
                    
                case FeedbackType.FreezeFrame:
                    var freeze = (MMF_FreezeFrame)player.AddFeedback(typeof(MMF_FreezeFrame));
                    freeze.FreezeFrameDuration = duration;
                    break;
                    
                // URP 피드백은 #if MM_URP로 감싸야 함
                // 여기서는 기본 구조만 제공
                    
                default:
                    Debug.LogWarning($"[FeedbackBuilder] {type} 피드백은 수동 설정이 필요합니다.");
                    break;
            }
        }

        void RegisterToFeedbackManager(string name, MMF_Player player)
        {
            var so = new SerializedObject(feedbackManager);
            var entriesProp = so.FindProperty("entries");
            
            // 기존에 같은 이름이 있는지 확인
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entry = entriesProp.GetArrayElementAtIndex(i);
                var nameProp = entry.FindPropertyRelative("name");
                if (nameProp.stringValue == name)
                {
                    // 기존 항목 업데이트
                    entry.FindPropertyRelative("player").objectReferenceValue = player;
                    so.ApplyModifiedProperties();
                    Debug.Log($"[FeedbackBuilder] 기존 '{name}' 항목 업데이트");
                    return;
                }
            }
            
            // 새 항목 추가
            entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
            var newEntry = entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1);
            newEntry.FindPropertyRelative("name").stringValue = name;
            newEntry.FindPropertyRelative("player").objectReferenceValue = player;
            newEntry.FindPropertyRelative("duration").floatValue = duration;
            
            so.ApplyModifiedProperties();
            Debug.Log($"[FeedbackBuilder] '{name}' 항목 추가 완료");
        }

        #endregion

        #region 유틸리티

        void FindFeedbackManager()
        {
            feedbackManager = FindAnyObjectByType<Core.FeedbackManager>();
        }

        void CreateFeedbackManager()
        {
            var go = new GameObject("FeedbackManager");
            feedbackManager = go.AddComponent<Core.FeedbackManager>();
            Selection.activeGameObject = go;
            Debug.Log("[FeedbackBuilder] FeedbackManager 생성 완료");
        }

        bool IsCharacterPreset(FeedbackPreset preset)
        {
            return preset == FeedbackPreset.Character_ScalePunch ||
                   preset == FeedbackPreset.Character_Jump ||
                   preset == FeedbackPreset.Character_Shake ||
                   preset == FeedbackPreset.Character_FadeIn ||
                   preset == FeedbackPreset.Character_FadeOut;
        }

        PresetConfig GetPresetConfig(FeedbackPreset preset)
        {
            return preset switch
            {
                // 화면 효과
                FeedbackPreset.CameraShake_Light => new PresetConfig
                {
                    displayName = "카메라 흔들림 (약함)",
                    csvName = "ShakeLight",
                    description = "문 닫힘, 가벼운 충격 등에 사용",
                    duration = 0.15f,
                    intensity = 0.5f,
                    feedbackTypes = new[] { FeedbackType.CameraShake }
                },
                
                FeedbackPreset.CameraShake_Heavy => new PresetConfig
                {
                    displayName = "카메라 흔들림 (강함)",
                    csvName = "ShakeHeavy",
                    description = "폭발, 강한 충격 등에 사용",
                    duration = 0.3f,
                    intensity = 2.0f,
                    feedbackTypes = new[] { FeedbackType.CameraShake }
                },
                
                FeedbackPreset.ScreenFlash_White => new PresetConfig
                {
                    displayName = "화면 플래시 (흰색)",
                    csvName = "FlashWhite",
                    description = "번개, 카메라 플래시 효과",
                    duration = 0.1f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.Flash }
                },
                
                FeedbackPreset.ScreenFlash_Red => new PresetConfig
                {
                    displayName = "화면 플래시 (빨간색)",
                    csvName = "FlashRed",
                    description = "피격, 위험 경고 효과",
                    duration = 0.15f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.Flash }
                },
                
                // 포스트 프로세싱
                FeedbackPreset.Vignette_Pulse => new PresetConfig
                {
                    displayName = "비네팅 펄스",
                    csvName = "VignettePulse",
                    description = "하트비트, 긴장감 고조",
                    duration = 0.5f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.Vignette_URP }
                },
                
                FeedbackPreset.Vignette_Tension => new PresetConfig
                {
                    displayName = "비네팅 (긴장)",
                    csvName = "Tension",
                    description = "서서히 조여오는 긴장감",
                    duration = 1.0f,
                    intensity = 1.5f,
                    feedbackTypes = new[] { FeedbackType.Vignette_URP }
                },
                
                FeedbackPreset.Bloom_Romantic => new PresetConfig
                {
                    displayName = "블룸 (로맨틱)",
                    csvName = "Romantic",
                    description = "로맨틱한 순간, 회상 장면",
                    duration = 0.8f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.Bloom_URP }
                },
                
                FeedbackPreset.ChromaticAberration_Shock => new PresetConfig
                {
                    displayName = "색수차 (충격)",
                    csvName = "Distort",
                    description = "충격, 어지러움, 혼란",
                    duration = 0.2f,
                    intensity = 1.5f,
                    feedbackTypes = new[] { FeedbackType.ChromaticAberration_URP }
                },
                
                FeedbackPreset.DepthOfField_Focus => new PresetConfig
                {
                    displayName = "피사계 심도 (집중)",
                    csvName = "Focus",
                    description = "특정 대상에 집중",
                    duration = 0.5f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.DepthOfField_URP }
                },
                
                // 캐릭터 효과
                FeedbackPreset.Character_ScalePunch => new PresetConfig
                {
                    displayName = "스케일 펀치 (강조)",
                    csvName = "CharPunch",
                    description = "캐릭터 강조, 놀람",
                    duration = 0.2f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.Scale }
                },
                
                FeedbackPreset.Character_Jump => new PresetConfig
                {
                    displayName = "점프 (기쁨)",
                    csvName = "CharJump",
                    description = "캐릭터 기쁨 표현",
                    duration = 0.3f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.Position }
                },
                
                FeedbackPreset.Character_Shake => new PresetConfig
                {
                    displayName = "흔들림 (놀람)",
                    csvName = "CharShake",
                    description = "캐릭터 놀람, 당황",
                    duration = 0.25f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.PositionShake }
                },
                
                FeedbackPreset.Character_FadeIn => new PresetConfig
                {
                    displayName = "페이드 인",
                    csvName = "CharFadeIn",
                    description = "캐릭터 등장",
                    duration = 0.3f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.SpriteRendererAlpha }
                },
                
                FeedbackPreset.Character_FadeOut => new PresetConfig
                {
                    displayName = "페이드 아웃",
                    csvName = "CharFadeOut",
                    description = "캐릭터 퇴장",
                    duration = 0.3f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.SpriteRendererAlpha }
                },
                
                // 복합 효과
                FeedbackPreset.Combo_Shock => new PresetConfig
                {
                    displayName = "충격",
                    csvName = "Shock",
                    description = "충격적인 순간 (Shake + Flash + Chromatic)",
                    duration = 0.3f,
                    intensity = 1.5f,
                    feedbackTypes = new[] { FeedbackType.CameraShake, FeedbackType.Flash, FeedbackType.ChromaticAberration_URP }
                },
                
                FeedbackPreset.Combo_Heartbeat => new PresetConfig
                {
                    displayName = "하트비트",
                    csvName = "Heartbeat",
                    description = "두근두근 (Scale + Vignette)",
                    duration = 0.8f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.Scale, FeedbackType.Vignette_URP }
                },
                
                FeedbackPreset.Combo_Confession => new PresetConfig
                {
                    displayName = "고백",
                    csvName = "Confession",
                    description = "고백 장면 (Bloom + Vignette + TimeScale)",
                    duration = 1.0f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.Bloom_URP, FeedbackType.Vignette_URP, FeedbackType.TimescaleModifier }
                },
                
                FeedbackPreset.Combo_Anger => new PresetConfig
                {
                    displayName = "분노",
                    csvName = "Anger",
                    description = "분노 표현 (Shake + Vignette + Flash)",
                    duration = 0.4f,
                    intensity = 2.0f,
                    feedbackTypes = new[] { FeedbackType.CameraShake, FeedbackType.Vignette_URP, FeedbackType.Flash }
                },
                
                FeedbackPreset.Combo_Sadness => new PresetConfig
                {
                    displayName = "슬픔",
                    csvName = "Sadness",
                    description = "슬픈 장면 (Vignette + 채도 감소)",
                    duration = 1.5f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.Vignette_URP, FeedbackType.ColorAdjustments_URP }
                },
                
                // UI 효과
                FeedbackPreset.UI_ButtonClick => new PresetConfig
                {
                    displayName = "버튼 클릭",
                    csvName = "UIClick",
                    description = "버튼 클릭 피드백",
                    duration = 0.1f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.Scale }
                },
                
                FeedbackPreset.UI_PopupIn => new PresetConfig
                {
                    displayName = "팝업 등장",
                    csvName = "UIPopupIn",
                    description = "팝업 등장 애니메이션",
                    duration = 0.25f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.Scale }
                },
                
                FeedbackPreset.UI_PopupOut => new PresetConfig
                {
                    displayName = "팝업 퇴장",
                    csvName = "UIPopupOut",
                    description = "팝업 퇴장 애니메이션",
                    duration = 0.2f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.Scale }
                },
                
                // 타임 효과
                FeedbackPreset.Time_SlowMotion_Short => new PresetConfig
                {
                    displayName = "슬로우 모션 (짧음)",
                    csvName = "SlowMo",
                    description = "짧은 슬로우 모션",
                    duration = 0.5f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.TimescaleModifier }
                },
                
                FeedbackPreset.Time_FreezeFrame => new PresetConfig
                {
                    displayName = "프리즈 프레임",
                    csvName = "Freeze",
                    description = "순간 정지",
                    duration = 0.1f,
                    intensity = 1.0f,
                    feedbackTypes = new[] { FeedbackType.FreezeFrame }
                },
                
                _ => new PresetConfig
                {
                    displayName = "Unknown",
                    csvName = "Unknown",
                    description = "알 수 없는 프리셋",
                    duration = 0.3f,
                    intensity = 1.0f,
                    feedbackTypes = new FeedbackType[0]
                }
            };
        }

        #endregion
    }
}
