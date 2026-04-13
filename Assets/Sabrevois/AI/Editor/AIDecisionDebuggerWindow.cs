using UnityEditor;
using UnityEngine;
using System.Linq;
using Sabrevois.AI.Parallel;
using Sabrevois.AI;
using Zenject;
using JetBrains.Annotations;

namespace Sabrevois.AI.Editor
{
    public class AIDecisionDebuggerWindow : EditorWindow
    {
        private class CachedRequestInfo
        {
            public int GameObjectId;
            public string AgentName;
            public string Status;
            public float TimeElapsedMs;
            public double TimeElapsedNs;
            public string ChosenAction;
            public int ThreadId;
            public System.DateTime Timestamp;
        }

        private class CachedThreadState
        {
            public int ThreadId;
            public string ThreadName;
            public CachedRequestInfo[] History;
        }

        private class CachedAgentState
        {
            public int GameObjectId;
            public string AgentName;
            public CachedRequestInfo[] History;
        }

        private Vector2 _scrollPos;
        private Vector2[] _boxScrolls = new Vector2[32];
        private CachedThreadState[] _cachedThreadStates = new CachedThreadState[0];
        private CachedAgentState[] _cachedAgentStates = new CachedAgentState[0];
        private float _cachedAvgTime;
        private float _cachedThroughput;
        private int _cachedReqCount;
        private int _selectedTab = 0;
        private double _lastUpdateTime;
        private readonly string[] _tabs = { "Worker Threads", "Agents" };
        
