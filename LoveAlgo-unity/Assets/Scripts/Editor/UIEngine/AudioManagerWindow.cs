using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

// 런타임 타입 별칭 (UnityEngine.AudioSettings와 충돌 방지)
using LoveAlgoAudioSettings = LoveAlgo.Story.AudioSettings;
using LoveAlgoCharacterBGMMapping = LoveAlgo.Story.CharacterBGMMapping;

namespace LoveAlgo.Editor.UIEngine
{
    /// <summary>
    /// Audio Manager - BGM/SFX 미리듣기 + 캐릭터별 매핑 관리 툴
    /// </summary>
    public class AudioManagerWindow : EditorWindow
    {
        [MenuItem("LoveAlgo/Audio Manager %#a", priority = 101)]
        static void OpenWindow()
        {
            var window = GetWindow<AudioManagerWindow>();
            window.titleContent = new GUIContent("Audio Manager", EditorGUIUtility.IconContent("d_AudioSource Icon").image);
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        // 탭
        enum Tab { BGM, SFX, Voice, Settings }
        Tab currentTab = Tab.BGM;

        // 데이터
        List<AudioFileData> bgmFiles = new();
        List<AudioFileData> sfxFiles = new();
        List<AudioFileData> voiceFiles = new();
        LoveAlgoAudioSettings audioSettings;

        // 재생 상태
        AudioFileData currentPlaying;
        AudioSource previewSource;
        double playStartTime;
        bool isPlaying;

        // 검색/필터
        string searchFilter = "";
        
        // 스크롤
        Vector2 leftScrollPos;
        Vector2 rightScrollPos;
        float splitWidth = 400f;
        bool isDraggingSplit;

        // 캐릭터 목록 (설정용)
        readonly string[] defaultCharacters = { "로아", "하예은", "서다은", "이봄", "도희원" };

        class AudioFileData
        {
            public string name;
            public string path;           // 전체 경로
            public string resourcePath;   // Resources 이후 경로
            public AudioClip clip;
            public float duration;
            public bool isLoaded;
        }

        void OnEnable()
        {
            LoadSettings();
            ScanAudioFiles();
            CreatePreviewSource();
        }

        void OnDisable()
        {
            StopPreview();
            if (previewSource != null)
            {
                DestroyImmediate(previewSource.gameObject);
            }
        }

        void CreatePreviewSource()
        {
            if (previewSource != null) return;
            
            var go = new GameObject("AudioPreview") { hideFlags = HideFlags.HideAndDontSave };
            previewSource = go.AddComponent<AudioSource>();
            previewSource.playOnAwake = false;
        }

        void OnGUI()
        {
            DrawToolbar();
            DrawTabs();
            DrawMainArea();
            
            // 재생 중이면 Repaint
            if (isPlaying)
            {
                Repaint();
                CheckPlaybackEnd();
            }
        }

        #region Toolbar

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Button(new GUIContent(" Scan", EditorGUIUtility.IconContent("d_Refresh").image), 
                EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ScanAudioFiles();
            }

            GUILayout.FlexibleSpace();

            // 검색
            GUILayout.Label("Search:", GUILayout.Width(50));
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Tabs

        void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            
            DrawTabButton("🎵 BGM", Tab.BGM, bgmFiles.Count);
            DrawTabButton("🔊 SFX", Tab.SFX, sfxFiles.Count);
            DrawTabButton("🎤 Voice", Tab.Voice, voiceFiles.Count);
            DrawTabButton("⚙️ Settings", Tab.Settings, -1);
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(2);
        }

        void DrawTabButton(string label, Tab tab, int count)
        {
            string text = count >= 0 ? $"{label} ({count})" : label;
            bool isSelected = currentTab == tab;
            
            var style = isSelected ? 
                new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold } : 
                EditorStyles.toolbarButton;
            
            if (GUILayout.Button(text, style, GUILayout.Width(120)))
            {
                currentTab = tab;
            }
        }

        #endregion

        #region Main Area

        void DrawMainArea()
        {
            if (currentTab == Tab.Settings)
            {
                DrawSettingsPanel();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            
            // 좌측: 파일 목록
            DrawFileList();
            
            // 스플리터
            DrawSplitter();
            
            // 우측: 상세 정보 + 플레이어
            DrawDetailsPanel();
            
            EditorGUILayout.EndHorizontal();
        }

        void DrawFileList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(splitWidth));
            
            var files = GetCurrentFileList();
            
            leftScrollPos = EditorGUILayout.BeginScrollView(leftScrollPos, EditorStyles.helpBox);

