using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SolarHarmony.DynamicWounds2D;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Sabrevois.Gameplay
{
    public class NPCBarkService : IInitializable, ITickable, IDisposable
    {
        private const string Endpoint = "http://localhost:1234/v1/chat/completions";
        private const float CooldownSeconds = 4f;
        private const int MaxResponseTokens = 64;

        private const string MasterDialoguePrompt =
            "You are a character in a fantasy world. " +
            "Respond with a JSON object containing keys \"dialogue\" and \"volume\". " +
            "\"dialogue\" must be ONLY the character's spoken words — pure speech, no narration, " +
            "no action descriptions, no stage directions, no asterisks, no parentheses. " +
            "Maximum 1-2 short sentences. Stay in character. Never break the fourth wall.\n" +
            "\"volume\" is a float 0.0–1.0 matching the dialogue's emotional intensity: " +
            "0.0–0.3 whisper, 0.3–0.5 quiet, 0.5–0.7 normal, 0.7–0.85 raised, 0.85–1.0 shouting.\n" +
            "Example: {\"dialogue\": \"You'll pay for that, you miserable wretch!\", \"volume\": 0.9}";

        private readonly Piper.PiperManager _piperManager;
        private readonly Dictionary<Health, float> _lastBarkTimes = new();
        private readonly Dictionary<Health, float> _lastDamageTimes = new();
        private readonly Dictionary<Health, Queue<string>> _dialogueMemory = new();
        private readonly Dictionary<Health, float> _lastReactionTimes = new();

        private const int MaxMemoryItems = 6;
        private const float HearingRange = 25f;
        private const float ReactionCooldownSeconds = 6f;

        private float _idleBarkTimer;
        private const float IdleBarkInterval = 10f;
        private const float IdleCooldownAfterDamage = 15f;
        private const float HighHealthThreshold = 0.7f;
        private const float LowHealthThreshold = 0.3f;

        public NPCBarkService(Piper.PiperManager piperManager)
        {
            _piperManager = piperManager;
        }

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

        public void Tick()
        {
            _idleBarkTimer -= Time.deltaTime;
            if (_idleBarkTimer > 0f)
                return;

            _idleBarkTimer = IdleBarkInterval;

            TryIdleBark();
        }

        private void TryIdleBark()
        {
            var npcs = WorldObjectRegistry.Instance?.Get(WorldObjectCategory.NPC);
            if (npcs == null || npcs.Count == 0)
                return;

            var eligible = npcs
                .Select(go => go.GetComponent<Health>())
                .Where(h => h != null && !h.IsDead)
                .Where(h => !_lastDamageTimes.TryGetValue(h, out float t)
                            || Time.time - t > IdleCooldownAfterDamage)
                .ToList();

            if (eligible.Count == 0)
                return;

            var health = eligible[Random.Range(0, eligible.Count)];

            if (!TryClaimCooldown(health))
                return;

            var personality = health.BarkPersonality;
            if (personality == null) return;

            string context = !string.IsNullOrWhiteSpace(personality.IdlePrompt)
                ? personality.IdlePrompt
                : "You have a moment of downtime. React in character.";
            string prompt = BuildPrompt(health, context);

            Debug.Log($"[NPCBarkService] Idle bark for {health.name}");
            FireAndForgetBark(health, prompt);
        }

        private async void HandleWoundCreated(WoundsComponent wounds, Wound wound, RaycastHit hit)
        {
            try
            {
                Debug.Log($"[NPCBarkService] Wound created event fired on {wounds?.name}.");

                var health = wounds.GetComponentInParent<Health>();
                if (health == null)
                {
                    Debug.LogWarning("[NPCBarkService] HandleWoundCreated: no Health component found.");
                    return;
                }
                if (!TryClaimCooldown(health))
                {
                    Debug.Log($"[NPCBarkService] Cooldown active for {health.name}.");
                    return;
                }

                _lastDamageTimes[health] = Time.time;

                string bodyPart = ResolveBodyPartName(wounds, wound.Position);
                string severity = wound.Gravity > 0.7f ? "severe" :
                    wound.Gravity > 0.3f ? "moderate" : "light";

                if (health.BarkPersonality == null) return;

                float healthPct = health.HealthPercent;
                var p = health.BarkPersonality;

                string context;
                if (healthPct <= LowHealthThreshold)
                {
                    context = !string.IsNullOrWhiteSpace(p.DesperateBargainPrompt)
                        ? p.DesperateBargainPrompt
                        : $"You are badly wounded. Your health is at {healthPct:P0}. React in character.";
                }
                else if (healthPct >= HighHealthThreshold && severity == "light")
                {
                    context = !string.IsNullOrWhiteSpace(p.AmusedByAttackPrompt)
                        ? p.AmusedByAttackPrompt
                        : $"You took a light hit. Your health is at {healthPct:P0}. React in character.";
                }
                else
                {
                    context = !string.IsNullOrWhiteSpace(p.AngryPrompt)
                        ? p.AngryPrompt
                        : $"You were hit in the {bodyPart}. The wound is {severity}. "
                            + $"Your health is at {healthPct:P0}. React in character.";
                }
                context += $" The {bodyPart} was hit ({severity}).";
                string prompt = BuildPrompt(health, context);

                Debug.Log($"[NPCBarkService] Requesting bark for {health.name}");
                var (result, volume) = await RequestBarkAsync(health.name, health.BarkPersonality, prompt);
                DisplayBark(health, result, volume);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPCBarkService] HandleWoundCreated exception: {e}");
            }
        }

        private async void HandleLimbSevered(WoundsComponent wounds, GameObject severedPart,
            Vector3 hitDirection)
        {
            try
            {
                Debug.Log($"[NPCBarkService] Limb severed event fired on {wounds?.name}.");

                var health = wounds.GetComponentInParent<Health>();
                if (health == null)
                {
                    Debug.LogWarning("[NPCBarkService] HandleLimbSevered: no Health component found.");
                    return;
                }
                if (!TryClaimCooldown(health))
                {
                    Debug.Log($"[NPCBarkService] Cooldown active for {health.name}.");
                    return;
                }

                string bodyPart = ResolveBodyPartName(wounds, Vector2.zero);

                if (health.BarkPersonality == null) return;

                float healthPct = health.HealthPercent;
                var p = health.BarkPersonality;

                string context;
                if (healthPct <= LowHealthThreshold)
                {
                    context = !string.IsNullOrWhiteSpace(p.DesperateBargainPrompt)
                        ? p.DesperateBargainPrompt
                        : $"Your {bodyPart} was severed! Your health is at {healthPct:P0}. React in character.";
                }
                else
                {
                    context = !string.IsNullOrWhiteSpace(p.AngryPrompt)
                        ? p.AngryPrompt
                        : $"Your {bodyPart} was severed! Your health is at {healthPct:P0}. React in character.";
                }
                context += $" The {bodyPart} was severed!";
                string prompt = BuildPrompt(health, context);

                Debug.Log($"[NPCBarkService] Requesting bark for {health.name}");
                var (result, volume) = await RequestBarkAsync(health.name, health.BarkPersonality, prompt);
                DisplayBark(health, result, volume);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPCBarkService] HandleLimbSevered exception: {e}");
            }
        }

        private async void HandleDeath(Health health)
        {
            try
            {
                Debug.Log($"[NPCBarkService] Death event fired on {health?.name}.");

                if (health == null)
                {
                    Debug.LogWarning("[NPCBarkService] HandleDeath: health is null.");
                    return;
                }

                if (health.BarkPersonality == null) return;

                var p = health.BarkPersonality;
                string context;

                if (HeadStillAlive(health))
                {
                    context = !string.IsNullOrWhiteSpace(p.DeathBargainPrompt)
                        ? p.DeathBargainPrompt
                        : "You have been fatally wounded but your head is still alive "
                            + "and you can still speak. React in character.";
                }
                else
                {
                    return;
                }

                string prompt = BuildPrompt(health, context);

                Debug.Log($"[NPCBarkService] Requesting bark for {health.name}");
                var (result, volume) = await RequestBarkAsync(health.name, health.BarkPersonality, prompt);
                DisplayBark(health, result, volume);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPCBarkService] HandleDeath exception: {e}");
            }
        }

        private void FireAndForgetBark(Health health, string prompt)
        {
            RequestBarkAsync(health.name, health.BarkPersonality, prompt)
                .ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(t.Result.dialogue))
                        DisplayBark(health, t.Result.dialogue, t.Result.volume);
                }, TaskContinuationOptions.ExecuteSynchronously);
        }

        private static bool HeadStillAlive(Health health)
        {
            var wounds = health.GetComponentInChildren<WoundsComponent>();
            if (wounds == null || wounds.LiveGraph == null)
                return false;

            int gw = wounds.GraphWidth;
            int gh = wounds.GraphHeight;
            if (gw <= 0 || gh <= 0)
                return false;

            int headStartY = gh - Mathf.Max(1, (int)(gh * 0.2f));
            for (int y = headStartY; y < gh; y++)
            {
                for (int x = 0; x < gw; x++)
                {
                    if (wounds.LiveGraph[y * gw + x])
                        return true;
                }
            }
            return false;
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

        private static async Task<(string dialogue, float volume)> RequestBarkAsync(
            string npcName, BarkPersonality personality, string prompt)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                Debug.Log($"[NPCBarkService] Sending request for {npcName} to {Endpoint}.");

                string systemPrompt = MasterDialoguePrompt;
                if (personality != null)
                    systemPrompt = MasterDialoguePrompt + "\n" + personality.BuildSystemPrompt();

                string json = BuildRequestBody(systemPrompt, prompt, MaxResponseTokens, 0.8f);

                using var www = UnityWebRequest.Post(Endpoint, json, "application/json");
                www.timeout = 5;

                var tcs = new TaskCompletionSource<(string dialogue, float volume)>();
                www.SendWebRequest().completed += _ =>
                {
                    try
                    {
                        if (www.result != UnityWebRequest.Result.Success)
                        {
                            sw.Stop();
                            var reason = www.result == UnityWebRequest.Result.ConnectionError
                                && www.error?.Contains("timed") == true
                                ? "TIMEOUT" : www.error;
                            Debug.LogWarning(
                                $"[NPCBarkService] Request failed for {npcName} " +
                                $"after {sw.Elapsed.TotalSeconds:F1}s ({www.result}): {reason}");
                            tcs.TrySetResult((null, 0.7f));
                            return;
                        }

                        Debug.Log(
                            $"[NPCBarkService] Response received for {npcName} " +
                            $"after {sw.Elapsed.TotalSeconds:F1}s.");

                        string rawText = www.downloadHandler?.text ?? string.Empty;

                        JObject root;
                        try { root = JObject.Parse(rawText); }
                        catch (Exception ex)
                        {
                            Debug.LogWarning(
                                $"[NPCBarkService] JObject.Parse failed for {npcName}: {ex.Message}");
                            tcs.TrySetResult((null, 0.7f));
                            return;
                        }

                        string content = (string)root?["choices"]?[0]?["message"]?["content"];
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            var (dialogue, volume) = ExtractDialogue(content);
                            if (string.IsNullOrWhiteSpace(dialogue))
                                dialogue = null;

                            sw.Stop();
                            Debug.Log(
                                $"[NPCBarkService] {npcName} bark: \"{Truncate(dialogue, 80)}\" " +
                                $"(vol={volume:F2}, {sw.Elapsed.TotalMilliseconds:F0}ms total).");
                            tcs.TrySetResult((dialogue, volume));
                        }
                        else
                        {
                            sw.Stop();
                            Debug.LogWarning(
                                $"[NPCBarkService] {npcName} bark: no content field found " +
                                $"after {sw.Elapsed.TotalSeconds:F1}s.");
                            tcs.TrySetResult((null, 0.7f));
                        }
                    }
                    catch (Exception e)
                    {
                        sw.Stop();
                        Debug.LogError(
                            $"[NPCBarkService] Exception in completed callback for {npcName}: {e}");
                        tcs.TrySetResult((null, 0.7f));
                    }
                };

                return await tcs.Task;
            }
            catch (Exception e)
            {
                sw.Stop();
                Debug.LogError(
                    $"[NPCBarkService] Exception sending request for {npcName} " +
                    $"after {sw.Elapsed.TotalMilliseconds:F0}ms: {e}");
                return (null, 0.7f);
            }
        }

        private void DisplayBark(Health health, string bark, float volume)
        {
            try
            {
                if (health == null || string.IsNullOrWhiteSpace(bark)) return;

                Debug.Log($"[Bark:{health.name}] {bark} (vol={volume:F2})");

                var display = health.GetComponentInChildren<BarkDisplay>();
                if (display == null)
                {
                    Debug.LogWarning($"[NPCBarkService] No BarkDisplay found on {health.name}.");
                    return;
                }

                display.Setup(_piperManager);
                display.Speak(bark, volume);
                Debug.Log($"[NPCBarkService] TTS triggered for {health.name}.");

                RecordDialogue(health, bark);
                BroadcastBark(health, bark);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPCBarkService] DisplayBark exception for {health?.name}: {e}");
            }
        }

        private void RecordDialogue(Health health, string bark)
        {
            if (!_dialogueMemory.TryGetValue(health, out var queue))
            {
                queue = new Queue<string>(MaxMemoryItems);
                _dialogueMemory[health] = queue;
            }

            queue.Enqueue(bark);
            while (queue.Count > MaxMemoryItems)
                queue.Dequeue();
        }

        private void BroadcastBark(Health source, string bark)
        {
            var npcs = WorldObjectRegistry.Instance?.Get(WorldObjectCategory.NPC);
            if (npcs == null || npcs.Count == 0) return;

            var sourcePos = source.transform.position;

            foreach (var go in npcs)
            {
                if (go == null) continue;

                var health = go.GetComponent<Health>();
                if (health == null || health == source || health.IsDead) continue;

                float dist = Vector3.Distance(sourcePos, health.transform.position);
                if (dist > HearingRange) continue;

                if (!_lastReactionTimes.TryGetValue(health, out float lastTime))
                    lastTime = 0f;
                if (Time.time - lastTime < ReactionCooldownSeconds) continue;

                var personality = health.BarkPersonality;
                if (personality == null) continue;

                float chance = 0.3f
                    * ((personality.Aggression + personality.Verbosity + personality.Cruelty) / 3f + 0.5f);
                if (UnityEngine.Random.value > chance) continue;

                _lastReactionTimes[health] = Time.time;

                string prompt = $"You overheard someone nearby say: \"{bark}\". "
                    + "React in character. Keep it very short.";

                FireAndForgetBark(health, prompt);
            }
        }

        private string BuildMemoryContext(Health health)
        {
            if (!_dialogueMemory.TryGetValue(health, out var queue) || queue.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("You recently said:");
            foreach (var line in queue)
                sb.AppendLine($"- \"{line}\"");
            sb.AppendLine("Avoid repeating yourself.");
            return sb.ToString();
        }

        private string BuildPrompt(Health health, string eventContext)
        {
            string memory = BuildMemoryContext(health);
            if (string.IsNullOrEmpty(memory))
                return eventContext;
            return memory + "\n" + eventContext;
        }

        private static string BuildRequestBody(
            string systemPrompt, string userPrompt, int maxTokens, float temperature)
        {
            var messages = new JArray();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new JObject
                {
                    ["role"] = "system",
                    ["content"] = systemPrompt
                });
            }

            messages.Add(new JObject
            {
                ["role"] = "user",
                ["content"] = userPrompt
            });

            var body = new JObject
            {
                ["messages"] = messages,
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature
            };

            return body.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static (string dialogue, float volume) ExtractDialogue(string text)
        {
            if (string.IsNullOrEmpty(text)) return (null, 0.7f);

            text = Regex.Replace(text, @"^```(?:json)?\s*", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\s*```$", "");

            JObject json = TryParseJson(text);
            if (json != null)
            {
                string dialogue = (string)json["dialogue"];
                if (!string.IsNullOrWhiteSpace(dialogue))
                {
                    float vol = json["volume"]?.Value<float>() ?? 0.7f;
                    if (vol <= 0.001f)
                        vol = 0.7f;
                    vol = Mathf.Clamp(vol, 0f, 1f);
                    return (dialogue.Trim(), vol);
                }
            }

            text = Regex.Replace(text, @"\*[^*]*\*", "");
            text = Regex.Replace(text, @"[""""]", "");
            text = Regex.Replace(text, @"\([^)]*\)", "");
            text = Regex.Replace(text, @"\s+", " ").Trim();

            if (string.IsNullOrWhiteSpace(text))
                return (null, 0.7f);

            if (text.StartsWith("{") || text.StartsWith("dialogue") || text.StartsWith("\""))
                return (null, 0.7f);

            return (text, 0.7f);
        }

        private static JObject TryParseJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            try { return JObject.Parse(text); }
            catch { }

            string sanitized = Regex.Replace(text, @"\b(dialogue|volume)\b(?=\s*:)", "\"$1\"",
                RegexOptions.IgnoreCase);

            try { return JObject.Parse(sanitized); }
            catch { }

            int lastComplete = text.LastIndexOf('}');
            if (lastComplete >= 0)
            {
                try { return JObject.Parse(text[..(lastComplete + 1)]); }
                catch { }
            }

            if (!text.EndsWith("}"))
            {
                try { return JObject.Parse(text + "\"}"); }
                catch { }
                try { return JObject.Parse(text + "}"); }
                catch { }
            }

            return null;
        }

        private static string ResolveBodyPartName(WoundsComponent wounds, Vector2 uv)
        {
            if (wounds == null) return "body";
            var atlas = wounds.AtlasData;
            if (atlas == null || atlas.BodyPartMappings == null) return "body";

            int idx = wounds.LastHitBodyPartIndex;
            if (idx >= 0 && idx < atlas.BodyPartMappings.Count)
            {
                var name = atlas.BodyPartMappings[idx].Name;
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }

            return "body";
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxLen) return s;
            return s[..maxLen] + "...";
        }
    }
}
