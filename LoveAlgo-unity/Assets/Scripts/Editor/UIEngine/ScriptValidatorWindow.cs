using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using LoveAlgo;
using LoveAlgo.Story;

namespace LoveAlgo.Editor.UIEngine
{
    /// <summary>
    /// 스토리 스크립트 유효성 검사 에디터 도구.
    /// - 누락된 LineID (Jump/Flow 대상)
    /// - 존재하지 않는 캐릭터
    /// - 닫히지 않은 Choice
    /// - 유효하지 않은 문법
    /// </summary>
    public class ScriptValidatorWindow : EditorWindow
    {
        [MenuItem("LoveAlgo/Tools/Script Validator", false, 102)]
        static void ShowWindow()
        {
            var window = GetWindow<ScriptValidatorWindow>("Script Validator");
            window.minSize = new Vector2(600, 450);
        }

        // 검증 대상 스크립트
        List<TextAsset> scriptsToValidate = new();
        Vector2 scriptsScrollPos;
        Vector2 resultsScrollPos;
        
        // 검증 결과
        List<ValidationResult> results = new();
        bool hasValidated;
        
        // 검색/필터
        string filterText = "";
        ErrorLevel filterLevel = ErrorLevel.All;
        
        // 알려진 캐릭터 목록 (GameConstants 기반 + 한글명)
        static readonly HashSet<string> KnownCharacters = new HashSet<string>(
            GameConstants.HeroineIds.Concat(GameConstants.HeroineNames)
        );
        
        // 알려진 표정 목록 (GameConstants 기반)
        static readonly HashSet<string> KnownEmotes = new HashSet<string>(
            GameConstants.DefaultEmotes
        );

        enum ErrorLevel
        {
            All,
            Error,
            Warning,
            Info
        }

        class ValidationResult
        {
            public TextAsset Script;
            public int LineNumber;
            public string LineId;
            public ErrorLevel Level;
            public string Category;
            public string Message;
        }

        void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            
            DrawToolbar();
            
            EditorGUILayout.BeginHorizontal();
            
            // 좌측: 스크립트 목록
            DrawScriptList();
            
            // 우측: 검증 결과
            DrawResults();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Button("모든 Story/*.csv 추가", EditorStyles.toolbarButton, GUILayout.Width(140)))
            {
                AddAllStoryScripts();
            }
            
