using UnityEditor;
using UnityEngine;
using System.Linq;
using Zenject;
using JetBrains.Annotations;
using System.Collections.Generic;
using Sabrevois.AI.DataSources;

#if UNITY_EDITOR
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

        private class AgentActionHistory
        {
            public string ActionName;
            public System.DateTime Timestamp;
        }

        private Vector2 _scrollPos;
        private Vector2[] _boxScrolls = new Vector2[32];
        private CachedThreadState[] _cachedThreadStates = new CachedThreadState[0];
        private CachedAgentState[] _cachedAgentStates = new CachedAgentState[0];
        private float _cachedAvgTime;
        private float _cachedThroughput;
        private int _selectedTab = 0;
        private double _lastUpdateTime;
        private bool _showDetails = true;
        private readonly string[] _tabs = { "Worker Threads", "Agents", "Agent Inspector" };
        private Dictionary<int, List<AgentActionHistory>> _agentActionHistories = new Dictionary<int, List<AgentActionHistory>>();
        private List<IDataSource> _dataSources;
        private string _dataSourceSearchQuery = "";

        [MenuItem("Window/AI/Decision Making Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<AIDecisionDebuggerWindow>("AI Decision Debugger");
            window.Show();
        }

        private void OnEnable()
        {
            _dataSources = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(IDataSource).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                .Select(type => (IDataSource)System.Activator.CreateInstance(type))
                .ToList();
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void Update()
        {
            if (EditorApplication.timeSinceStartup - _lastUpdateTime > 0.1)
            {
                _lastUpdateTime = EditorApplication.timeSinceStartup;
                UpdateData();
            }
        }

        private void OnGUI()
        {
            var service = FindService();
            if (service == null)
            {
                EditorGUILayout.HelpBox("IDecisionMakingService not found in current scene context.", MessageType.Warning);
                return;
            }

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 10, 10), margin = new RectOffset(5, 5, 5, 5) };
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label) { richText = true };

            DrawGlobalMetrics(boxStyle, labelStyle);

            GUILayout.Space(10);
            
            _showDetails = GUILayout.Toggle(_showDetails, "Show Detailed History");
            if (_showDetails)
            {
                GUILayout.Space(10);
                _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);
                GUILayout.Space(10);

                if (Event.current.type == EventType.MouseDrag && Event.current.button == 2)
                {
                    _scrollPos -= Event.current.delta;
                    Event.current.Use();
                    Repaint();
                }

                _scrollPos = GUILayout.BeginScrollView(_scrollPos);
                GUILayout.BeginHorizontal();

                switch (_selectedTab)
                {
                    case 0: DrawWorkerThreadsTab(boxStyle, labelStyle); break;
                    case 1: DrawAgentsTab(boxStyle, labelStyle); break;
                    case 2: DrawAgentInspectorTab(boxStyle, labelStyle, service); break;
                }

                GUILayout.EndHorizontal();
                GUILayout.EndScrollView();
            }
        }

        private void UpdateData()
        {
            var service = FindService();
            if (service == null) return;

            if (service is ParallelDecisionMakingService parallelService)
            {
                var mapped = parallelService.EditorThreadStates.Values.Select(t => new CachedThreadState {
                    ThreadId = t.ThreadId,
                    ThreadName = t.ThreadName,
                    History = t.History.Select(req => new CachedRequestInfo {
                        GameObjectId = req.GameObjectId,
                        AgentName = req.AgentName,
                        Status = req.Status,
                        TimeElapsedMs = req.TimeElapsedMs,
                        ChosenAction = req.ChosenAction,
                        ThreadId = t.ThreadId,
                        Timestamp = req.Timestamp
                    }).ToArray()
                });
                UpdateMetrics(parallelService.GetAverageChoosingTime(), parallelService.GetAverageThroughput(), mapped);
            }
            else if (service is SequentialDecisionMakingService sequentialService)
            {
                var mapped = sequentialService.EditorThreadStates.Values.Select(t => new CachedThreadState {
                    ThreadId = t.ThreadId,
                    ThreadName = t.ThreadName,
                    History = t.History.Select(req => new CachedRequestInfo {
                        GameObjectId = req.GameObjectId,
                        AgentName = req.AgentName,
                        Status = req.Status,
                        TimeElapsedMs = req.TimeElapsedMs,
                        ChosenAction = req.ChosenAction,
                        ThreadId = t.ThreadId,
                        Timestamp = req.Timestamp
                    }).ToArray()
                });
                UpdateMetrics(sequentialService.GetAverageChoosingTime(), sequentialService.GetAverageThroughput(), mapped);
            }
        }

        private void UpdateMetrics(float avgTime, float throughput, IEnumerable<CachedThreadState> threadStates)
        {
            _cachedAvgTime = avgTime;
            _cachedThroughput = throughput;

            var threadStatesList = new List<CachedThreadState>();
            var allRequests = new List<CachedRequestInfo>();

            foreach (var t in threadStates)
            {
                threadStatesList.Add(t);
                allRequests.AddRange(t.History);
            }

            _cachedThreadStates = threadStatesList.OrderBy(t => t.ThreadId).ToArray();

            if (_selectedTab == 1 || _selectedTab == 2)
            {
                allRequests = allRequests.OrderBy(req => req.Timestamp).ToList();

                if (_selectedTab == 1)
                {
                    _cachedAgentStates = allRequests
                        .GroupBy(req => req.GameObjectId)
                        .Select(g => new CachedAgentState {
                            GameObjectId = g.Key,
                            AgentName = g.First().AgentName,
                            History = g.ToArray()
                        })
                        .OrderBy(a => a.AgentName)
                        .ToArray();
                }

                if (_selectedTab == 2)
                {
                    _agentActionHistories.Clear();
                    foreach (var req in allRequests)
                    {
                        if (!_agentActionHistories.ContainsKey(req.GameObjectId))
                        {
                            _agentActionHistories[req.GameObjectId] = new List<AgentActionHistory>();
                        }

                        if (req.Status == "Done" && !string.IsNullOrEmpty(req.ChosenAction))
                        {
                            var historyList = _agentActionHistories[req.GameObjectId];
                            if (historyList.Count == 0 || historyList.Last().ActionName != req.ChosenAction)
                            {
                                historyList.Add(new AgentActionHistory { ActionName = req.ChosenAction, Timestamp = req.Timestamp });
                            }
                        }
                    }

                    const int maxHistorySize = 10;
                    foreach (var historyList in _agentActionHistories.Values)
                    {
                        if (historyList.Count > maxHistorySize)
                        {
                            historyList.RemoveRange(0, historyList.Count - maxHistorySize);
                        }
                    }
                }
            }
        }

        private void DrawGlobalMetrics(GUIStyle boxStyle, GUIStyle labelStyle)
        {
            GUILayout.BeginVertical(boxStyle);
            GUILayout.Label("<b>Global Metrics</b>", labelStyle);
            GUILayout.Label($"Average Time: {_cachedAvgTime * 1000f:F3}ms", labelStyle);
            var tp = _cachedThroughput;
            GUILayout.Label($"Throughput: {(float.IsInfinity(tp) || float.IsNaN(tp) ? 0 : tp):F2} req/s", labelStyle);
            GUILayout.EndVertical();
        }

        private void DrawWorkerThreadsTab(GUIStyle boxStyle, GUIStyle labelStyle)
        {
            float windowWidth = EditorGUIUtility.currentViewWidth;
            float currentWidth = 0f;
            float boxActualWidth = 420f;
            float boxHeight = 360f;
            int boxIndex = 0;

            foreach (var thread in _cachedThreadStates)
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

                EnsureBoxScrollsSize(boxIndex);
                _boxScrolls[boxIndex] = GUILayout.BeginScrollView(_boxScrolls[boxIndex]);
                
                for (int i = thread.History.Length - 1; i >= 0; i--)
                {
                    DrawRequestHistoryItem(thread.History[i], labelStyle);
                }
                GUILayout.EndScrollView();

                GUILayout.EndVertical();
                boxIndex++;
            }
        }

        private void DrawAgentsTab(GUIStyle boxStyle, GUIStyle labelStyle)
        {
            float windowWidth = EditorGUIUtility.currentViewWidth;
            float currentWidth = 0f;
            float boxActualWidth = 420f;
            float boxHeight = 360f;
            int boxIndex = 0;

            foreach (var agent in _cachedAgentStates)
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

                EnsureBoxScrollsSize(boxIndex);
                _boxScrolls[boxIndex] = GUILayout.BeginScrollView(_boxScrolls[boxIndex]);
                
                for (int i = agent.History.Length - 1; i >= 0; i--)
                {
                    DrawDetailedRequestHistoryItem(agent.History[i], labelStyle);
                }
                GUILayout.EndScrollView();

                GUILayout.EndVertical();
                boxIndex++;
            }
        }

        private void DrawAgentInspectorTab(GUIStyle boxStyle, GUIStyle labelStyle, IDecisionMakingService service)
        {
            GUILayout.BeginVertical();

            var selectedAgents = Selection.gameObjects.Select(go => go.GetComponent<Agent>()).Where(a => a != null).Take(4).ToList();

            if (Selection.gameObjects.Length > 4)
            {
                EditorGUILayout.HelpBox("A maximum of 4 agents can be displayed at once.", MessageType.Info);
            }

            int boxIndex = 0;
            foreach (var agent in selectedAgents)
            {
                GUILayout.BeginHorizontal(boxStyle);

                GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.45f));
                
                GUILayout.Label($"<color=yellow><b>{agent.Name}</b></color>", labelStyle);
                GUILayout.Space(5);

                GUILayout.Label($"Current Action: {agent.CurrentAction?.GetType().Name ?? "None"}");
                GUILayout.Label($"Interruptible: {agent.CurrentAction?.Interruptible}");

                GUILayout.Space(10);
                GUILayout.Label("<b>Actions & Considerations</b>", labelStyle);
                
                EnsureBoxScrollsSize(boxIndex);
                _boxScrolls[boxIndex] = GUILayout.BeginScrollView(_boxScrolls[boxIndex], GUILayout.Height(150));
                
                if (service is ParallelDecisionMakingService parallelService && parallelService.EditorConsiderations.TryGetValue(agent.gameObject.GetInstanceID(), out var considerations))
                {
                    foreach (var consideration in considerations)
                    {
                        GUILayout.Label($"{consideration.ActionName}: {consideration.Utility:F4}");
                    }
                }
                else
                {
                    GUILayout.Label("No consideration data available (needs ParallelDecisionMakingService update).");
                }
                GUILayout.EndScrollView();
                boxIndex++;

                GUILayout.Space(10);
                GUILayout.Label("<b>Action History</b>", labelStyle);

                EnsureBoxScrollsSize(boxIndex);
                _boxScrolls[boxIndex] = GUILayout.BeginScrollView(_boxScrolls[boxIndex], GUILayout.Height(150));
                
                if (_agentActionHistories.TryGetValue(agent.gameObject.GetInstanceID(), out var history))
                {
                    DrawActionHistory(history);
                }
                GUILayout.EndScrollView();
                boxIndex++;

                GUILayout.EndVertical();
                GUILayout.Space(20);

                GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.45f));
                GUILayout.Label("<b>Data Sources</b>", labelStyle);
                
                GUILayout.BeginHorizontal();
                GUILayout.Label("Search: ", GUILayout.Width(50));
                _dataSourceSearchQuery = GUILayout.TextField(_dataSourceSearchQuery);
                GUILayout.EndHorizontal();
                
                GUILayout.Space(5);

                EnsureBoxScrollsSize(boxIndex);
                _boxScrolls[boxIndex] = GUILayout.BeginScrollView(_boxScrolls[boxIndex]);
                
                foreach (var dataSource in _dataSources)
                {
                    string dsName = dataSource.GetType().Name;
                    if (string.IsNullOrEmpty(_dataSourceSearchQuery) || dsName.IndexOf(_dataSourceSearchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        GUILayout.Label($"{dsName}: {dataSource.GetValue(agent.gameObject)}");
                    }
                }
                GUILayout.EndScrollView();
                boxIndex++;

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            
            GUILayout.EndVertical();
        }

        private void DrawRequestHistoryItem(CachedRequestInfo req, GUIStyle labelStyle)
        {
            string objName = string.IsNullOrEmpty(req.AgentName) ? $"Obj {req.GameObjectId}" : req.AgentName;
            string chosenAction = GetShortActionName(req);
            string timeStr = req.Timestamp.ToString("HH:mm:ss");
            string msNsStr = req.Status == "Done" ? $"{req.TimeElapsedMs:F4}ms" : "";
            Color c = GetAgeColor(req.Timestamp);

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

        private void DrawDetailedRequestHistoryItem(CachedRequestInfo req, GUIStyle labelStyle)
        {
            string chosenAction = GetShortActionName(req);
            string timeStr = req.Timestamp.ToString("HH:mm:ss");
            string msNsStr = req.Status == "Done" ? $"{req.TimeElapsedMs:F4}ms" : "";
            string tName = _cachedThreadStates.FirstOrDefault(t => t.ThreadId == req.ThreadId)?.ThreadName;
            string thrdNum = string.IsNullOrEmpty(tName) ? "?" : tName.Substring(tName.Length - 1);
            Color c = GetAgeColor(req.Timestamp);

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

        private void DrawActionHistory(List<AgentActionHistory> history)
        {
            var actionsWithDurations = new List<(string ActionName, double Duration)>();
            if (history.Count > 0)
            {
                for (int i = 0; i < history.Count - 1; i++)
                {
                    actionsWithDurations.Add((history[i].ActionName, (history[i+1].Timestamp - history[i].Timestamp).TotalSeconds));
                }
                var lastAction = history[history.Count - 1];
                actionsWithDurations.Add((lastAction.ActionName, (System.DateTime.Now - lastAction.Timestamp).TotalSeconds));
            }

            foreach (var action in actionsWithDurations.AsEnumerable().Reverse())
            {
                GUILayout.Label($"[{action.Duration:F2}s] {action.ActionName}");
            }
        }

        private string GetShortActionName(CachedRequestInfo req)
        {
            string chosenAction = req.Status == "Done" ? req.ChosenAction : "...";
            if (chosenAction != null && chosenAction.EndsWith("Action")) 
                chosenAction = chosenAction.Substring(0, chosenAction.Length - 6);
            return chosenAction ?? "...";
        }

        private Color GetAgeColor(System.DateTime timestamp)
        {
            double age = (System.DateTime.Now - timestamp).TotalSeconds;
            if (age < 1.0)
                return Color.Lerp(Color.green, Color.white, (float)age);
            return Color.white;
        }

        private void EnsureBoxScrollsSize(int requiredIndex)
        {
            if (requiredIndex >= _boxScrolls.Length)
            {
                System.Array.Resize(ref _boxScrolls, Mathf.Max(_boxScrolls.Length * 2, requiredIndex + 1));
            }
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
#endif