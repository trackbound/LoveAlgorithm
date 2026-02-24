using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

// 런타임 타입 참조
using LineType = LoveAlgo.Story.LineType;
using NextType = LoveAlgo.Story.NextType;
using CsvUtility = LoveAlgo.Story.CsvUtility;

namespace LoveAlgo.Editor.UIEngine
{
    /// <summary>
    /// Flowchart Visualizer - 스토리 분기 시각화 툴
    /// </summary>
    public class FlowchartVisualizerWindow : EditorWindow
    {
        [MenuItem("LoveAlgo/Flowchart Visualizer %#f", priority = 105)]
        static void OpenWindow()
        {
            var window = GetWindow<FlowchartVisualizerWindow>();
            window.titleContent = new GUIContent("Flowchart", EditorGUIUtility.IconContent("d_UnityEditor.Graphs.AnimatorControllerTool").image);
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        // 데이터
        TextAsset currentScript;
        List<FlowNode> nodes = new();
        List<FlowConnection> connections = new();

        // 캔버스 상태
        Vector2 scrollOffset;
        float zoom = 1f;
        bool isDraggingCanvas;
        Vector2 lastMousePos;

        // 노드 드래그
        FlowNode selectedNode;
        FlowNode draggedNode;
        Vector2 dragOffset;

        // 스타일
        GUIStyle nodeStyle;
        GUIStyle selectedNodeStyle;
        GUIStyle anchorStyle;
        GUIStyle choiceStyle;
        GUIStyle endStyle;
        bool stylesInitialized;

        // 노드 타입별 색상
        static readonly Color AnchorColor = new(0.3f, 0.5f, 0.8f);
        static readonly Color ChoiceColor = new(0.8f, 0.6f, 0.2f);
        static readonly Color EndColor = new(0.6f, 0.3f, 0.3f);
        static readonly Color OptionColor = new(0.6f, 0.7f, 0.3f);
        static readonly Color DefaultColor = new(0.4f, 0.4f, 0.4f);

        // 레이아웃 설정
        const float NODE_WIDTH = 200f;
        const float NODE_HEIGHT = 70f;
        const float NODE_SPACING_X = 260f;
        const float NODE_SPACING_Y = 100f;

        void OnEnable()
        {
            LoadLastScript();
        }

        void InitStyles()
        {
            if (stylesInitialized) return;

            nodeStyle = new GUIStyle("flow node 0")
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                wordWrap = true,
                padding = new RectOffset(8, 8, 8, 8)
            };

            selectedNodeStyle = new GUIStyle("flow node 0 on")
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                wordWrap = true,
                padding = new RectOffset(8, 8, 8, 8)
            };

            anchorStyle = new GUIStyle("flow node 1")
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                wordWrap = true,
                padding = new RectOffset(8, 8, 8, 8)
            };