            if (GUILayout.Button("선택 항목 추가", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                AddSelectedScripts();
            }
            
            if (GUILayout.Button("목록 비우기", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                scriptsToValidate.Clear();
                results.Clear();
                hasValidated = false;
            }
            
            GUILayout.FlexibleSpace();
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("▶ 검증 실행", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                RunValidation();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();
        }

        void DrawScriptList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            
            EditorGUILayout.LabelField("검증 대상 스크립트", EditorStyles.boldLabel);
            
            scriptsScrollPos = EditorGUILayout.BeginScrollView(scriptsScrollPos, GUILayout.ExpandHeight(true));
            
            for (int i = scriptsToValidate.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(scriptsToValidate[i], typeof(TextAsset), false);
                EditorGUI.EndDisabledGroup();
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    scriptsToValidate.RemoveAt(i);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            // 드래그앤드롭 영역
            var dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "여기에 CSV 파일 드롭", EditorStyles.helpBox);
            HandleDragAndDrop(dropArea);
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.LabelField($"총 {scriptsToValidate.Count}개", EditorStyles.miniLabel);
            
            EditorGUILayout.EndVertical();
        }

        void HandleDragAndDrop(Rect dropArea)
        {
            Event evt = Event.current;
            
            if (dropArea.Contains(evt.mousePosition))
            {
                switch (evt.type)
                {
                    case EventType.DragUpdated:
                        if (DragAndDrop.objectReferences.Any(o => o is TextAsset))
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            evt.Use();
                        }
                        break;
                        
                    case EventType.DragPerform:
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            if (obj is TextAsset ta && !scriptsToValidate.Contains(ta))
                            {
                                scriptsToValidate.Add(ta);
                            }
                        }
                        evt.Use();
                        break;
                }
            }
        }

        void DrawResults()
        {
            EditorGUILayout.BeginVertical();
            
            // 필터 바
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("검증 결과", EditorStyles.boldLabel, GUILayout.Width(70));
            
            filterLevel = (ErrorLevel)EditorGUILayout.EnumPopup(filterLevel, GUILayout.Width(80));
            filterText = EditorGUILayout.TextField(filterText, EditorStyles.toolbarSearchField);
            
            if (hasValidated)
            {
                var errorCount = results.Count(r => r.Level == ErrorLevel.Error);
                var warnCount = results.Count(r => r.Level == ErrorLevel.Warning);
                var infoCount = results.Count(r => r.Level == ErrorLevel.Info);
                
                GUILayout.Label($"오류:{errorCount} 경고:{warnCount} 정보:{infoCount}", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 결과 목록
            resultsScrollPos = EditorGUILayout.BeginScrollView(resultsScrollPos);
            
            if (!hasValidated)
            {
                EditorGUILayout.HelpBox("스크립트를 추가하고 '검증 실행' 버튼을 눌러주세요.", MessageType.Info);
            }
            else if (results.Count == 0)
            {
                EditorGUILayout.HelpBox("✓ 모든 스크립트가 검증을 통과했습니다!", MessageType.Info);
            }
            else
            {
                var filtered = FilterResults();
                foreach (var result in filtered)
                {
                    DrawResultItem(result);
                }
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
        }

        IEnumerable<ValidationResult> FilterResults()
        {
            var filtered = results.AsEnumerable();
            
            if (filterLevel != ErrorLevel.All)
            {
                filtered = filtered.Where(r => r.Level == filterLevel);
            }
            
            if (!string.IsNullOrEmpty(filterText))
            {
                filtered = filtered.Where(r => 
                    r.Message.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                    (r.LineId?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            
            return filtered;
        }

        void DrawResultItem(ValidationResult result)
        {
            Color bgColor = result.Level switch
            {
                ErrorLevel.Error => new Color(0.8f, 0.3f, 0.3f, 0.3f),
                ErrorLevel.Warning => new Color(0.8f, 0.7f, 0.2f, 0.3f),
                _ => new Color(0.4f, 0.6f, 0.8f, 0.3f)
            };
            
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.backgroundColor = bgColor;
            
            // 아이콘
            string icon = result.Level switch
            {
                ErrorLevel.Error => "❌",
                ErrorLevel.Warning => "⚠",
                _ => "ℹ"
            };
            GUILayout.Label(icon, GUILayout.Width(20));
            
            EditorGUILayout.BeginVertical();
            
            // 파일명 + 라인 (클릭 시 에디터에서 열기)
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"{result.Script.name}:{result.LineNumber}", EditorStyles.linkLabel))
            {
                AssetDatabase.OpenAsset(result.Script, result.LineNumber);
            }
            
            if (!string.IsNullOrEmpty(result.LineId))
            {
                GUILayout.Label($"[{result.LineId}]", EditorStyles.miniLabel);
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.Label($"[{result.Category}]", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            // 메시지
            EditorGUILayout.LabelField(result.Message, EditorStyles.wordWrappedMiniLabel);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }

        void AddAllStoryScripts()
        {
            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Resources/Story" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (asset != null && !scriptsToValidate.Contains(asset))
                    {
                        scriptsToValidate.Add(asset);
                    }
                }
            }
        }

        void AddSelectedScripts()
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is TextAsset ta && !scriptsToValidate.Contains(ta))
                {
                    scriptsToValidate.Add(ta);
                }
            }
        }

        void RunValidation()
        {
            results.Clear();
            hasValidated = true;
            
            foreach (var script in scriptsToValidate)
            {
                ValidateScript(script);
            }
            
            // 중요도 순 정렬
            results = results.OrderBy(r => r.Level).ThenBy(r => r.Script.name).ThenBy(r => r.LineNumber).ToList();
            
            Debug.Log($"[ScriptValidator] 검증 완료: {scriptsToValidate.Count}개 스크립트, {results.Count}개 문제 발견");
        }

        void ValidateScript(TextAsset script)
        {
            if (script == null) return;
            
            var lines = ScriptParser.Parse(script);
            var lineIndex = ScriptParser.BuildLineIndex(lines);
            
            // 1. 라인별 기본 검증
            bool inChoice = false;
            int choiceStartLine = 0;
            
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                int lineNumber = i + 2; // CSV는 2행부터 (1행은 헤더)
                
                // Choice 블록 추적
                if (line.Type == LineType.Choice)
                {
                    if (inChoice)
                    {
                        AddResult(script, choiceStartLine, null, ErrorLevel.Error, "Choice",
                            "이전 Choice가 닫히지 않은 채 새 Choice 시작");
                    }
                    inChoice = true;
                    choiceStartLine = lineNumber;
                }
                else if (line.Type == LineType.Option)
                {
                    if (!inChoice)
                    {
                        AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Option",
                            "Choice 블록 없이 Option 라인 사용");
                    }
                }
                else if (inChoice && line.Type != LineType.Option)
                {
                    // Choice 후 Option이 아닌 라인이 오면 Choice 종료
                    inChoice = false;
                }
                
                // 타입별 검증
                ValidateLine(script, line, lineNumber, lineIndex);
            }
            
            // 마지막 Choice가 닫히지 않았는지 확인
            if (inChoice)
            {
                AddResult(script, choiceStartLine, null, ErrorLevel.Warning, "Choice",
                    "Choice 블록이 명시적으로 종료되지 않음");
            }
        }

        void ValidateLine(TextAsset script, ScriptLine line, int lineNumber, Dictionary<string, int> lineIndex)
        {
            switch (line.Type)
            {
                case LineType.Text:
                    ValidateText(script, line, lineNumber);
                    break;
                    
                case LineType.Char:
                    ValidateChar(script, line, lineNumber);
                    break;
                    
                case LineType.BG:
                    ValidateBG(script, line, lineNumber);
                    break;
                    
                case LineType.Sound:
                    ValidateSound(script, line, lineNumber);
                    break;
                    
                case LineType.Flow:
                    ValidateFlow(script, line, lineNumber, lineIndex);
                    break;
                    
                case LineType.Option:
                    ValidateOption(script, line, lineNumber, lineIndex);
                    break;
            }
        }

        void ValidateText(TextAsset script, ScriptLine line, int lineNumber)
        {
            if (string.IsNullOrEmpty(line.Value))
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Warning, "Text",
                    "빈 대사 텍스트");
            }
            
            // 인라인 태그 검증
            var tagPattern = @"<(\w+)(?:=([^/>]+))?(?:/>|>)";
            var matches = Regex.Matches(line.Value ?? "", tagPattern);
            foreach (Match m in matches)
            {
                string tagName = m.Groups[1].Value.ToLower();
                if (!IsValidInlineTag(tagName))
                {
                    AddResult(script, lineNumber, line.LineID, ErrorLevel.Warning, "Tag",
                        $"알 수 없는 인라인 태그: <{tagName}>");
                }
            }
            
            // 변수 치환 검증
            var varPattern = @"\{\{(\w+)\}\}";
            matches = Regex.Matches(line.Value ?? "", varPattern);
            foreach (Match m in matches)
            {
                string varName = m.Groups[1].Value;
                if (!IsValidVariable(varName))
                {
                    AddResult(script, lineNumber, line.LineID, ErrorLevel.Info, "Variable",
                        $"사용자 정의 변수: {{{{{varName}}}}}");
                }
            }
        }

        void ValidateChar(TextAsset script, ScriptLine line, int lineNumber)
        {
            if (string.IsNullOrEmpty(line.Value))
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Char",
                    "빈 Char 명령");
                return;
            }
            
            // 형식: 슬롯:액션:대상[:표정]
            var parts = line.Value.Split(':');
            if (parts.Length < 2)
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Char",
                    $"잘못된 Char 형식: {line.Value}");
                return;
            }
            
            string slot = parts[0].ToUpper();
            string action = parts[1];
            
            // 슬롯 검증
            if (slot != "L" && slot != "C" && slot != "R")
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Char",
                    $"알 수 없는 슬롯: {slot} (L/C/R만 가능)");
            }
            
            // 액션 검증
            if (action != "Enter" && action != "Exit" && action != "Emote")
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Char",
                    $"알 수 없는 액션: {action} (Enter/Exit/Emote만 가능)");
            }
            
            // Enter 시 캐릭터명 필수
            if (action == "Enter" && parts.Length < 3)
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Char",
                    "Enter에는 캐릭터 이름이 필요합니다");
            }
            
            // 캐릭터명 검증
            if (parts.Length >= 3 && action == "Enter")
            {
                string charName = parts[2];
                if (!KnownCharacters.Contains(charName))
                {
                    AddResult(script, lineNumber, line.LineID, ErrorLevel.Warning, "Char",
                        $"알 수 없는 캐릭터: {charName}");
                }
            }
            
            // 표정 검증
            if (parts.Length >= 4 && action == "Enter")
            {
                string emote = parts[3];
                if (!KnownEmotes.Contains(emote))
                {
                    AddResult(script, lineNumber, line.LineID, ErrorLevel.Info, "Char",
                        $"알 수 없는 표정: {emote}");
                }
            }
            
            if (action == "Emote" && parts.Length >= 3)
            {
                string emote = parts[2];
                if (!KnownEmotes.Contains(emote))
                {
                    AddResult(script, lineNumber, line.LineID, ErrorLevel.Info, "Char",
                        $"알 수 없는 표정: {emote}");
                }
            }
        }

        void ValidateBG(TextAsset script, ScriptLine line, int lineNumber)
        {
            if (string.IsNullOrEmpty(line.Value))
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "BG",
                    "빈 BG 명령");
                return;
            }
            
            // 형식: 배경이름[:전환타입:시간]
            var parts = line.Value.Split(':');
            string bgName = parts[0];
            
            // 배경 리소스 존재 여부 (간단 검사)
            var bgResource = Resources.Load<Sprite>($"Backgrounds/{bgName}");
            if (bgResource == null)
            {
                // 직접 에셋 확인
                var guids = AssetDatabase.FindAssets($"{bgName} t:Sprite", new[] { "Assets/Resources/Backgrounds" });
                if (guids.Length == 0)
                {
                    AddResult(script, lineNumber, line.LineID, ErrorLevel.Warning, "BG",
                        $"배경 리소스를 찾을 수 없음: {bgName}");
                }
            }
            
            // 전환 타입 검증
            if (parts.Length >= 2)
            {
                string transition = parts[1];
                var validTransitions = new[] { "Cut", "Fade", "Cross", "Slide" };
                if (!validTransitions.Contains(transition))
                {
                    AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "BG",
                        $"알 수 없는 전환 타입: {transition}");
                }
            }
        }

        void ValidateSound(TextAsset script, ScriptLine line, int lineNumber)
        {
            if (string.IsNullOrEmpty(line.Value))
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Sound",
                    "빈 Sound 명령");
                return;
            }
            
            // 형식: 카테고리:이름[:옵션]
            var parts = line.Value.Split(':');
            if (parts.Length < 2 && parts[0] != "Stop")
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Sound",
                    $"잘못된 Sound 형식: {line.Value}");
                return;
            }
            
            string category = parts[0].ToUpper();
            var validCategories = new[] { "BGM", "SFX", "VOICE", "STOP" };
            if (!validCategories.Contains(category))
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Sound",
                    $"알 수 없는 오디오 카테고리: {category}");
            }
        }

        void ValidateFlow(TextAsset script, ScriptLine line, int lineNumber, Dictionary<string, int> lineIndex)
        {
            if (string.IsNullOrEmpty(line.Value))
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Flow",
                    "빈 Flow 명령");
                return;
            }
            
            // 형식: 명령:인자
            var parts = line.Value.Split(':');
            string command = parts[0];
            
            switch (command)
            {
                case "Jump":
                    if (parts.Length < 2)
                    {
                        AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Flow",
                            "Jump에 대상 LineID가 필요합니다");
                    }
                    else if (!lineIndex.ContainsKey(parts[1]))
                    {
                        AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Flow",
                            $"점프 대상 LineID를 찾을 수 없음: {parts[1]}");
                    }
                    break;
                    
                case "If":
                    // 형식: If:조건:점프대상
                    if (parts.Length < 3)
                    {
                        AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Flow",
                            "If 문법 오류: If:조건:점프대상 형식이어야 합니다");
                    }
                    else
                    {
                        string target = parts[2];
                        if (!lineIndex.ContainsKey(target))
                        {
                            AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Flow",
                                $"If 점프 대상 LineID를 찾을 수 없음: {target}");
                        }
                        
                        ValidateCondition(script, lineNumber, line.LineID, parts[1]);
                    }
                    break;
                    
                case "End":
                case "Save":
                    // 추가 인자 불필요
                    break;
                    
                default:
                    AddResult(script, lineNumber, line.LineID, ErrorLevel.Warning, "Flow",
                        $"알 수 없는 Flow 명령: {command}");
                    break;
            }
        }

        void ValidateOption(TextAsset script, ScriptLine line, int lineNumber, Dictionary<string, int> lineIndex)
        {
            if (string.IsNullOrEmpty(line.Value))
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Option",
                    "빈 Option 값");
                return;
            }
            
            // 형식: 버튼텍스트|점프대상|효과...|조건
            var parts = line.Value.Split('|');
            
            if (parts.Length < 2)
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Option",
                    "Option에 버튼텍스트와 점프대상이 필요합니다");
                return;
            }
            
            string buttonText = parts[0];
            string jumpTarget = parts[1];
            
            if (string.IsNullOrWhiteSpace(buttonText))
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Warning, "Option",
                    "빈 선택지 텍스트");
            }
            
            if (!lineIndex.ContainsKey(jumpTarget))
            {
                AddResult(script, lineNumber, line.LineID, ErrorLevel.Error, "Option",
                    $"선택지 점프 대상 LineID를 찾을 수 없음: {jumpTarget}");
            }
            
            // 효과 및 조건 검증
            for (int i = 2; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.StartsWith("if:", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateCondition(script, lineNumber, line.LineID, part.Substring(3));
                }
                else
                {
                    ValidateEffect(script, lineNumber, line.LineID, part);
                }
            }
        }

        void ValidateCondition(TextAsset script, int lineNumber, string lineId, string condition)
        {
            // 간단한 조건 형식 검증
            // Love:캐릭터>=값, Stat:스탯>=값, Flag:이름, !Flag:이름
            
            if (condition.StartsWith("Love:") || condition.StartsWith("Stat:"))
            {
                if (!Regex.IsMatch(condition, @"^(Love|Stat):\w+[<>=]+\d+$"))
                {
                    AddResult(script, lineNumber, lineId, ErrorLevel.Warning, "Condition",
                        $"조건 형식이 올바르지 않을 수 있음: {condition}");
                }
            }
            else if (condition.StartsWith("Flag:") || condition.StartsWith("!Flag:"))
            {
                // 형식 OK
            }
            else
            {
                AddResult(script, lineNumber, lineId, ErrorLevel.Warning, "Condition",
                    $"알 수 없는 조건 형식: {condition}");
            }
        }

        void ValidateEffect(TextAsset script, int lineNumber, string lineId, string effect)
        {
            if (string.IsNullOrWhiteSpace(effect)) return;
            
            // Love:캐릭터:값, Stat:스탯:값, Flag:이름:값, Money:값, SFX:이름
            var parts = effect.Split(':');
            
            if (parts.Length < 2)
            {
                AddResult(script, lineNumber, lineId, ErrorLevel.Warning, "Effect",
                    $"효과 형식이 올바르지 않음: {effect}");
                return;
            }
            
            string type = parts[0];
            var validTypes = new[] { "Love", "Stat", "Flag", "Money", "SFX" };
            
            if (!validTypes.Contains(type))
            {
                AddResult(script, lineNumber, lineId, ErrorLevel.Warning, "Effect",
                    $"알 수 없는 효과 타입: {type}");
            }
        }

        bool IsValidInlineTag(string tagName)
        {
            var validTags = new[] { "wait", "sfx", "emote", "speed", "size", "color", "b", "i", "u", "br" };
            return validTags.Contains(tagName);
        }

        bool IsValidVariable(string varName)
        {
            var knownVars = new[] { "PlayerName", "Day", "Money" };
            return knownVars.Contains(varName);
        }

        void AddResult(TextAsset script, int lineNumber, string lineId, ErrorLevel level, string category, string message)
        {
            results.Add(new ValidationResult
            {
                Script = script,
                LineNumber = lineNumber,
                LineId = lineId,
                Level = level,
                Category = category,
                Message = message
            });
        }
    }
}