        [MenuItem("Window/AI/Decision Making Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<AIDecisionDebuggerWindow>("AI Decision Debugger");
            window.Show();
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.padding = new RectOffset(10, 10, 10, 10);
            boxStyle.margin = new RectOffset(5, 5, 5, 5);

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.richText = true;
            
            var service = FindService();

            if (service == null)
            {
                EditorGUILayout.HelpBox("ParallelDecisionMakingService not found in current scene context.", MessageType.Warning);
                return;
            }

            if (Event.current.type == EventType.Layout)
            {
                if (EditorApplication.timeSinceStartup - _lastUpdateTime > 0.1)
                {
                    _lastUpdateTime = EditorApplication.timeSinceStartup;

                    var parallelService = service as ParallelDecisionMakingService;
                    var sequentialService = service as SequentialDecisionMakingService;

                    if (parallelService != null)
                    {
                        _cachedAvgTime = parallelService.GetAverageChoosingTime();
                        _cachedThroughput = parallelService.GetAverageThroughput();
                        _cachedReqCount = parallelService.EditorRequests.Count;

                        _cachedThreadStates = parallelService.EditorThreadStates.Values.OrderBy(t => t.ThreadId).Select(t => new CachedThreadState { 
                            ThreadId = t.ThreadId, 
                            ThreadName = t.ThreadName,
                            History = t.History.Select(req => new CachedRequestInfo {
                                GameObjectId = req.GameObjectId,
                                AgentName = req.AgentName,
                                Status = req.Status,
                                TimeElapsedMs = req.TimeElapsedMs,
                                TimeElapsedNs = req.TimeElapsedNs,
                                ChosenAction = req.ChosenAction,
                                ThreadId = t.ThreadId,
                                Timestamp = req.Timestamp
                            }).ToArray()
                        }).ToArray();

                        if (_selectedTab == 1)
                        {
                            _cachedAgentStates = parallelService.EditorThreadStates.Values
                                .SelectMany(t => t.History.Select(req => new CachedRequestInfo {
                                    GameObjectId = req.GameObjectId,
                                    AgentName = req.AgentName,
                                    Status = req.Status,
                                    TimeElapsedMs = req.TimeElapsedMs,
                                    TimeElapsedNs = req.TimeElapsedNs,
                                    ChosenAction = req.ChosenAction,
                                    ThreadId = t.ThreadId,
                                    Timestamp = req.Timestamp
                                }))
                                .GroupBy(req => req.GameObjectId)
                                .Select(g => new CachedAgentState {
                                    GameObjectId = g.Key,
                                    AgentName = g.First().AgentName,
                                    History = g.ToArray()
                                })
                                .OrderBy(a => a.AgentName)
                                .ToArray();
                        }
                    }
                    else if (sequentialService != null)
                    {
                        _cachedAvgTime = sequentialService.GetAverageChoosingTime();
                        _cachedThroughput = sequentialService.GetAverageThroughput();
                        _cachedReqCount = sequentialService.EditorRequestsCount;

                        _cachedThreadStates = sequentialService.EditorThreadStates.Values.OrderBy(t => t.ThreadId).Select(t => new CachedThreadState { 
                            ThreadId = t.ThreadId, 
                            ThreadName = t.ThreadName,
                            History = t.History.Select(req => new CachedRequestInfo {
                                GameObjectId = req.GameObjectId,
                                AgentName = req.AgentName,
                                Status = req.Status,
                                TimeElapsedMs = req.TimeElapsedMs,
                                TimeElapsedNs = req.TimeElapsedNs,
                                ChosenAction = req.ChosenAction,
                                ThreadId = t.ThreadId,
                                Timestamp = req.Timestamp
                            }).ToArray()
                        }).ToArray();

                        if (_selectedTab == 1)
                        {
                            _cachedAgentStates = sequentialService.EditorThreadStates.Values
                                .SelectMany(t => t.History.Select(req => new CachedRequestInfo {
                                    GameObjectId = req.GameObjectId,
                                    AgentName = req.AgentName,
                                    Status = req.Status,
                                    TimeElapsedMs = req.TimeElapsedMs,
                                    TimeElapsedNs = req.TimeElapsedNs,
                                    ChosenAction = req.ChosenAction,
                                    ThreadId = t.ThreadId,
                                    Timestamp = req.Timestamp
                                }))
                                .GroupBy(req => req.GameObjectId)
                                .Select(g => new CachedAgentState {
                                    GameObjectId = g.Key,
                                    AgentName = g.First().AgentName,
                                    History = g.ToArray()
                                })
                                .OrderBy(a => a.AgentName)
                                .ToArray();
                        }
                    }
                }
            }

            GUILayout.BeginVertical(boxStyle);
            GUILayout.Label("<b>Global Metrics</b>", labelStyle);
            GUILayout.Label($"Average Time: {_cachedAvgTime * 1000f:F3}ms", labelStyle);
            var tp = _cachedThroughput;
            GUILayout.Label($"Throughput: {(float.IsInfinity(tp) || float.IsNaN(tp) ? 0 : tp):F2} req/s", labelStyle);
            GUILayout.EndVertical();

            GUILayout.Space(10);
            
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);
            GUILayout.Space(10);

            if (UnityEngine.Event.current.type == EventType.MouseDrag && UnityEngine.Event.current.button == 2)
            {
                _scrollPos -= UnityEngine.Event.current.delta;
                UnityEngine.Event.current.Use();
                Repaint();
            }

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            GUILayout.BeginHorizontal();

            float windowWidth = EditorGUIUtility.currentViewWidth;
            float currentWidth = 0f;
            float boxActualWidth = 420f; // 410 width + 10 margin approx
            float boxHeight = 360f; // Approx height for 15 entries

            if (_selectedTab == 0)
            {
                var threadStates = _cachedThreadStates;

                int boxIndex = 0;
                foreach (var thread in threadStates)
                {
                    if (currentWidth + boxActualWidth > windowWidth && currentWidth > 0f)
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        currentWidth = 0f;
                    }
                    currentWidth += boxActualWidth;

                    GUILayout.BeginVertical(boxStyle, GUILayout.Width(410), GUILayout.Height(boxHeight));
                    GUILayout.Label($"<color=cyan><b>{thread.ThreadName} [{thread.ThreadId}]</b></color>", labelStyle);
                    GUILayout.Space(5);

                    _boxScrolls[boxIndex] = GUILayout.BeginScrollView(_boxScrolls[boxIndex]);
                    var history = thread.History;
                    for (int i = history.Length - 1; i >= 0; i--)
                    {
                        var req = history[i];
                        string objName = string.IsNullOrEmpty(req.AgentName) ? $"Obj {req.GameObjectId}" : req.AgentName;
                        string chosenAction = req.Status == "Done" ? req.ChosenAction : "...";
                        if (chosenAction.EndsWith("Action")) chosenAction = chosenAction.Substring(0, chosenAction.Length - 6);
                        string timeStr = req.Timestamp.ToString("HH:mm:ss");
                        string msNsStr = req.Status == "Done" ? $"{req.TimeElapsedMs:F4}ms" : "";
                        
                        double age = (System.DateTime.Now - req.Timestamp).TotalSeconds;
                        Color c = Color.white;
                        if (age < 1.0)
                            c = Color.Lerp(Color.green, Color.white, (float)age);

                        GUILayout.BeginHorizontal();
                        Color oldColor = GUI.contentColor;
                        GUI.contentColor = Color.grey;
                        GUILayout.Label($"[{timeStr}]", labelStyle, GUILayout.Width(65));
                        GUI.contentColor = c;
                        GUILayout.Label(objName, labelStyle, GUILayout.Width(105));
                        GUILayout.Label($"<b>{chosenAction}</b>", labelStyle, GUILayout.Width(120));
                        GUILayout.Label(msNsStr, labelStyle);
                        GUI.contentColor = oldColor;
                        GUILayout.EndHorizontal();
                        
                        GUILayout.Space(2);
                    }
                    GUILayout.EndScrollView();

                    GUILayout.EndVertical();
                    boxIndex++;
                }
            }
            else
            {
                var agentStates = _cachedAgentStates;
                int boxIndex = 0;

                foreach (var agent in agentStates)
                {
                    if (currentWidth + boxActualWidth > windowWidth && currentWidth > 0f)
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        currentWidth = 0f;
                    }
                    currentWidth += boxActualWidth;

                    GUILayout.BeginVertical(boxStyle, GUILayout.Width(410), GUILayout.Height(boxHeight));
                    string objName = string.IsNullOrEmpty(agent.AgentName) ? $"Obj {agent.GameObjectId}" : agent.AgentName;
                    GUILayout.Label($"<color=yellow><b>{objName}</b></color>", labelStyle);
                    GUILayout.Space(5);

                    if (boxIndex >= _boxScrolls.Length)
                    {
                        System.Array.Resize(ref _boxScrolls, _boxScrolls.Length * 2);
                    }

                    _boxScrolls[boxIndex] = GUILayout.BeginScrollView(_boxScrolls[boxIndex]);
                    var history = agent.History;
                    for (int i = history.Length - 1; i >= 0; i--)
                    {
                        var req = history[i];
                        string chosenAction = req.Status == "Done" ? req.ChosenAction : "...";
                        if (chosenAction.EndsWith("Action")) chosenAction = chosenAction.Substring(0, chosenAction.Length - 6);
                        string timeStr = req.Timestamp.ToString("HH:mm:ss");
                        string msNsStr = req.Status == "Done" ? $"{req.TimeElapsedMs:F4}ms" : "";
                        string tName = _cachedThreadStates.FirstOrDefault(t => t.ThreadId == req.ThreadId)?.ThreadName;
                        string thrdNum = string.IsNullOrEmpty(tName) ? "?" : tName.Substring(tName.Length - 1);
                        
                        double age = (System.DateTime.Now - req.Timestamp).TotalSeconds;
                        Color c = Color.white;
                        if (age < 1.0)
                            c = Color.Lerp(Color.green, Color.white, (float)age);

                        GUILayout.BeginHorizontal();
                        Color oldColor = GUI.contentColor;
                        GUI.contentColor = Color.grey;
                        GUILayout.Label($"[{timeStr}]", labelStyle, GUILayout.Width(65));
                        GUI.contentColor = c;
                        GUILayout.Label($"Thread {thrdNum}", labelStyle, GUILayout.Width(60));
                        GUILayout.Label($"<b>{chosenAction}</b>", labelStyle, GUILayout.Width(135));
                        GUILayout.Label(msNsStr, labelStyle, GUILayout.Width(100));
                        GUI.contentColor = oldColor;
                        GUILayout.EndHorizontal();
                        
                        GUILayout.Space(2);
                    }
                    GUILayout.EndScrollView();

                    GUILayout.EndVertical();
                    boxIndex++;
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        [CanBeNull]
        private IDecisionMakingService FindService()
        {
            if (!Application.isPlaying) 
                return null;
                
            var context = Object.FindAnyObjectByType<SceneContext>();
            if (context != null && context.Container != null)
            {
                return context.Container.TryResolve<IDecisionMakingService>();
            }

            return null;
        }
    }
}
