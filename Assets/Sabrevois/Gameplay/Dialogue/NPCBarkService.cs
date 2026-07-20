using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SolarHarmony.DynamicWounds2D;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;

namespace Sabrevois.Gameplay
{
    public class NPCBarkService : IInitializable, IDisposable
    {
        private const string Endpoint = "http://127.0.0.1:1234/v1/chat/completions";
        private const float CooldownSeconds = 4f;
        private const int MaxResponseTokens = 40;

        private readonly Dictionary<Health, float> _lastBarkTimes = new();

        public void Initialize()
        {
            WoundsComponent.OnAnyWoundCreated += HandleWoundCreated;
            WoundsComponent.OnAnyLimbSevered += HandleLimbSevered;
            Health.OnAnyDeath += HandleDeath;
        }

        public void Dispose()
        {
            WoundsComponent.OnAnyWoundCreated -= HandleWoundCreated;
            WoundsComponent.OnAnyLimbSevered -= HandleLimbSevered;
            Health.OnAnyDeath -= HandleDeath;
        }

        private async void HandleWoundCreated(WoundsComponent wounds, Wound wound, RaycastHit hit)
        {
            var health = wounds.GetComponentInParent<Health>();
            if (health == null || !TryClaimCooldown(health)) return;

            var personality = health.BarkPersonality;
            string bodyPart = ResolveBodyPartName(wounds, wound.Position);
            string severity = wound.Gravity > 0.7f ? "severe" :
                wound.Gravity > 0.3f ? "moderate" : "light";
            string prompt = $"You just took a {severity} hit to the {bodyPart}. React with one short sentence in-character.";

            string result = await RequestBarkAsync(health.name, personality, prompt);
            LogBark(health.name, result);
        }

        private async void HandleLimbSevered(WoundsComponent wounds, GameObject severedPart,
            Vector3 hitDirection)
        {
            var health = wounds.GetComponentInParent<Health>();
            if (health == null || !TryClaimCooldown(health)) return;

            var personality = health.BarkPersonality;
            string limbName = severedPart != null ? severedPart.name : "limb";
            string prompt =
                $"Your {limbName} was just severed. React with one short sentence in-character.";

            string result = await RequestBarkAsync(health.name, personality, prompt);
            LogBark(health.name, result);
        }

        private async void HandleDeath(Health health)
        {
            if (health == null) return;
            var personality = health.BarkPersonality;
            string prompt = "You are dying. Say your final short sentence in-character.";

            string result = await RequestBarkAsync(health.name, personality, prompt);
            LogBark(health.name, result);
        }

        private bool TryClaimCooldown(Health health)
        {
            if (_lastBarkTimes.TryGetValue(health, out float lastTime))
            {
                if (Time.time - lastTime < CooldownSeconds)
                    return false;
            }

            _lastBarkTimes[health] = Time.time;
            return true;
        }

        private static async Task<string> RequestBarkAsync(
            string npcName, BarkPersonality personality, string prompt)
        {
            string json = BuildRequestBody(
                personality != null ? personality.SystemPrompt : null,
                prompt,
                MaxResponseTokens,
                0.8f);

            using var www = UnityWebRequest.Post(Endpoint, json, "application/json");
            www.timeout = 10;

            var tcs = new TaskCompletionSource<string>();
            www.SendWebRequest().completed += _ =>
            {
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning(
                        $"[NPCBarkService] Request failed for {npcName}: {www.error}");
                    tcs.TrySetResult(null);
                    return;
                }

                try
                {
                    var response =
                        JsonUtility.FromJson<BarkResponse>(www.downloadHandler.text);
                    if (response?.choices != null && response.choices.Length > 0)
                    {
                        string content = response.choices[0].message.content;
                        content = content.Trim().Trim('"', '\'');
                        tcs.TrySetResult(content);
                    }
                    else
                    {
                        tcs.TrySetResult(null);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[NPCBarkService] Parse error: {e.Message}");
                    tcs.TrySetResult(null);
                }
            };

            return await tcs.Task;
        }

        private static void LogBark(string npcName, string bark)
        {
            if (!string.IsNullOrWhiteSpace(bark))
                Debug.Log($"[Bark:{npcName}] {bark}");
        }

        private static string BuildRequestBody(
            string systemPrompt, string userPrompt, int maxTokens, float temperature)
        {
            var sb = new StringBuilder();
            sb.Append("{\"messages\":[");

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                string escapedSystem = EscapeJson(systemPrompt);
                sb.Append($"{{\"role\":\"system\",\"content\":\"{escapedSystem}\"}},");
            }

            string escapedUser = EscapeJson(userPrompt);
            sb.Append($"{{\"role\":\"user\",\"content\":\"{escapedUser}\"}}");

            sb.Append($"],\"max_tokens\":{maxTokens},\"temperature\":{temperature}}}");
            return sb.ToString();
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r");
        }

        private static string ResolveBodyPartName(WoundsComponent wounds, Vector2 uv)
        {
            if (wounds == null) return "body";
            var atlas = wounds.AtlasData;
            if (atlas == null || atlas.BodyPartMappings == null) return "body";

            int idx = wounds.LastHitBodyPartIndex;
            if (idx >= 0 && idx < atlas.BodyPartMappings.Count)
                return atlas.BodyPartMappings[idx].PartName;

            return "body";
        }

        [Serializable]
        private class BarkResponse
        {
            public BarkChoice[] choices;
        }

        [Serializable]
        private class BarkChoice
        {
            public BarkMessage message;
        }

        [Serializable]
        private class BarkMessage
        {
            public string role;
            public string content;
        }
    }
}