            foreach (var file in files)
            {
                if (!MatchesFilter(file)) continue;
                DrawFileItem(file);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        List<AudioFileData> GetCurrentFileList()
        {
            return currentTab switch
            {
                Tab.BGM => bgmFiles,
                Tab.SFX => sfxFiles,
                Tab.Voice => voiceFiles,
                _ => new List<AudioFileData>()
            };
        }

        bool MatchesFilter(AudioFileData file)
        {
            if (string.IsNullOrEmpty(searchFilter)) return true;
            return file.name.ToLower().Contains(searchFilter.ToLower());
        }

        void DrawFileItem(AudioFileData file)
        {
            bool isCurrentPlaying = currentPlaying == file && isPlaying;
            
            EditorGUILayout.BeginHorizontal(isCurrentPlaying ? 
                new GUIStyle("SelectionRect") : GUIStyle.none);

            // 재생/정지 버튼
            string playIcon = isCurrentPlaying ? "⏹" : "▶";
            if (GUILayout.Button(playIcon, GUILayout.Width(25)))
            {
                if (isCurrentPlaying)
                    StopPreview();
                else
                    PlayPreview(file);
            }

            // 파일 이름
            EditorGUILayout.LabelField(file.name, GUILayout.MinWidth(150));

            // 길이
            if (file.isLoaded)
            {
                string duration = FormatTime(file.duration);
                EditorGUILayout.LabelField(duration, GUILayout.Width(50));
                
                // 프로그레스 바 (재생 중일 때)
                if (isCurrentPlaying)
                {
                    float progress = (float)((EditorApplication.timeSinceStartup - playStartTime) / file.duration);
                    progress = Mathf.Clamp01(progress);
                    var rect = EditorGUILayout.GetControlRect(GUILayout.Width(100));
                    EditorGUI.ProgressBar(rect, progress, "");
                }
                else
                {
                    GUILayout.Space(104);
                }
            }
            else
            {
                EditorGUILayout.LabelField("...", GUILayout.Width(50));
            }

            // 선택 버튼
            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                Selection.activeObject = file.clip;
                EditorGUIUtility.PingObject(file.clip);
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawSplitter()
        {
            var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(5));
            rect.height = position.height - 60;
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                isDraggingSplit = true;
                Event.current.Use();
            }
            
            if (isDraggingSplit)
            {
                splitWidth = Mathf.Clamp(Event.current.mousePosition.x, 250, position.width - 250);
                Repaint();
                
                if (Event.current.type == EventType.MouseUp)
                    isDraggingSplit = false;
            }
            
            GUI.Box(rect, "", EditorStyles.helpBox);
        }

