using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;

namespace Sabrevois.Gameplay
{
    /// <summary>
    /// Typed decisions from the local "system one" model (jev, default :8017).
    ///
    /// That model does not generate text: it reads the logits of answer letters for a set of
    /// criteria and returns the best-fitting letter. NPC choices (fight / flee / hold, and
    /// whether to speak up) are decided here; the text model on :8000 is only used afterwards
    /// to phrase the chosen line.
    ///
    /// Request:
    ///   { "model": "jevos-v4", "state": "...",
    ///     "questions": { "intent": { "type": "choice",
    ///                                 "criteria": { "A": "...", "B": "...", "C": "..." } } } }
    /// Response:
    ///   { "answers": { "intent": { "choice": "B", "probabilities": {...}, "confidence": 0.9 } } }
    /// </summary>
    public sealed class NpcReasoningService : ITickable, IDisposable
    {
        private const string DefaultModelName = "jevos-v4";
        private const string DefaultEndpoint = "http://127.0.0.1:8017/v1/systemone";
        private const float TimeoutSeconds = 4f;
        private const int FailureThreshold = 3;
        private const float UnavailableSeconds = 30f;

        [Inject(Optional = true)] private LocalModelSettings _modelSettings;

        private readonly List<ActiveRequest> _activeRequests = new();
        private string _endpoint;

        private int _consecutiveFailures;
        private float _retryAfterTime;

        private sealed class ActiveRequest
        {
            public UnityWebRequest Request;
            public float StartedAt;
        }

        /// <summary>False while the decision server is considered offline.</summary>
        public bool Available => Time.unscaledTime >= _retryAfterTime;

        private string Endpoint => _endpoint ??= ResolveEndpoint();

        private string ModelName =>
            _modelSettings?.GetModel<JevModel>()?.ModelName ?? DefaultModelName;

        private string ResolveEndpoint()
        {
            // The environment variable still wins so the game can be pointed at another
            // machine without touching the serialized game config.
            string custom = Environment.GetEnvironmentVariable("SABREVOIS_REASONING_ENDPOINT");
            if (!string.IsNullOrWhiteSpace(custom)) return custom.Trim();

            string baseUrl = _modelSettings?.GetModel<JevModel>()?.BaseUrl;
            return string.IsNullOrWhiteSpace(baseUrl)
                ? DefaultEndpoint
                : baseUrl.TrimEnd('/') + "/v1/systemone";
        }

        public void Tick()
        {
            if (_activeRequests.Count == 0) return;

            for (int i = _activeRequests.Count - 1; i >= 0; i--)
            {
                var active = _activeRequests[i];
                if (active.Request == null ||
                    Time.unscaledTime - active.StartedAt < TimeoutSeconds)
                    continue;

                _activeRequests.RemoveAt(i);
                Debug.LogWarning($"[NpcReasoning] Decision request exceeded {TimeoutSeconds:F0}s — aborting.");
                active.Request.Abort();
            }
        }

        public void Dispose()
        {
            foreach (var active in _activeRequests)
                active.Request?.Abort();
            _activeRequests.Clear();
        }

        /// <summary>Asks one multiple-choice question; returns the chosen letter or null.</summary>
        public async Task<string> ChooseAsync(string state, string questionId,
            params (string letter, string criterion)[] criteria)
        {
            if (!Available) return null;
            if (criteria == null || criteria.Length < 2) return null;

            var criteriaJson = new JObject();
            foreach (var (key, criterion) in criteria)
                criteriaJson[key] = criterion;

            var body = new JObject
            {
                ["model"] = ModelName,
                ["state"] = state,
                ["questions"] = new JObject
                {
                    [questionId] = new JObject
                    {
                        ["type"] = "choice",
                        ["criteria"] = criteriaJson
                    }
                }
            };

            JObject response = await PostAsync(questionId, body.ToString(Newtonsoft.Json.Formatting.None));
            if (response == null)
            {
                NoteFailure();
                return null;
            }

            string letter = (string)response["answers"]?[questionId]?["choice"];
            if (string.IsNullOrEmpty(letter))
            {
                NoteFailure();
                return null;
            }

            _consecutiveFailures = 0;
            return letter;
        }

        /// <summary>Yes/no decision. Null when the server could not answer.</summary>
        public async Task<bool?> AskAsync(string state, string questionId, string ifTrue, string ifFalse)
        {
            string letter = await ChooseAsync(state, questionId, ("A", ifTrue), ("B", ifFalse));
            if (letter == null) return null;
            return letter == "A";
        }

        private void NoteFailure()
        {
            _consecutiveFailures++;
            if (_consecutiveFailures < FailureThreshold) return;

            _consecutiveFailures = 0;
            _retryAfterTime = Time.unscaledTime + UnavailableSeconds;
            Debug.LogWarning(
                $"[NpcReasoning] Decision server looks offline — falling back to personality rules " +
                $"for {UnavailableSeconds:F0}s.");
        }

        private async Task<JObject> PostAsync(string label, string json)
        {
            var www = UnityWebRequest.Post(Endpoint, json, "application/json");
            www.timeout = Mathf.CeilToInt(TimeoutSeconds);

            // Tick() aborts anything past the budget: UnityWebRequest.timeout does not fire
            // for every kind of stalled connect.
            var active = new ActiveRequest { Request = www, StartedAt = Time.unscaledTime };
            _activeRequests.Add(active);

            var tcs = new TaskCompletionSource<JObject>();

            try
            {
                www.SendWebRequest().completed += _ =>
                {
                    try
                    {
                        if (www.result != UnityWebRequest.Result.Success)
                        {
                            Debug.LogWarning(
                                $"[NpcReasoning] {label} failed ({www.result}): {www.error}");
                            tcs.TrySetResult(null);
                            return;
                        }

                        tcs.TrySetResult(JObject.Parse(www.downloadHandler?.text ?? "{}"));
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[NpcReasoning] {label} callback failed: {e.Message}");
                        tcs.TrySetResult(null);
                    }
                };

                return await tcs.Task;
            }
            finally
            {
                _activeRequests.Remove(active);
                www.Dispose();
            }
        }
    }
}
