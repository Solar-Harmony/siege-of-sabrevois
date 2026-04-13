using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Sabrevois.AI.Actions;

#if UNITY_EDITOR
namespace Sabrevois.AI
{
    public partial class SequentialDecisionMakingService
    {
        public class ThreadState
        {
            public int ThreadId;
            public string ThreadName;
            public ConcurrentQueue<RequestInfo> History = new();
        }

        public class RequestInfo
        {
            public int GameObjectId;
            public string AgentName;
            public string Status;
            public float TimeElapsedMs;
            public double TimeElapsedNs;
            public string ChosenAction;
            public DateTime Timestamp;
        }

        public readonly ConcurrentDictionary<int, ThreadState> EditorThreadStates = new();
        public int EditorRequestsCount = 0;

        partial void EditorInitThreadState()
        {
            EditorThreadStates[1] = new ThreadState { ThreadId = 1, ThreadName = "Main Thread" };
        }

        partial void EditorBeginRequest(int gameObjectId, string agentName, ref object reqInfoObj)
        {
            var reqInfo = new RequestInfo
            {
                GameObjectId = gameObjectId,
                AgentName = agentName,
                Status = "In Progress",
                TimeElapsedMs = 0f,
                Timestamp = DateTime.Now
            };
            reqInfoObj = reqInfo;

            if (EditorThreadStates.TryGetValue(1, out var ts))
            {
                ts.History.Enqueue(reqInfo);
                if (ts.History.Count > 50)
                    ts.History.TryDequeue(out _);
            }
        }

        partial void EditorEndRequest(object reqInfoObj, ActionInstance chosenAction, Stopwatch sw)
        {
            if (reqInfoObj is RequestInfo reqInfo)
            {
                reqInfo.Status = "Done";
                reqInfo.TimeElapsedMs = (float)sw.Elapsed.TotalMilliseconds;
                reqInfo.TimeElapsedNs = (double)sw.ElapsedTicks / Stopwatch.Frequency * 1_000_000_000;
                reqInfo.ChosenAction = chosenAction?.Action?.GetType().Name ?? "None";
            }
        }
    }
}
#endif