        void DrawDetailsPanel()
        {
            EditorGUILayout.BeginVertical();
            rightScrollPos = EditorGUILayout.BeginScrollView(rightScrollPos, EditorStyles.helpBox);

            if (currentPlaying != null)
            {
                DrawPlayingDetails();
            }
            else
            {
                EditorGUILayout.HelpBox("왼쪽 목록에서 오디오 파일을 선택하여 재생하세요.", MessageType.Info);
            }

            // 캐릭터별 BGM 매핑 (BGM 탭일 때만)
            if (currentTab == Tab.BGM)
            {
                EditorGUILayout.Space(20);
                DrawCharacterBGMMapping();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawPlayingDetails()
        {
            EditorGUILayout.LabelField("Now Playing", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Name:", currentPlaying.name);
            EditorGUILayout.LabelField("Path:", currentPlaying.resourcePath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Duration:", FormatTime(currentPlaying.duration));
            
            if (currentPlaying.clip != null)
            {
                EditorGUILayout.LabelField("Channels:", currentPlaying.clip.channels.ToString());
                EditorGUILayout.LabelField("Frequency:", $"{currentPlaying.clip.frequency} Hz");
            }

            EditorGUILayout.Space(10);

            // 플레이어 컨트롤
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button(isPlaying ? "⏹ Stop" : "▶ Play", GUILayout.Height(30)))
            {
                if (isPlaying)
                    StopPreview();
                else
                    PlayPreview(currentPlaying);
            }

            // 루프 토글
            bool loop = previewSource != null && previewSource.loop;
            bool newLoop = GUILayout.Toggle(loop, "🔁 Loop", "Button", GUILayout.Width(70), GUILayout.Height(30));
            if (newLoop != loop && previewSource != null)
            {
                previewSource.loop = newLoop;
            }

            EditorGUILayout.EndHorizontal();

            // 프로그레스 바
            if (isPlaying && currentPlaying.duration > 0)
            {
                EditorGUILayout.Space(5);
                float progress = (float)((EditorApplication.timeSinceStartup - playStartTime) / currentPlaying.duration);
                if (previewSource != null && previewSource.loop)
                    progress = progress % 1f;
                else
                    progress = Mathf.Clamp01(progress);
                
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(20)), 
                    progress, FormatTime(progress * currentPlaying.duration) + " / " + FormatTime(currentPlaying.duration));
            }

            EditorGUILayout.Space(10);

            // CSV에서 사용할 코드
            EditorGUILayout.LabelField("CSV Usage:", EditorStyles.boldLabel);
            string csvCode = currentTab switch
            {
                Tab.BGM => $"Sound,,BGM:{currentPlaying.name},>",
                Tab.SFX => $"Sound,,SFX:{currentPlaying.name},>",
                Tab.Voice => $"Sound,,Voice:{currentPlaying.name},>",
                _ => ""
            };
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField(csvCode);
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
            {
                EditorGUIUtility.systemCopyBuffer = csvCode;
                Debug.Log($"Copied: {csvCode}");
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawCharacterBGMMapping()
        {
            EditorGUILayout.LabelField("Character BGM Mapping", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("캐릭터 등장 시 자동으로 재생될 BGM을 설정합니다.", MessageType.None);
            
            if (audioSettings == null)
            {
                EditorGUILayout.HelpBox("AudioSettings 에셋이 없습니다. 생성해주세요.", MessageType.Warning);
                if (GUILayout.Button("Create AudioSettings"))
                {
                    CreateAudioSettings();
                }
                return;
            }

            EditorGUI.BeginChangeCheck();

            // 기본 설정
            EditorGUILayout.Space(5);
            audioSettings.defaultBGM = (AudioClip)EditorGUILayout.ObjectField(
                "Default BGM", audioSettings.defaultBGM, typeof(AudioClip), false);
            audioSettings.bgmFadeDuration = EditorGUILayout.FloatField(
                "Fade Duration (sec)", audioSettings.bgmFadeDuration);
            audioSettings.autoSwitchOnCharacterEnter = EditorGUILayout.Toggle(
                "Auto Switch on Enter", audioSettings.autoSwitchOnCharacterEnter);
            audioSettings.useCrossfade = EditorGUILayout.Toggle(
                "Use Crossfade", audioSettings.useCrossfade);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Character → BGM", EditorStyles.boldLabel);

            // 캐릭터별 매핑
            for (int i = 0; i < audioSettings.characterBGMs.Count; i++)
            {
                var mapping = audioSettings.characterBGMs[i];
                
                EditorGUILayout.BeginHorizontal();
                
                mapping.characterName = EditorGUILayout.TextField(mapping.characterName, GUILayout.Width(80));
                mapping.bgmClip = (AudioClip)EditorGUILayout.ObjectField(
                    mapping.bgmClip, typeof(AudioClip), false, GUILayout.Width(200));
                
                // 미리듣기 버튼
                if (mapping.bgmClip != null)
                {
                    if (GUILayout.Button("▶", GUILayout.Width(25)))
                    {
                        var fileData = bgmFiles.Find(f => f.clip == mapping.bgmClip);
                        if (fileData != null)
                            PlayPreview(fileData);
                        else
                        {
                            // 직접 재생
                            StopPreview();
                            previewSource.clip = mapping.bgmClip;
                            previewSource.Play();
                            isPlaying = true;
                            playStartTime = EditorApplication.timeSinceStartup;
                        }
                    }
                }

                // 삭제 버튼
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    audioSettings.characterBGMs.RemoveAt(i);
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            // 추가 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Character"))
            {
                audioSettings.characterBGMs.Add(new LoveAlgoCharacterBGMMapping());
            }
            
            // 기본 캐릭터로 초기화
            if (GUILayout.Button("Reset to Default"))
            {
                audioSettings.characterBGMs.Clear();
                foreach (var name in defaultCharacters)
                {
                    audioSettings.characterBGMs.Add(new LoveAlgoCharacterBGMMapping { characterName = name });
                }
            }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(audioSettings);
            }
        }

        #endregion

        #region Settings Panel

        void DrawSettingsPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Audio Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // AudioSettings 에셋
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Settings Asset:", GUILayout.Width(100));
            var newSettings = (LoveAlgoAudioSettings)EditorGUILayout.ObjectField(
                audioSettings, typeof(LoveAlgoAudioSettings), false);
            if (newSettings != audioSettings)
            {
                audioSettings = newSettings;
                SaveSettings();
            }
            EditorGUILayout.EndHorizontal();

            if (audioSettings == null)
            {
                EditorGUILayout.Space(10);
                if (GUILayout.Button("Create New AudioSettings", GUILayout.Height(30)))
                {
                    CreateAudioSettings();
                }
            }
            else
            {
                EditorGUILayout.Space(10);
                
                // 인스펙터처럼 표시
                var editor = UnityEditor.Editor.CreateEditor(audioSettings);
                editor.OnInspectorGUI();
                DestroyImmediate(editor);
            }

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Audio Folders", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "오디오 파일은 다음 경로에 배치하세요:\n" +
                "• BGM: Resources/Audio/BGM/\n" +
                "• SFX: Resources/Audio/SFX/\n" +
                "• Voice: Resources/Audio/Voice/", 
                MessageType.Info);

            // 폴더 열기 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open BGM Folder"))
                OpenOrCreateFolder("Assets/Resources/Audio/BGM");
            if (GUILayout.Button("Open SFX Folder"))
                OpenOrCreateFolder("Assets/Resources/Audio/SFX");
            if (GUILayout.Button("Open Voice Folder"))
                OpenOrCreateFolder("Assets/Resources/Audio/Voice");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void OpenOrCreateFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                // 폴더 생성
                string[] parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
                AssetDatabase.Refresh();
            }
            
            // 폴더 선택
            var folder = AssetDatabase.LoadAssetAtPath<Object>(path);
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        #endregion

        #region Audio Preview

        void PlayPreview(AudioFileData file)
        {
            if (previewSource == null) CreatePreviewSource();
            
            // 클립 로드
            if (!file.isLoaded || file.clip == null)
            {
                file.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(file.path);
                if (file.clip != null)
                {
                    file.duration = file.clip.length;
                    file.isLoaded = true;
                }
            }

            if (file.clip == null)
            {
                Debug.LogWarning($"[AudioManager] 클립 로드 실패: {file.path}");
                return;
            }

            StopPreview();
            
            currentPlaying = file;
            previewSource.clip = file.clip;
            previewSource.Play();
            isPlaying = true;
            playStartTime = EditorApplication.timeSinceStartup;
        }

        void StopPreview()
        {
            if (previewSource != null)
            {
                previewSource.Stop();
            }
            isPlaying = false;
        }

        void CheckPlaybackEnd()
        {
            if (!isPlaying || previewSource == null) return;
            
            if (!previewSource.isPlaying && !previewSource.loop)
            {
                isPlaying = false;
            }
        }

        #endregion

        #region Data Management

        void ScanAudioFiles()
        {
            bgmFiles = ScanFolder("Assets/Resources/Audio/BGM", "Audio/BGM");
            sfxFiles = ScanFolder("Assets/Resources/Audio/SFX", "Audio/SFX");
            voiceFiles = ScanFolder("Assets/Resources/Audio/Voice", "Audio/Voice");
            
            Debug.Log($"[Audio Manager] Scanned: {bgmFiles.Count} BGM, {sfxFiles.Count} SFX, {voiceFiles.Count} Voice");
        }

        List<AudioFileData> ScanFolder(string folderPath, string resourceBase)
        {
            var result = new List<AudioFileData>();
            
            if (!AssetDatabase.IsValidFolder(folderPath))
                return result;

            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath });
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                
                // Resources 경로 계산
                string resourcePath = resourceBase + "/" + name;
                
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                
                result.Add(new AudioFileData
                {
                    name = name,
                    path = path,
                    resourcePath = resourcePath,
                    clip = clip,
                    duration = clip?.length ?? 0,
                    isLoaded = clip != null
                });
            }