            choiceStyle = new GUIStyle("flow node 3")
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                wordWrap = true,
                padding = new RectOffset(8, 8, 8, 8)
            };

            endStyle = new GUIStyle("flow node 6")
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                wordWrap = true,
                padding = new RectOffset(8, 8, 8, 8)
            };

            stylesInitialized = true;
        }

        void OnGUI()
        {
            InitStyles();
            DrawToolbar();
            DrawCanvas();
            DrawMinimap();
            ProcessEvents();
        }

        #region Toolbar

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 파일 선택
            EditorGUILayout.LabelField("Script:", GUILayout.Width(45));
            var newScript = (TextAsset)EditorGUILayout.ObjectField(
                currentScript, typeof(TextAsset), false, GUILayout.Width(200));
            
            if (newScript != currentScript)
            {
                currentScript = newScript;
                if (currentScript != null)
                {
                    ParseScript();
                    AutoLayout();
                    SaveLastScript();
                }
            }

            if (GUILayout.Button("📂 Open", EditorStyles.toolbarButton, GUILayout.Width(55)))
            {
                string path = EditorUtility.OpenFilePanel("Open Story Script", 
                    "Assets/Resources/Story", "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    string relativePath = path.Replace(Application.dataPath, "Assets");
                    currentScript = AssetDatabase.LoadAssetAtPath<TextAsset>(relativePath);
                    if (currentScript != null)
                    {
                        ParseScript();
                        AutoLayout();
                        SaveLastScript();
                    }
                }
            }

            GUILayout.Space(20);

            if (GUILayout.Button("🔄 Refresh", EditorStyles.toolbarButton, GUILayout.Width(65)))
            {
                if (currentScript != null)
                {
                    ParseScript();
                    AutoLayout();
                }
            }

            if (GUILayout.Button("📐 Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(85)))
            {
                AutoLayout();
            }

            if (GUILayout.Button("🔍 Fit All", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                FitAllNodes();
            }

            GUILayout.FlexibleSpace();

            // 줌
            EditorGUILayout.LabelField("Zoom:", GUILayout.Width(40));
            zoom = EditorGUILayout.Slider(zoom, 0.3f, 2f, GUILayout.Width(100));

            if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                zoom = 1f;
                scrollOffset = Vector2.zero;
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Canvas

        // 캔버스 영역
        Rect canvasRect;

        /// <summary>
        /// 월드 좌표를 화면 좌표로 변환
        /// </summary>
        Rect WorldToScreen(Rect worldRect)
        {
            return new Rect(
                worldRect.x * zoom + scrollOffset.x,
                worldRect.y * zoom + scrollOffset.y + 20, // 툴바 오프셋
                worldRect.width * zoom,
                worldRect.height * zoom
            );
        }

        /// <summary>
        /// 화면 좌표를 월드 좌표로 변환
        /// </summary>
        Vector2 ScreenToWorld(Vector2 screenPos)
        {
            return new Vector2(
                (screenPos.x - scrollOffset.x) / zoom,
                (screenPos.y - 20 - scrollOffset.y) / zoom
            );
        }

        void DrawCanvas()
        {
            canvasRect = new Rect(0, 20, position.width, position.height - 20);
            
            // 배경
            EditorGUI.DrawRect(canvasRect, new Color(0.15f, 0.15f, 0.15f));
            DrawGrid(canvasRect, 20 * zoom, new Color(0.2f, 0.2f, 0.2f));
            DrawGrid(canvasRect, 100 * zoom, new Color(0.25f, 0.25f, 0.25f));

            // 클리핑 영역 설정 (캔버스 밖으로 나가지 않도록)
            GUI.BeginClip(canvasRect);

            // 연결선 그리기 (화면 좌표로 변환)
            DrawConnections();

            // 노드 그리기 (화면 좌표로 변환)
            DrawNodes();

            GUI.EndClip();
        }

        void DrawGrid(Rect rect, float spacing, Color color)
        {
            int widthDivs = Mathf.CeilToInt(rect.width / spacing);
            int heightDivs = Mathf.CeilToInt(rect.height / spacing);

            Handles.BeginGUI();
            Handles.color = color;

            Vector3 offset = new(scrollOffset.x % spacing, scrollOffset.y % spacing, 0);

            for (int i = 0; i <= widthDivs; i++)
            {
                Handles.DrawLine(
                    new Vector3(spacing * i + offset.x, 0, 0),
                    new Vector3(spacing * i + offset.x, rect.height, 0));
            }

            for (int i = 0; i <= heightDivs; i++)
            {
                Handles.DrawLine(
                    new Vector3(0, spacing * i + offset.y, 0),
                    new Vector3(rect.width, spacing * i + offset.y, 0));
            }

            Handles.EndGUI();
        }

        void DrawConnections()
        {
            foreach (var conn in connections)
            {
                var fromNode = nodes.FirstOrDefault(n => n.Id == conn.FromId);
                var toNode = nodes.FirstOrDefault(n => n.Id == conn.ToId);

                if (fromNode == null || toNode == null) continue;

                // 월드 좌표를 화면 좌표로 변환
                Rect fromScreen = WorldToScreen(fromNode.Rect);
                Rect toScreen = WorldToScreen(toNode.Rect);

                // 캔버스 로컬 좌표로 (툴바 오프셋 제거)
                Vector2 start = new Vector2(fromScreen.center.x, fromScreen.center.y - 20);
                Vector2 end = new Vector2(toScreen.center.x, toScreen.center.y - 20);

                // 화면 밖이면 스킵 (최적화)
                Rect screenBounds = new Rect(-100, -100, canvasRect.width + 200, canvasRect.height + 200);
                if (!screenBounds.Contains(start) && !screenBounds.Contains(end)) continue;

                // 베지어 곡선 탄젠트 (줌 적용)
                float tangentLength = 50 * zoom;
                Vector2 startTan = start + Vector2.right * tangentLength;
                Vector2 endTan = end + Vector2.left * tangentLength;

                // 연결 타입별 색상
                Color lineColor = conn.IsChoice ? new Color(0.9f, 0.7f, 0.2f) : new Color(0.5f, 0.7f, 0.9f);
                
                Handles.BeginGUI();
                Handles.DrawBezier(start, end, startTan, endTan, lineColor, null, 2f);

                // 화살표
                Vector2 arrowDir = (end - endTan).normalized;
                float arrowSize = 8 * zoom;
                Vector2 arrowPos = end - arrowDir * (arrowSize + 5);
                Vector2 arrowLeft = arrowPos + (Vector2)(Quaternion.Euler(0, 0, 150) * (Vector3)(arrowDir * arrowSize));
                Vector2 arrowRight = arrowPos + (Vector2)(Quaternion.Euler(0, 0, -150) * (Vector3)(arrowDir * arrowSize));
                
                Handles.color = lineColor;
                Handles.DrawAAConvexPolygon(end, arrowLeft, arrowRight);
                Handles.EndGUI();

                // 조건 라벨
                if (!string.IsNullOrEmpty(conn.Label))
                {
                    Vector2 midPoint = (start + end) / 2;
                    Rect labelRect = new(midPoint.x - 40, midPoint.y - 10, 80, 20);
                    GUI.Label(labelRect, conn.Label, EditorStyles.miniLabel);
                }
            }
        }

        void DrawNodes()
        {
            foreach (var node in nodes)
            {
                // 화면 좌표로 변환
                Rect screenRect = WorldToScreen(node.Rect);
                // 캔버스 로컬 좌표로 (툴바 오프셋 제거)
                Rect localRect = new Rect(screenRect.x, screenRect.y - 20, screenRect.width, screenRect.height);

                // 화면 밖이면 스킵 (최적화)
                if (localRect.xMax < 0 || localRect.x > canvasRect.width ||
                    localRect.yMax < 0 || localRect.y > canvasRect.height)
                    continue;

                DrawNode(node, localRect);
            }
        }

        void DrawNode(FlowNode node, Rect screenRect)
        {
            GUIStyle style = GetNodeStyle(node);
            
            // 선택 표시
            if (node == selectedNode)
            {
                Rect highlightRect = new(screenRect.x - 3, screenRect.y - 3, 
                    screenRect.width + 6, screenRect.height + 6);
                EditorGUI.DrawRect(highlightRect, new Color(1f, 0.8f, 0.2f, 0.5f));
            }

            // 배경색
            Color bgColor = GetNodeColor(node);
            EditorGUI.DrawRect(screenRect, bgColor);

            // 노드 박스
            GUI.Box(screenRect, "", style);

            // 툴팁 (마우스 오버 시 전체 내용 표시)
            string tooltip = $"{node.DisplayName}";
            if (!string.IsNullOrEmpty(node.SubText)) tooltip += $"\n{node.SubText}";
            if (!string.IsNullOrEmpty(node.FullText)) tooltip += $"\n───\n{node.FullText}";
            GUI.Label(screenRect, new GUIContent("", tooltip));

            // 줌이 너무 작으면 내용 생략
            if (zoom < 0.4f) return;

            // 내용
            float padding = 4 * zoom;
            Rect contentRect = new Rect(screenRect.x + padding, screenRect.y + padding, 
                screenRect.width - padding * 2, screenRect.height - padding * 2);
            
            GUILayout.BeginArea(contentRect);
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            // 타입 아이콘 + 이름
            string icon = GetNodeIcon(node);
            string displayText = node.DisplayName;
            int maxChars = Mathf.Max(8, (int)(22 * zoom));
            if (displayText.Length > maxChars) displayText = displayText.Substring(0, maxChars - 2) + "..";
            
            int fontSize = Mathf.Max(8, (int)(11 * zoom));
            GUILayout.Label($"{icon} {displayText}", new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            });

            // 부가 정보 (줌 0.6 이상일 때만)
            if (!string.IsNullOrEmpty(node.SubText) && zoom >= 0.6f)
            {
                string subText = node.SubText;
                int subMaxChars = Mathf.Max(10, (int)(28 * zoom));
                if (subText.Length > subMaxChars) subText = subText.Substring(0, subMaxChars - 2) + "..";
                
                GUILayout.Label(subText, new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(7, (int)(9 * zoom)),
                    wordWrap = true,
                    clipping = TextClipping.Clip,
                    normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
                });
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        GUIStyle GetNodeStyle(FlowNode node)
        {
            if (node == selectedNode) return selectedNodeStyle;
            
            return node.Type switch
            {
                FlowNodeType.Anchor => anchorStyle,
                FlowNodeType.Choice => choiceStyle,
                FlowNodeType.End => endStyle,
                _ => nodeStyle
            };
        }

        Color GetNodeColor(FlowNode node)
        {
            return node.Type switch
            {
                FlowNodeType.Anchor => AnchorColor,
                FlowNodeType.Choice => ChoiceColor,
                FlowNodeType.Option => OptionColor,
                FlowNodeType.End => EndColor,
                _ => DefaultColor
            };
        }

        string GetNodeIcon(FlowNode node)
        {
            return node.Type switch
            {
                FlowNodeType.Anchor => "📍",
                FlowNodeType.Choice => "❓",
                FlowNodeType.Option => "→",
                FlowNodeType.End => "🏁",
                FlowNodeType.Start => "▶",
                _ => "○"
            };
        }

        #endregion

        #region Minimap

        void DrawMinimap()
        {
            if (nodes.Count == 0) return;

            float mapWidth = 150;
            float mapHeight = 100;
            Rect mapRect = new(position.width - mapWidth - 10, position.height - mapHeight - 10, 
                mapWidth, mapHeight);

            // 배경
            EditorGUI.DrawRect(mapRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));

            // 경계 계산
            float minX = nodes.Min(n => n.Rect.x);
            float maxX = nodes.Max(n => n.Rect.xMax);
            float minY = nodes.Min(n => n.Rect.y);
            float maxY = nodes.Max(n => n.Rect.yMax);
            float contentWidth = maxX - minX + 100;
            float contentHeight = maxY - minY + 100;

            float scaleX = (mapWidth - 10) / contentWidth;
            float scaleY = (mapHeight - 10) / contentHeight;
            float scale = Mathf.Min(scaleX, scaleY);

            // 노드들 그리기
            foreach (var node in nodes)
            {
                float x = mapRect.x + 5 + (node.Rect.x - minX) * scale;
                float y = mapRect.y + 5 + (node.Rect.y - minY) * scale;
                float w = node.Rect.width * scale;
                float h = node.Rect.height * scale;

                Color c = GetNodeColor(node);
                c.a = 0.8f;
                EditorGUI.DrawRect(new Rect(x, y, Mathf.Max(w, 4), Mathf.Max(h, 3)), c);
            }

            // 뷰포트 표시
            float vpX = mapRect.x + 5 + (-scrollOffset.x / zoom - minX) * scale;
            float vpY = mapRect.y + 5 + (-scrollOffset.y / zoom - minY) * scale;
            float vpW = (position.width / zoom) * scale;
            float vpH = ((position.height - 20) / zoom) * scale;

            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.5f);
            Handles.DrawWireDisc(new Vector3(vpX + vpW/2, vpY + vpH/2, 0), Vector3.forward, 5);
            Handles.EndGUI();
        }

        #endregion

        #region Events

        void ProcessEvents()
        {
            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            // 캔버스 영역 체크
            if (!canvasRect.Contains(mousePos - new Vector2(0, 20))) return;

            // 월드 좌표로 변환
            Vector2 worldPos = ScreenToWorld(mousePos);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        // 노드 선택/드래그
                        FlowNode hitNode = nodes.LastOrDefault(n => n.Rect.Contains(worldPos));
                        if (hitNode != null)
                        {
                            selectedNode = hitNode;
                            draggedNode = hitNode;
                            dragOffset = worldPos - hitNode.Rect.position;
                            e.Use();
                        }
                        else
                        {
                            selectedNode = null;
                        }
                    }
                    else if (e.button == 2 || (e.button == 0 && e.alt))
                    {
                        // 캔버스 드래그
                        isDraggingCanvas = true;
                        lastMousePos = mousePos;
                        e.Use();
                    }
                    Repaint();
                    break;

                case EventType.MouseDrag:
                    if (draggedNode != null)
                    {
                        draggedNode.Rect.position = worldPos - dragOffset;
                        e.Use();
                        Repaint();
                    }
                    else if (isDraggingCanvas)
                    {
                        scrollOffset += mousePos - lastMousePos;
                        lastMousePos = mousePos;
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    draggedNode = null;
                    isDraggingCanvas = false;
                    break;

                case EventType.ScrollWheel:
                    float oldZoom = zoom;
                    zoom = Mathf.Clamp(zoom - e.delta.y * 0.05f, 0.3f, 2f);
                    
                    // 마우스 위치 기준 줌
                    if (zoom != oldZoom)
                    {
                        Vector2 zoomCenter = mousePos - new Vector2(0, 20);
                        scrollOffset += zoomCenter * (1 - zoom / oldZoom);
                    }
                    
                    e.Use();
                    Repaint();
                    break;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.F && selectedNode != null)
                    {
                        // 선택 노드로 포커스
                        scrollOffset = -selectedNode.Rect.center * zoom + 
                            new Vector2(position.width / 2, position.height / 2);
                        e.Use();
                        Repaint();
                    }
                    else if (e.keyCode == KeyCode.A)
                    {
                        // 전체 보기
                        FitAllNodes();
                        e.Use();
                        Repaint();
                    }
                    else if (e.keyCode == KeyCode.Home)
                    {
                        // 시작으로
                        var startNode = nodes.FirstOrDefault(n => n.Type == FlowNodeType.Start);
                        if (startNode != null)
                        {
                            scrollOffset = -startNode.Rect.center * zoom + 
                                new Vector2(position.width / 2, position.height / 2);
                        }
                        e.Use();
                        Repaint();
                    }
                    break;
            }
        }

        #endregion

        #region Parsing

        void ParseScript()
        {
            nodes.Clear();
            connections.Clear();

            if (currentScript == null) return;

            var rows = CsvUtility.SplitRecords(currentScript.text);
            
            string currentAnchor = "_START_";
            string lastChoiceId = null;
            int optionIndex = 0;

            // 시작 노드
            nodes.Add(new FlowNode
            {
                Id = "_START_",
                Type = FlowNodeType.Start,
                DisplayName = "START",
                LineIndex = 0
            });

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i].Text.Trim();
                int lineNumber = rows[i].StartLine;
                if (string.IsNullOrEmpty(row) || row.StartsWith("#") || row.StartsWith("LineID,"))
                    continue;

                var columns = CsvUtility.SplitCsv(row);
                if (columns.Length < 5) continue;

                string lineId = columns[0].Trim();
                string typeStr = columns[1].Trim();
                string speaker = columns[2].Trim();
                string value = columns[3].Trim();

                // LineID가 있으면 앵커 노드 생성
                if (!string.IsNullOrEmpty(lineId))
                {
                    // 이전 앵커에서 연결
                    if (!string.IsNullOrEmpty(currentAnchor) && currentAnchor != lineId)
                    {
                        // Jump가 아닌 자연스러운 흐름이면 연결
                        var lastNode = nodes.LastOrDefault(n => n.Id == currentAnchor);
                        if (lastNode != null && lastNode.Type != FlowNodeType.End)
                        {
                            connections.Add(new FlowConnection
                            {
                                FromId = currentAnchor,
                                ToId = lineId
                            });
                        }
                    }

                    currentAnchor = lineId;

                    // 앵커의 첫 대사를 SubText로 표시
                    string subText = "";
                    if (typeStr.Equals("Text", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(value))
                    {
                        subText = value.Length > 25 ? value.Substring(0, 23) + ".." : value;
                    }

                    nodes.Add(new FlowNode
                    {
                        Id = lineId,
                        Type = FlowNodeType.Anchor,
                        DisplayName = lineId,
                        SubText = subText,
                        FullText = string.IsNullOrEmpty(value) ? lineId : $"[{typeStr}] {speaker}: {value}",
                        LineIndex = lineNumber
                    });
                }

                // Type별 처리
                if (Enum.TryParse<LineType>(typeStr, true, out var type))
                {
                    switch (type)
                    {
                        case LineType.Flow:
                            ProcessFlowLine(value, currentAnchor, lineNumber);
                            break;

                        case LineType.Choice:
                            lastChoiceId = string.IsNullOrEmpty(lineId) ? $"_CHOICE_{lineNumber}" : lineId;
                            optionIndex = 0;
                            
                            if (!nodes.Exists(n => n.Id == lastChoiceId))
                            {
                                nodes.Add(new FlowNode
                                {
                                    Id = lastChoiceId,
                                    Type = FlowNodeType.Choice,
                                    DisplayName = "Choice",
                                    LineIndex = lineNumber
                                });

                                if (!string.IsNullOrEmpty(currentAnchor) && currentAnchor != lastChoiceId)
                                {
                                    connections.Add(new FlowConnection
                                    {
                                        FromId = currentAnchor,
                                        ToId = lastChoiceId
                                    });
                                }
                            }
                            break;

                        case LineType.Option:
                            if (!string.IsNullOrEmpty(lastChoiceId))
                            {
                                ProcessOptionLine(value, lastChoiceId, optionIndex++, lineNumber);
                            }
                            break;
                    }
                }
            }
        }

        void ProcessFlowLine(string value, string currentAnchor, int lineIndex)
        {
            var parts = value.Split(':');
            string command = parts[0].Trim();

            switch (command.ToLower())
            {
                case "jump":
                    if (parts.Length > 1)
                    {
                        string target = parts[1].Trim();
                        connections.Add(new FlowConnection
                        {
                            FromId = currentAnchor,
                            ToId = target,
                            Label = "Jump"
                        });
                    }
                    break;

                case "end":
                    string endId = $"_END_{lineIndex}";
                    nodes.Add(new FlowNode
                    {
                        Id = endId,
                        Type = FlowNodeType.End,
                        DisplayName = "END",
                        LineIndex = lineIndex
                    });
                    connections.Add(new FlowConnection
                    {
                        FromId = currentAnchor,
                        ToId = endId
                    });
                    break;

                case "if":
                    if (parts.Length >= 3)
                    {
                        string condition = parts[1].Trim();
                        string target = parts[2].Trim();
                        connections.Add(new FlowConnection
                        {
                            FromId = currentAnchor,
                            ToId = target,
                            Label = condition,
                            IsChoice = true
                        });
                    }
                    break;
            }
        }

        void ProcessOptionLine(string value, string choiceId, int optionIndex, int lineIndex)
        {
            var optionParts = value.Split('|');
            if (optionParts.Length < 2) return;

            string fullText = optionParts[0].Trim();
            string target = optionParts[1].Trim();
            
            // 효과/조건 추출
            string effects = "";
            for (int i = 2; i < optionParts.Length; i++)
            {
                if (!string.IsNullOrEmpty(effects)) effects += ", ";
                effects += optionParts[i].Trim();
            }

            // 표시용 텍스트 축약
            string displayText = fullText;
            if (displayText.Length > 20)
                displayText = displayText.Substring(0, 18) + "..";

            string optionId = $"{choiceId}_OPT{optionIndex}";
            
            nodes.Add(new FlowNode
            {
                Id = optionId,
                Type = FlowNodeType.Option,
                DisplayName = displayText,
                SubText = $"→ {target}",
                FullText = $"{fullText}\n→ {target}" + (string.IsNullOrEmpty(effects) ? "" : $"\n효과: {effects}"),
                LineIndex = lineIndex
            });

            // Choice → Option
            connections.Add(new FlowConnection
            {
                FromId = choiceId,
                ToId = optionId,
                IsChoice = true
            });

            // Option → Target
            connections.Add(new FlowConnection
            {
                FromId = optionId,
                ToId = target
            });
        }

        #endregion

        #region Layout

        void AutoLayout()
        {
            if (nodes.Count == 0) return;

            // 레벨 기반 레이아웃
            var levels = new Dictionary<string, int>();
            var visited = new HashSet<string>();
            var queue = new Queue<(string id, int level)>();

            // 시작점 찾기
            var startNode = nodes.FirstOrDefault(n => n.Type == FlowNodeType.Start);
            if (startNode != null)
            {
                queue.Enqueue((startNode.Id, 0));
            }

            // BFS로 레벨 할당
            while (queue.Count > 0)
            {
                var (id, level) = queue.Dequeue();
                if (visited.Contains(id)) continue;
                visited.Add(id);
                levels[id] = level;

                // 연결된 노드들
                foreach (var conn in connections.Where(c => c.FromId == id))
                {
                    if (!visited.Contains(conn.ToId))
                    {
                        queue.Enqueue((conn.ToId, level + 1));
                    }
                }
            }

            // 방문하지 않은 노드들 (고립된 노드)
            foreach (var node in nodes)
            {
                if (!levels.ContainsKey(node.Id))
                {
                    levels[node.Id] = 0;
                }
            }

            // 레벨별 노드 그룹화
            var levelGroups = nodes.GroupBy(n => levels.GetValueOrDefault(n.Id, 0))
                .OrderBy(g => g.Key)
                .ToList();

            // 위치 할당
            float startX = 50;
            float startY = 50;

            foreach (var group in levelGroups)
            {
                int level = group.Key;
                var nodesInLevel = group.ToList();

                float x = startX + level * NODE_SPACING_X;
                float totalHeight = nodesInLevel.Count * NODE_SPACING_Y;
                float startYOffset = -totalHeight / 2;

                for (int i = 0; i < nodesInLevel.Count; i++)
                {
                    var node = nodesInLevel[i];
                    float y = startY + startYOffset + i * NODE_SPACING_Y + 200;
                    node.Rect = new Rect(x, y, NODE_WIDTH, NODE_HEIGHT);
                }
            }

            // 뷰를 콘텐츠에 맞추기
            FitAllNodes();
        }

        /// <summary>
        /// 모든 노드가 보이도록 뷰 조정
        /// </summary>
        void FitAllNodes()
        {
            if (nodes.Count == 0) return;

            // 콘텐츠 바운드 계산
            float minX = nodes.Min(n => n.Rect.x);
            float maxX = nodes.Max(n => n.Rect.xMax);
            float minY = nodes.Min(n => n.Rect.y);
            float maxY = nodes.Max(n => n.Rect.yMax);

            float contentWidth = maxX - minX + 100;   // 여백 추가
            float contentHeight = maxY - minY + 100;

            // 콘텐츠 중앙
            Vector2 contentCenter = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);

            // 캨버스 크기
            float canvasWidth = position.width - 20;   // 여백
            float canvasHeight = position.height - 40; // 툴바 + 여백

            // 콘텐츠가 캨버스에 들어오도록 줌 계산
            float zoomX = canvasWidth / contentWidth;
            float zoomY = canvasHeight / contentHeight;
            zoom = Mathf.Clamp(Mathf.Min(zoomX, zoomY) * 0.9f, 0.3f, 1.5f); // 90%로 여유 두기

            // 스크롤 오프셋: 콘텐츠 중앙을 캨버스 중앙에 맞추기
            Vector2 canvasCenter = new Vector2(canvasWidth / 2, canvasHeight / 2);
            scrollOffset = canvasCenter - contentCenter * zoom;
        }

        #endregion

        #region Persistence

        void LoadLastScript()
        {
            string guid = EditorPrefs.GetString("LoveAlgo_Flowchart_LastScript", "");
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                currentScript = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (currentScript != null)
                {
                    ParseScript();
                    AutoLayout();
                }
            }
        }

        void SaveLastScript()
        {
            if (currentScript != null)
            {
                string path = AssetDatabase.GetAssetPath(currentScript);
                string guid = AssetDatabase.AssetPathToGUID(path);
                EditorPrefs.SetString("LoveAlgo_Flowchart_LastScript", guid);
            }
        }

        #endregion

        #region Data Classes

        enum FlowNodeType
        {
            Start,
            Anchor,
            Choice,
            Option,
            End
        }

        class FlowNode
        {
            public string Id;
            public FlowNodeType Type;
            public string DisplayName;
            public string SubText;
            public string FullText;  // 툴팁용 전체 텍스트
            public int LineIndex;
            public Rect Rect;
        }

        class FlowConnection
        {
            public string FromId;
            public string ToId;
            public string Label;
            public bool IsChoice;
        }

        #endregion
    }
}