            return result.OrderBy(f => f.name).ToList();
        }

        void LoadSettings()
        {
            string guid = EditorPrefs.GetString("LoveAlgo_AudioManager_SettingsGuid", "");
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                audioSettings = AssetDatabase.LoadAssetAtPath<LoveAlgoAudioSettings>(path);
            }

            // 없으면 찾아보기
            if (audioSettings == null)
            {
                var guids = AssetDatabase.FindAssets("t:AudioSettings");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    audioSettings = AssetDatabase.LoadAssetAtPath<LoveAlgoAudioSettings>(path);
                    SaveSettings();
                }
            }
        }

        void SaveSettings()
        {
            if (audioSettings != null)
            {
                string path = AssetDatabase.GetAssetPath(audioSettings);
                string guid = AssetDatabase.AssetPathToGUID(path);
                EditorPrefs.SetString("LoveAlgo_AudioManager_SettingsGuid", guid);
            }
        }

        void CreateAudioSettings()
        {
            audioSettings = CreateInstance<LoveAlgoAudioSettings>();
            
            // 기본 캐릭터 추가
            foreach (var name in defaultCharacters)
            {
                audioSettings.characterBGMs.Add(new LoveAlgoCharacterBGMMapping { characterName = name });
            }
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Audio Settings", "AudioSettings", "asset", "Save audio settings");
            
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(audioSettings, path);
                AssetDatabase.SaveAssets();
                SaveSettings();
            }
        }

        #endregion

        #region Utility

        string FormatTime(float seconds)
        {
            int min = (int)(seconds / 60);
            int sec = (int)(seconds % 60);
            return $"{min}:{sec:D2}";
        }

        #endregion
    }
}
