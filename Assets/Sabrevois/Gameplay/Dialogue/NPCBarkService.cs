using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Sabrevois.Gameplay.AI;
using Sabrevois.Gameplay.Input;
using SolarHarmony.DynamicWounds2D;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Sabrevois.Gameplay
{
    /// <summary>
    /// Drives NPC barks through a small per-NPC mood state machine.
    ///
    /// Events that matter: seeing a character for the first time, being damaged (with a
    /// severity level and the body part that was hit), losing a limb, dying, and overhearing
    /// another bark. Anger persists after a hit, so a wounded NPC keeps using angry idle
    /// lines long after the fight, and it is published on <see cref="Health"/> for the
    /// utility AI (<see cref="Sabrevois.Gameplay.AI.DataSources.AngerDataSource"/> /
    /// <see cref="Sabrevois.Gameplay.AI.DataSources.FearDataSource"/>).
    ///
    /// Lines come from a local LLM when it is reachable and from authored fallback lines
    /// otherwise, so the NPCs always talk.
    /// </summary>
    public class NPCBarkService : IInitializable, ITickable, IDisposable
    {
        // RAON-1 served by the KRAFTON llama.cpp fork listens on :8000. The address comes
        // from the LocalModelSettings game config, and SABREVOIS_LLM_ENDPOINT still wins
        // so the game can point at LM Studio (:1234) or any other OpenAI-compatible server.
        private const string DefaultEndpoint = "http://127.0.0.1:8000/v1/chat/completions";
        private const int MaxResponseTokens = 80;

        [Inject(Optional = true)] private LocalModelSettings _modelSettings;

        private string _endpoint;

        private string Endpoint => _endpoint ??= ResolveEndpoint();

        private string ResolveEndpoint()
        {
            string custom = Environment.GetEnvironmentVariable("SABREVOIS_LLM_ENDPOINT");
            if (!string.IsNullOrWhiteSpace(custom)) return custom.Trim();

            string baseUrl = _modelSettings?.GetModel<RaonModel>()?.BaseUrl;
            return string.IsNullOrWhiteSpace(baseUrl)
                ? DefaultEndpoint
                : baseUrl.TrimEnd('/') + "/v1/chat/completions";
        }

        // Hard deadline for a single attempt. UnityWebRequest.timeout does not fire for
        // every kind of stalled connect, so Tick() also aborts anything past this budget.
        private const float RequestTimeoutSeconds = 6f;

        // After this many consecutive failures the endpoint is treated as offline and
        // local fallback lines are used (no HTTP at all) until UnavailableSeconds passes.
        private const int FailureThreshold = 2;
        private const float UnavailableSeconds = 60f;

        private const float CooldownSeconds = 4f;
        private const float IdleBarkInterval = 8f;
        private const float IdleCooldownAfterDamage = 6f;
        private const float PerceptionInterval = 0.5f;
        private const float SightRange = 18f;
        private const float SightEyeHeight = 1.2f;
        private const float SightBarkCooldown = 25f;
        private const float HearingRange = 25f;
        private const float ReactionCooldownSeconds = 6f;
        private const float CombatMemorySeconds = 8f;
        private const float AwarenessRadius = 20f;
        private const float ChatRange = 12f;
        private const int MaxChatTokens = 160;
        private const int MaxChatHistory = 12;
        private const float ConversationWindowSeconds = 90f;
        private const int MaxMemoryItems = 6;

        // Barks are on by default. SABREVOIS_BARKS=0 turns the automatic idle / sight /
        // damage barks off and leaves only the chat UI.
        private static readonly bool AutomaticBarksEnabled = ResolveAutomaticBarks();

        private static bool ResolveAutomaticBarks()
        {
            string value = Environment.GetEnvironmentVariable("SABREVOIS_BARKS");
            return string.IsNullOrEmpty(value) || value != "0";
        }

        private static readonly WorldObjectCategory[] ObjectCategories =
        {
            WorldObjectCategory.Tree,
            WorldObjectCategory.House,
            WorldObjectCategory.Food
        };

        private static readonly BarkContext EmptyContext = new();

        private const string MasterDialoguePrompt =
            "You are a character in a fantasy world. " +
            "Respond with a JSON object containing keys \"dialogue\" and \"volume\". " +
            "\"dialogue\" must be ONLY the character's spoken words — pure speech, no narration, " +
            "no action descriptions, no stage directions, no asterisks, no parentheses. " +
            "Maximum 1-2 short sentences. Stay in character and match the mood you are given. " +
            "If a body part is mentioned, you may react to it. Never break the fourth wall.\n" +
            "\"volume\" is a float 0.0–1.0 matching the dialogue's emotional intensity: " +
            "0.0–0.3 whisper, 0.3–0.5 quiet, 0.5–0.7 normal, 0.7–0.85 raised, 0.85–1.0 shouting.\n" +
            "Example: {\"dialogue\": \"You'll pay for that, you miserable wretch!\", \"volume\": 0.9}";

        private readonly Piper.PiperManager _piperManager;
        private readonly NpcReasoningService _reasoning;
        private readonly BarkSpeechScheduler _speech;
        private readonly Dictionary<Health, NpcBarkStateMachine> _machines = new();
        private readonly Dictionary<Health, float> _lastBarkTimes = new();
        private readonly Dictionary<Health, float> _lastDamageTimes = new();
        private readonly Dictionary<Health, float> _lastSightBarkTimes = new();
        private readonly Dictionary<Health, Transform> _lastSeenTargets = new();
        private readonly Dictionary<Health, Queue<string>> _dialogueMemory = new();
        private readonly Dictionary<Health, float> _lastReactionTimes = new();
        private readonly Dictionary<Health, BarkContext> _contexts = new();
        private readonly Dictionary<Health, List<JObject>> _chatHistory = new();
        private readonly Dictionary<Health, float> _conversationUntil = new();
        private readonly Dictionary<Health, string> _lastDecision = new();
        private readonly List<ActiveRequest> _activeRequests = new();

        private readonly StringBuilder _contextBuilder = new();

        private int _sightBlockMask = ~0;
        private float _nextPerceptionTime;
        private float _idleBarkTimer;
        private int _consecutiveFailures;
        private float _retryAfterTime;

        private Transform _player;
        private float _nextPlayerLookup;

        private sealed class ActiveRequest
        {
            public UnityWebRequest Request;
            public float StartedAt;
        }

        public NPCBarkService(Piper.PiperManager piperManager, NpcReasoningService reasoning,
            BarkSpeechScheduler speech)
        {
            _piperManager = piperManager;
            _reasoning = reasoning;
            _speech = speech;
        }

        public void Initialize()
        {
            int ignore = 0;
            foreach (string layerName in new[] { "Character", "UI", "Ignore Raycast" })
            {
                int layer = LayerMask.NameToLayer(layerName);
                if (layer >= 0) ignore |= 1 << layer;
            }
            _sightBlockMask = ~ignore;

            WoundsComponent.OnAnyLimbSevered += HandleLimbSevered;
            Health.OnAnyDamageTaken += HandleDamageTaken;
            Health.OnAnyDeath += HandleDeath;
        }

        public void Dispose()
        {
            WoundsComponent.OnAnyLimbSevered -= HandleLimbSevered;
            Health.OnAnyDamageTaken -= HandleDamageTaken;
            Health.OnAnyDeath -= HandleDeath;

            foreach (var active in _activeRequests)
                active.Request?.Abort();
            _activeRequests.Clear();
        }

        public void Tick()
        {
            AbortExpiredRequests();

            if (Time.unscaledTime >= _nextPerceptionTime)
            {
                _nextPerceptionTime = Time.unscaledTime + PerceptionInterval;
                UpdatePerception();
            }

            if (!AutomaticBarksEnabled) return;

            _idleBarkTimer -= Time.unscaledDeltaTime;
            if (_idleBarkTimer > 0f)
                return;

            _idleBarkTimer = IdleBarkInterval;
            TryIdleBark();
        }

        // ---------------------------------------------------------------- perception

        /// <summary>
        /// Samples line of sight and health for every living NPC, pushes the result into its
        /// state machine, and publishes the mood for the utility AI. Runs a few times a second.
        /// </summary>
        private void UpdatePerception()
        {
            var npcs = WorldObjectRegistry.Instance?.Get(WorldObjectCategory.NPC);
            if (npcs == null || npcs.Count == 0) return;

            var characters = UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None);

            for (int i = 0; i < npcs.Count; i++)
            {
                var go = npcs[i];
                if (go == null) continue;

                var health = go.GetComponent<Health>();
                if (health == null || health.IsDead) continue;

                var machine = GetMachine(health);
                var personality = health.BarkPersonality;

                Transform seen = FindVisibleCharacter(health, characters);
                _contexts[health] = BuildContext(health, seen);

                machine.Observe(PerceptionInterval, health.HealthPercent, ResolveThreat(health, seen), personality);
                PublishMood(health, machine);
                ClearStaleOpponent(health, seen);

                HandleSighting(health, machine, seen, personality);
            }
        }

        // ---------------------------------------------------------------- awareness

        /// <summary>
        /// Caches what the NPC can perceive right now: the character it is reacting to, the
        /// player, and the nearest registered world object (tree / house / food).
        /// </summary>
        private BarkContext BuildContext(Health health, Transform seen)
        {
            var context = new BarkContext();
            Vector3 origin = health.transform.position;
            Transform player = ResolvePlayer();

            if (seen != null)
            {
                context.Target = seen;
                context.TargetLabel = player != null && seen == player ? "traveller" : seen.name;
            }

            _contextBuilder.Clear();

            if (player != null)
            {
                float distance = Vector3.Distance(origin, player.position);
                if (distance <= AwarenessRadius)
                    AppendNearby(_contextBuilder, "the traveller", distance);
            }

            GameObject nearestObject = FindNearestObject(origin, out float objectDistance, out string objectLabel);
            if (nearestObject != null)
            {
                context.ObjectLabel = objectLabel;
                context.ObjectDistance = objectDistance;
                AppendNearby(_contextBuilder, $"a {objectLabel}", objectDistance);
            }

            if (seen != null && (player == null || seen != player))
                AppendNearby(_contextBuilder, seen.name, Vector3.Distance(origin, seen.position));

            context.NearbyDescription = _contextBuilder.ToString();
            return context;
        }

        private Transform ResolvePlayer()
        {
            if (_player != null) return _player;

            if (Time.unscaledTime >= _nextPlayerLookup)
            {
                _nextPlayerLookup = Time.unscaledTime + 2f;
                var move = UnityEngine.Object.FindFirstObjectByType<MoveController>();
                _player = move != null ? move.transform : null;
            }

            return _player;
        }

        private static GameObject FindNearestObject(Vector3 origin, out float distance,
            out string label)
        {
            distance = AwarenessRadius;
            label = null;
            GameObject best = null;

            var registry = WorldObjectRegistry.Instance;
            if (registry == null) return null;

            for (int c = 0; c < ObjectCategories.Length; c++)
            {
                WorldObjectCategory category = ObjectCategories[c];
                var objects = registry.Get(category);
                if (objects == null) continue;

                for (int i = 0; i < objects.Count; i++)
                {
                    var go = objects[i];
                    if (go == null || !go.activeInHierarchy) continue;

                    float sqr = (go.transform.position - origin).sqrMagnitude;
                    if (sqr >= distance * distance) continue;

                    distance = Mathf.Sqrt(sqr);
                    best = go;
                    label = LabelFor(category);
                }
            }

            return best;
        }

        private static void AppendNearby(System.Text.StringBuilder sb, string label, float distance)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(label);
            sb.Append(" (");
            sb.Append(distance.ToString("F0"));
            sb.Append("m)");
        }

        private static string LabelFor(WorldObjectCategory category) => category switch
        {
            WorldObjectCategory.Tree => "tree",
            WorldObjectCategory.House => "house",
            _ => "food"
        };

        private BarkContext GetContext(Health health)
        {
            return _contexts.TryGetValue(health, out var context) ? context : EmptyContext;
        }

        private Transform ResolveThreat(Health health, Transform seen)
        {
            if (seen != null) return seen;

            var opponent = health.GetComponent<OpponentComponent>();
            if (opponent == null || opponent.CurrentOpponent == null)
                return null;

            // Keep considering the attacker a threat for a while after a hit, but let it
            // expire — otherwise the NPC can never calm down and never drops the opponent.
            if (_lastDamageTimes.TryGetValue(health, out float last) &&
                Time.unscaledTime - last < CombatMemorySeconds)
                return opponent.CurrentOpponent;

            return null;
        }

        /// <summary>
        /// Combat memory is short (a few seconds), while anger lingers for barks. Drop the
        /// opponent once the fight is actually over so the AI stops chasing forever.
        /// </summary>
        private void ClearStaleOpponent(Health health, Transform seen)
        {
            var opponent = health.GetComponent<OpponentComponent>();
            if (opponent == null || opponent.CurrentOpponent == null) return;
            if (opponent.CurrentOpponent == seen) return;

            bool recentCombat = _lastDamageTimes.TryGetValue(health, out float last) &&
                                Time.unscaledTime - last < CombatMemorySeconds;
            if (!recentCombat)
                opponent.CurrentOpponent = null;
        }

        private Transform FindVisibleCharacter(Health self, Health[] characters)
        {
            Vector3 origin = self.transform.position + Vector3.up * SightEyeHeight;
            Transform best = null;
            float bestSqr = SightRange * SightRange;

            for (int i = 0; i < characters.Length; i++)
            {
                var other = characters[i];
                if (other == null || other == self || other.IsDead) continue;

                Vector3 target = other.transform.position + Vector3.up * SightEyeHeight;
                float sqr = (target - origin).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                if (Physics.Linecast(origin, target, _sightBlockMask, QueryTriggerInteraction.Ignore))
                    continue;

                bestSqr = sqr;
                best = other.transform;
            }

            return best;
        }

        private async void HandleSighting(Health health, NpcBarkStateMachine machine, Transform seen,
            BarkPersonality personality)
        {
            if (seen == null)
            {
                _lastSeenTargets.Remove(health);
                return;
            }

            if (_lastSeenTargets.TryGetValue(health, out var previous) && previous == seen)
                return;

            _lastSeenTargets[health] = seen;

            if (_lastSightBarkTimes.TryGetValue(health, out float last) &&
                Time.unscaledTime - last < SightBarkCooldown)
                return;

            _lastSightBarkTimes[health] = Time.unscaledTime;

            machine.OnSighted(seen, personality);
            PublishMood(health, machine);

            if (!AutomaticBarksEnabled || InConversation(health)) return;

            BarkContext context = GetContext(health);
            string target = context.HasTarget ? context.TargetLabel : seen.name;

            await DecideIntentAsync(health, machine, $"You just noticed {target} nearby.");

            string sighting = $"You just noticed {target} nearby.";
            FireAndForgetBark(health, BuildPrompt(health, machine, sighting), BarkSituation.Sight);
        }

        // ---------------------------------------------------------------- events

        private async void HandleDamageTaken(Health health, float depth)
        {
            try
            {
                if (health == null || health.IsDead || !IsNpc(health)) return;

                var machine = GetMachine(health);
                var personality = health.BarkPersonality;

                DamageLevel level = ClassifyDamage(depth, health.HealthPercent);
                string bodyPart = ResolveBodyPartName(health, depth);

                machine.OnDamaged(level, health.HealthPercent, personality);
                PublishMood(health, machine);

                _lastDamageTimes[health] = Time.unscaledTime;

                if (!TryClaimCooldown(health)) return;

                BarkContext barkContext = GetContext(health);
                string attacker = barkContext.HasTarget ? $" by {barkContext.TargetLabel}" : "";
                await DecideIntentAsync(health, machine,
                    $"You were just hit{attacker} on your {bodyPart} ({level}).");

                if (!AutomaticBarksEnabled) return;

                string context = DescribeDamage(machine, level, depth, bodyPart, health, barkContext);
                BarkSituation situation = NpcBarkStateMachine.SituationFor(level);

                Debug.Log($"[NPCBarkService] {health.name}: {level} hit on {bodyPart} (depth {depth:F2}) -> {situation}.");

                var (dialogue, volume) = await RequestBarkAsync(health, BuildPrompt(health, machine, context), situation, bodyPart);
                DisplayBark(health, dialogue, volume, situation);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPCBarkService] HandleDamageTaken exception: {e}");
            }
        }

        private async void HandleLimbSevered(WoundsComponent wounds, GameObject severedPart,
            Vector3 hitDirection)
        {
            try
            {
                if (wounds == null) return;

                var health = wounds.GetComponentInParent<Health>();
                if (health == null || health.IsDead || !IsNpc(health)) return;

                var machine = GetMachine(health);
                var personality = health.BarkPersonality;

                machine.OnLimbSevered(personality);
                PublishMood(health, machine);
                _lastDamageTimes[health] = Time.unscaledTime;

                if (!TryClaimCooldown(health)) return;

                string bodyPart = ResolveBodyPartName(health, 0f);

                BarkContext barkContext = GetContext(health);
                string attacker = barkContext.HasTarget ? $" by {barkContext.TargetLabel}" : "";
                await DecideIntentAsync(health, machine,
                    $"Your {bodyPart} was just severed{attacker}.");

                if (!AutomaticBarksEnabled) return;

                string intent = machine.Intent == NpcBarkIntent.Retreat
                    ? "You want to get away from them."
                    : "You want to make them pay.";
                string context = $"Your {bodyPart} was just severed. Your health is at {health.HealthPercent:P0}. {intent}";

                Debug.Log($"[NPCBarkService] {health.name}: {bodyPart} severed.");

                var (dialogue, volume) = await RequestBarkAsync(health, BuildPrompt(health, machine, context), BarkSituation.Severed, bodyPart);
                DisplayBark(health, dialogue, volume, BarkSituation.Severed);
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
                if (health == null || !IsNpc(health)) return;

                var machine = GetMachine(health);
                machine.Kill();
                PublishMood(health, machine);

                if (!AutomaticBarksEnabled)
                {
                    Forget(health);
                    return;
                }

                // A killing blow already used the shared cooldown; the death line always plays.
                if (HeadStillAlive(health))
                {
                    _lastBarkTimes[health] = Time.unscaledTime;

                    string context = "You have been fatally wounded but your head is still alive "
                                     + "and you can still speak. Offer a deal.";
                    var (dialogue, volume) = await RequestBarkAsync(health, BuildPrompt(health, machine, context), BarkSituation.Death, null);
                    DisplayBark(health, dialogue, volume, BarkSituation.Death);
                }

                Forget(health);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPCBarkService] HandleDeath exception: {e}");
            }
        }

        // ---------------------------------------------------------------- idle chatter

        private async void TryIdleBark()
        {
            var npcs = WorldObjectRegistry.Instance?.Get(WorldObjectCategory.NPC);
            if (npcs == null || npcs.Count == 0) return;

            // Two-pass reservoir sampling: prefer NPCs that actually have a mood to voice
            // (angry/wounded), otherwise fall back to any eligible NPC. Allocation-free.
            Health moodNpc = null;
            int moodCount = 0;
            Health anyNpc = null;
            int anyCount = 0;

            for (int i = 0; i < npcs.Count; i++)
            {
                var go = npcs[i];
                if (go == null) continue;

                var candidate = go.GetComponent<Health>();
                if (candidate == null || candidate.IsDead) continue;
                if (_lastDamageTimes.TryGetValue(candidate, out float last) &&
                    Time.unscaledTime - last < IdleCooldownAfterDamage)
                    continue;

                // Don't interrupt an open conversation with idle chatter.
                if (InConversation(candidate)) continue;

                anyCount++;
                if (Random.Range(0, anyCount) == 0)
                    anyNpc = candidate;

                NpcBarkState mood = GetMachine(candidate).State;
                if (mood is NpcBarkState.Calm or NpcBarkState.Alert)
                    continue;

                moodCount++;
                if (Random.Range(0, moodCount) == 0)
                    moodNpc = candidate;
            }

            Health health = moodNpc != null ? moodNpc : anyNpc;

            if (health == null || !TryClaimCooldown(health)) return;

            var machine = GetMachine(health);
            BarkSituation situation = machine.IdleSituation;

            // The reasoning model decides whether this NPC actually speaks up; the text
            // model only phrases the line afterwards.
            if (_reasoning != null && _reasoning.Available)
            {
                try
                {
                    string state = BuildReasoningState(health, machine,
                        "You have a quiet moment.",
                        "Do you say something out loud right now?");

                    bool? speak = await _reasoning.AskAsync(state, "speak",
                        "Say something out loud.",
                        "Stay silent.");

                    if (speak == false) return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[NPCBarkService] Speak decision failed: {e.Message}");
                }
            }

            string context = situation switch
            {
                BarkSituation.AngryIdle =>
                    "You are still furious at whoever hit you. Grumble or threaten under your breath.",
                BarkSituation.HurtModerate =>
                    "You are wounded and it hurts. Complain or tend to yourself.",
                BarkSituation.Desperate =>
                    "You are badly hurt and may not survive. React to your condition.",
                _ =>
                    "You have a moment of downtime."
            };

            Debug.Log($"[NPCBarkService] Idle bark for {health.name} ({machine.State}).");
            FireAndForgetBark(health, BuildPrompt(health, machine, context), situation);
        }

        private async void FireAndForgetBark(Health health, string prompt, BarkSituation situation,
            string bodyPart = null)
        {
            try
            {
                var (dialogue, volume) = await RequestBarkAsync(health, prompt, situation, bodyPart);
                if (!string.IsNullOrWhiteSpace(dialogue))
                    DisplayBark(health, dialogue, volume, situation);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPCBarkService] FireAndForgetBark exception: {e}");
            }
        }

        // ---------------------------------------------------------------- mood sharing

        private NpcBarkStateMachine GetMachine(Health health)
        {
            if (_machines.TryGetValue(health, out var machine))
                return machine;

            machine = new NpcBarkStateMachine();
            machine.StateEntered += state => OnMoodChanged(health, state);
            _machines[health] = machine;
            return machine;
        }

        /// <summary>Reacts to mood transitions by nudging the utility AI's shared state.</summary>
        private void OnMoodChanged(Health health, NpcBarkState state)
        {
            if (health == null) return;

            Debug.Log($"[NPCBarkService] {health.name}: mood -> {state}");

            var opponent = health.GetComponent<OpponentComponent>();
            var conversation = health.GetComponent<ConversationComponent>();

            switch (state)
            {
                case NpcBarkState.Calm:
                    // Fight is over: drop the stale opponent, otherwise the utility AI keeps
                    // attacking/fleeing forever because HasOpponentDataSource stays at 1.
                    if (opponent != null) opponent.CurrentOpponent = null;
                    break;

                case NpcBarkState.Angry:
                    // Too angry to keep chatting.
                    if (conversation != null) conversation.ConversationPartner = null;
                    break;
            }
        }

        private static void PublishMood(Health health, NpcBarkStateMachine machine)
        {
            health.Anger = machine.Anger;
            health.Fear = machine.Fear;
            health.BarkMood = machine.State;
        }

        // ---------------------------------------------------------------- decisions

        /// <summary>True while the player's conversation with this NPC is still open.</summary>
        private bool InConversation(Health health) =>
            _conversationUntil.TryGetValue(health, out float until) && Time.unscaledTime < until;

        private void MarkConversation(Health health) =>
            _conversationUntil[health] = Time.unscaledTime + ConversationWindowSeconds;

        /// <summary>
        /// Everything the NPC knows about itself right now: body, needs, current action,
        /// opponent, conversation and surroundings. Shared by the chat prompt and the decision
        /// model so both reason from the same facts.
        /// </summary>
        private string BuildNpcStatus(Health health, NpcBarkStateMachine machine)
        {
            var sb = new StringBuilder();

            sb.Append($"Body: health {health.HealthPercent:P0}");
            if (health.IsDead) sb.Append(" (dead)");

            var wounds = health.GetComponentInChildren<WoundsComponent>();
            if (wounds != null && wounds.VisibleHeightFraction < 0.99f)
                sb.Append($", body integrity {wounds.VisibleHeightFraction:P0}");

            var energy = health.GetComponent<Energy>();
            if (energy != null) sb.Append($", energy {energy.CurrentEnergy01:P0}");

            var hunger = health.GetComponent<Hunger>();
            if (hunger != null) sb.Append($", hunger {hunger.CurrentHunger01:P0}");

            var opponent = health.GetComponent<OpponentComponent>();
            if (opponent != null && opponent.CurrentOpponent != null)
                sb.Append($". Facing: {opponent.CurrentOpponent.name}");

            var conversation = health.GetComponent<ConversationComponent>();
            if (conversation != null && conversation.ConversationPartner != null)
                sb.Append($". Talking with: {conversation.ConversationPartner.name}");

            var agent = health.GetComponent<Sabrevois.AI.Agent>();
            if (agent != null && agent.CurrentAction != null)
                sb.Append($". Doing: {ActionName(agent.CurrentAction)}");

            sb.Append('.');

            BarkContext context = GetContext(health);
            if (context.NearbyDescription.Length > 0)
                sb.Append($" Nearby: {context.NearbyDescription}.");

            return sb.ToString();
        }

        private static string ActionName(object action)
        {
            string name = action?.GetType().Name ?? "idle";
            return name.EndsWith("Action") ? name[..^"Action".Length] : name;
        }

        private static string IntentWord(NpcBarkIntent intent) => intent switch
        {
            NpcBarkIntent.Confront => "confront them",
            NpcBarkIntent.Retreat => "get away",
            _ => "hold position"
        };

        /// <summary>
        /// Compact second-person scenario for the decision model, ending with the question it
        /// should answer. The model picks a letter; it never writes a line.
        /// </summary>
        private string BuildReasoningState(Health health, NpcBarkStateMachine machine,
            string eventText, string question)
        {
            var personality = health.BarkPersonality;

            var sb = new StringBuilder();
            sb.Append($"You are {health.name}, {DescribeTraits(personality)}. ");
            sb.Append($"Your mood is {MoodWord(machine.State)} (anger {machine.Anger:P0}, fear {machine.Fear:P0}), ");
            sb.Append($"and your intent is to {IntentWord(machine.Intent)}. ");
            sb.Append(BuildNpcStatus(health, machine));
            sb.Append(' ').Append(eventText);
            sb.Append(' ').Append(question);

            return sb.ToString();
        }

        private static string DescribeTraits(BarkPersonality personality)
        {
            if (personality == null) return "an ordinary person";

            var sb = new StringBuilder();
            sb.Append(TraitWord(personality.Aggression, "a pacifist", "mild", "aggressive", "very aggressive"));
            sb.Append(", ").Append(TraitWord(personality.Pride, "humble", "modest", "proud", "arrogant"));
            sb.Append(", ").Append(TraitWord(personality.Cowardice, "fearless", "steady", "cowardly", "terrified of danger"));
            sb.Append(", ").Append(TraitWord(personality.Cruelty, "merciful", "fair", "cruel", "sadistic"));
            return sb.ToString();
        }

        private static string TraitWord(float value, string low, string midLow, string midHigh, string high)
        {
            if (value < 0.25f) return low;
            if (value < 0.5f) return midLow;
            if (value < 0.75f) return midHigh;
            return high;
        }

        private static string MoodWord(NpcBarkState state) => state switch
        {
            NpcBarkState.Angry => "furious",
            NpcBarkState.Wounded => "wounded and in pain",
            NpcBarkState.Critical => "dying",
            NpcBarkState.Alert => "wary",
            NpcBarkState.Dead => "dead",
            _ => "calm"
        };

        /// <summary>Fight / flee / hold, decided by the reasoning model and kept until Calm.</summary>
        private async Task DecideIntentAsync(Health health, NpcBarkStateMachine machine, string eventText)
        {
            if (_reasoning == null || !_reasoning.Available) return;

            try
            {
                string state = BuildReasoningState(health, machine, eventText, "What do you do?");

                string letter = await _reasoning.ChooseAsync(state, "intent",
                    ("A", "Attack them and make them pay."),
                    ("B", "Retreat and get away."),
                    ("C", "Hold position and wait."));

                if (letter == null) return;

                machine.ApplyDecision(letter switch
                {
                    "A" => NpcBarkIntent.Confront,
                    "B" => NpcBarkIntent.Retreat,
                    _ => NpcBarkIntent.None
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NPCBarkService] Intent decision failed: {e.Message}");
            }
        }

        private void Forget(Health health)
        {
            _machines.Remove(health);
            _lastBarkTimes.Remove(health);
            _lastDamageTimes.Remove(health);
            _lastSightBarkTimes.Remove(health);
            _lastSeenTargets.Remove(health);
            _dialogueMemory.Remove(health);
            _lastReactionTimes.Remove(health);
            _contexts.Remove(health);
            _chatHistory.Remove(health);
            _conversationUntil.Remove(health);
            _lastDecision.Remove(health);
        }

        /// <summary>The player also has a Health, but only registered NPCs talk.</summary>
        private static bool IsNpc(Health health)
        {
            var npcs = WorldObjectRegistry.Instance?.Get(WorldObjectCategory.NPC);
            return npcs != null && npcs.Contains(health.gameObject);
        }

        private bool TryClaimCooldown(Health health)
        {
            if (_lastBarkTimes.TryGetValue(health, out float lastTime) &&
                Time.unscaledTime - lastTime < CooldownSeconds)
                return false;

            _lastBarkTimes[health] = Time.unscaledTime;
            return true;
        }

        // ---------------------------------------------------------------- prompting

        private string BuildPrompt(Health health, NpcBarkStateMachine machine, string eventContext)
        {
            var sb = new StringBuilder();

            string memory = BuildMemoryContext(health);
            if (!string.IsNullOrEmpty(memory))
                sb.AppendLine(memory);

            sb.AppendLine($"Mood: {machine.State}. Anger {machine.Anger:P0}, fear {machine.Fear:P0}.");

            if (machine.Intent == NpcBarkIntent.Confront) sb.Append("You want to confront them. ");
            else if (machine.Intent == NpcBarkIntent.Retreat) sb.Append("You want to get away from them. ");

            sb.Append(eventContext);

            // Concrete surroundings: naming these is what keeps the line from being generic.
            BarkContext context = GetContext(health);
            if (context.NearbyDescription.Length > 0)
                sb.Append($" Nearby: {context.NearbyDescription}.");

            return sb.ToString();
        }

        private static string DescribeDamage(NpcBarkStateMachine machine, DamageLevel level,
            float depth, string bodyPart, Health health, BarkContext context)
        {
            string size = level switch
            {
                DamageLevel.Light => "a glancing hit",
                DamageLevel.Moderate => "a solid hit",
                DamageLevel.Severe => "a deep, serious wound",
                _ => "a devastating wound"
            };

            string intent = machine.Intent switch
            {
                NpcBarkIntent.Confront => "You want to make them pay.",
                NpcBarkIntent.Retreat => "You want to get away from them.",
                _ => "You are trying to stay standing."
            };

            string attacker = context != null && context.HasTarget ? $" by {context.TargetLabel}" : "";

            return $"You were just hit{attacker}: {size} on your {bodyPart} (penetration {depth:F1}). "
                   + $"Your health is at {health.HealthPercent:P0}. {intent}";
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

        private static DamageLevel ClassifyDamage(float depth, float healthPercent)
        {
            if (healthPercent <= 0.2f || depth >= 1.5f) return DamageLevel.Critical;
            if (depth >= 1.0f) return DamageLevel.Severe;
            if (depth >= 0.5f) return DamageLevel.Moderate;
            return DamageLevel.Light;
        }

        private static string ResolveBodyPartName(Health health, float depth)
        {
            if (depth >= 100f) return "body"; // ForceKill has no wound to inspect.

            var wounds = health.GetComponentInChildren<WoundsComponent>();
            if (wounds == null) return "body";

            var atlas = wounds.AtlasData;
            if (atlas == null || atlas.BodyPartMappings == null) return "body";

            int idx = wounds.LastHitBodyPartIndex;
            if (idx >= 0 && idx < atlas.BodyPartMappings.Count)
            {
                string name = atlas.BodyPartMappings[idx].Name;
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }

            return "body";
        }

        // ---------------------------------------------------------------- LLM transport

        /// <summary>
        /// Asks the LLM for a line, and falls back to local lines whenever the endpoint is
        /// unreachable, slow, or returns something unusable. Always returns speakable text.
        /// </summary>
        private async Task<(string dialogue, float volume)> RequestBarkAsync(
            Health health, string prompt, BarkSituation situation, string bodyPart)
        {
            string npcName = health.name;
            var personality = health.BarkPersonality;
            var machine = GetMachine(health);
            BarkContext context = GetContext(health);

            if (!EndpointAvailable)
            {
                Debug.Log($"[NPCBarkService] LLM offline — local {situation} bark for {npcName}.");
                return BuildFallback(personality, situation, bodyPart, context);
            }

            var sw = Stopwatch.StartNew();

            try
            {
                string systemPrompt = MasterDialoguePrompt;
                if (personality != null)
                    systemPrompt += "\n" + personality.BuildSystemPrompt();
                systemPrompt += $"\nCurrent mood: {machine.State}. Intent: {machine.Intent}.";

                string json = BuildRequestBody(systemPrompt, prompt, MaxResponseTokens, 0.8f);

                var result = await SendSingleRequestAsync(npcName, json);
                if (result != null)
                {
                    sw.Stop();
                    _consecutiveFailures = 0;
                    Debug.Log(
                        $"[NPCBarkService] {npcName} ({situation}): \"{Truncate(result.Value.dialogue, 80)}\" " +
                        $"(vol={result.Value.volume:F2}, {sw.Elapsed.TotalMilliseconds:F0}ms).");
                    return result.Value;
                }
            }
            catch (Exception e)
            {
                sw.Stop();
                Debug.LogError(
                    $"[NPCBarkService] Exception sending request for {npcName} " +
                    $"after {sw.Elapsed.TotalMilliseconds:F0}ms: {e}");
            }

            NoteFailure();
            return BuildFallback(personality, situation, bodyPart, context);
        }

        private bool EndpointAvailable => Time.unscaledTime >= _retryAfterTime;

        private void NoteFailure()
        {
            _consecutiveFailures++;
            if (_consecutiveFailures < FailureThreshold) return;

            _consecutiveFailures = 0;
            _retryAfterTime = Time.unscaledTime + UnavailableSeconds;
            Debug.LogWarning(
                $"[NPCBarkService] LLM endpoint looks offline — using local fallback barks " +
                $"for the next {UnavailableSeconds:F0}s.");
        }

        private static (string dialogue, float volume) BuildFallback(
            BarkPersonality personality, BarkSituation situation, string bodyPart, BarkContext context)
        {
            string target = context != null && context.HasTarget ? context.TargetLabel : null;
            string nearbyObject = context != null ? context.ObjectLabel : null;

            string line = personality != null ? personality.GetFallbackLine(situation) : null;
            if (string.IsNullOrWhiteSpace(line))
                line = BarkFallbackLines.GetLine(situation, bodyPart, target, nearbyObject);
            else
                line = BarkFallbackLines.Substitute(line, bodyPart, target, nearbyObject);

            return (line, BarkFallbackLines.VolumeFor(situation));
        }

        private async Task<(string dialogue, float volume)?> SendSingleRequestAsync(
            string npcName, string json)
        {
            var www = UnityWebRequest.Post(Endpoint, json, "application/json");
            www.timeout = Mathf.CeilToInt(RequestTimeoutSeconds);

            // Registered so Tick() can Abort() it if the connection stalls:
            // UnityWebRequest.timeout does not fire for every kind of dead endpoint.
            var active = new ActiveRequest { Request = www, StartedAt = Time.unscaledTime };
            _activeRequests.Add(active);

            var tcs = new TaskCompletionSource<(string dialogue, float volume)?>();

            try
            {
                www.SendWebRequest().completed += _ =>
                {
                    try
                    {
                        if (www.result != UnityWebRequest.Result.Success)
                        {
                            Debug.LogWarning(
                                $"[NPCBarkService] Request failed for {npcName} " +
                                $"({www.result}): {www.error}");
                            tcs.TrySetResult(null);
                            return;
                        }

                        string rawText = www.downloadHandler?.text ?? string.Empty;

                        JObject root;
                        try { root = JObject.Parse(rawText); }
                        catch (Exception ex)
                        {
                            Debug.LogWarning(
                                $"[NPCBarkService] JObject.Parse failed for {npcName}: {ex.Message}");
                            tcs.TrySetResult(null);
                            return;
                        }

                        string content = (string)root?["choices"]?[0]?["message"]?["content"];
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            var (dialogue, volume) = ExtractDialogue(content);
                            if (string.IsNullOrWhiteSpace(dialogue))
                                dialogue = null;

                            tcs.TrySetResult((dialogue, volume));
                        }
                        else
                        {
                            string reasoning = (string)root?["choices"]?[0]?["message"]?["reasoning_content"];
                            string finish = (string)root?["choices"]?[0]?["finish_reason"];
                            Debug.LogWarning(
                                string.IsNullOrWhiteSpace(reasoning)
                                    ? $"[NPCBarkService] {npcName} bark: no content field (finish_reason={finish})."
                                    : $"[NPCBarkService] {npcName} bark: only reasoning_content returned " +
                                      $"(finish_reason={finish}); thinking is still on — using fallback.");
                            tcs.TrySetResult(null);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(
                            $"[NPCBarkService] Exception in completed callback for {npcName}: {e}");
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

        private void AbortExpiredRequests()
        {
            if (_activeRequests.Count == 0) return;

            for (int i = _activeRequests.Count - 1; i >= 0; i--)
            {
                var active = _activeRequests[i];
                if (active.Request == null || Time.unscaledTime - active.StartedAt < RequestTimeoutSeconds)
                    continue;

                _activeRequests.RemoveAt(i);
                Debug.LogWarning(
                    $"[NPCBarkService] LLM request exceeded {RequestTimeoutSeconds:F0}s — aborting.");
                active.Request.Abort();
            }
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
                ["temperature"] = temperature,
                // llama.cpp extension: barks are one or two sentences, so skip the
                // thinking tokens even when the server runs with --reasoning on.
                ["chat_template_kwargs"] = new JObject
                {
                    ["enable_thinking"] = false
                }
            };

            return body.ToString(Newtonsoft.Json.Formatting.None);
        }

        // ---------------------------------------------------------------- player chat

        /// <summary>Nearest NPC the player is looking at (or standing next to), if any.</summary>
        public Health FindChatTarget()
        {
            Camera cam = Camera.main;

            if (cam != null)
            {
                var ray = new Ray(cam.transform.position, cam.transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, 15f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var looked = hit.collider.GetComponentInParent<Health>();
                    if (looked != null && !looked.IsDead && IsNpc(looked))
                        return looked;
                }
            }

            var npcs = WorldObjectRegistry.Instance?.Get(WorldObjectCategory.NPC);
            if (npcs == null) return null;

            Transform player = ResolvePlayer();
            Vector3 origin = player != null ? player.position : (cam != null ? cam.transform.position : Vector3.zero);

            Health best = null;
            float bestSqr = ChatRange * ChatRange;

            for (int i = 0; i < npcs.Count; i++)
            {
                var go = npcs[i];
                if (go == null) continue;

                var health = go.GetComponent<Health>();
                if (health == null || health.IsDead) continue;

                float sqr = (health.transform.position - origin).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = health;
            }

            return best;
        }

        /// <summary>
        /// Sends a player line to one NPC and returns its spoken reply. If the question asks
        /// for a decision (yes/no or a choice), jev decides first and the text model only
        /// phrases that decision — the model is never asked to reason on its own.
        /// </summary>
        public async Task<(string reply, float volume)> ChatAsync(Health npc, string playerText)
        {
            if (npc == null || string.IsNullOrWhiteSpace(playerText))
                return (null, 0.6f);

            // A new conversation starts fresh; continuing one keeps its history.
            if (!InConversation(npc))
            {
                _chatHistory.Remove(npc);
                _lastDecision.Remove(npc);
            }

            MarkConversation(npc);

            var machine = GetMachine(npc);
            string decision = await DecideFromQuestionAsync(npc, machine, playerText);

            if (!string.IsNullOrEmpty(decision))
                _lastDecision[npc] = decision;

            string system = BuildChatSystemPrompt(npc, machine, decision);
            var result = await SendChatAsync(npc, system, playerText);

            if (result == null)
            {
                Debug.LogWarning($"[NPCBarkService] Chat reply failed for {npc.name}.");
                string fallback = BarkFallbackLines.GetLine(BarkSituation.Idle);
                SpeakLine(npc, fallback, 0.6f);
                return (fallback, 0.6f);
            }

            var (reply, volume) = result.Value;
            if (string.IsNullOrWhiteSpace(reply))
            {
                string fallback = BarkFallbackLines.GetLine(BarkSituation.Idle);
                SpeakLine(npc, fallback, 0.6f);
                return (fallback, 0.6f);
            }

            RememberChat(npc, playerText, reply);
            RecordDialogue(npc, reply);
            SpeakLine(npc, reply, volume);

            return (reply, volume);
        }

        private void SpeakLine(Health npc, string line, float volume)
        {
            var display = npc.GetComponentInChildren<BarkDisplay>();
            if (display == null) return;

            display.Setup(_piperManager);
            _speech.Enqueue(display, line, volume, BarkPriority.Important);
        }

        private void RememberChat(Health npc, string playerText, string reply)
        {
            if (!_chatHistory.TryGetValue(npc, out var history))
            {
                history = new List<JObject>();
                _chatHistory[npc] = history;
            }

            history.Add(new JObject { ["role"] = "user", ["content"] = playerText });
            history.Add(new JObject { ["role"] = "assistant", ["content"] = reply });

            while (history.Count > MaxChatHistory) history.RemoveAt(0);
        }

        /// <summary>
        /// Runs jev when the traveller asks for a decision. Returns the decided option, or
        /// null when the message is ordinary conversation.
        /// </summary>
        private async Task<string> DecideFromQuestionAsync(Health health, NpcBarkStateMachine machine,
            string playerText)
        {
            if (_reasoning == null || !_reasoning.Available) return null;

            try
            {
                string classify = BuildReasoningState(health, machine,
                    $"The traveller says: \"{playerText}\"",
                    "Does this ask you to make a decision or choose between options?");

                bool? asksForDecision = await _reasoning.AskAsync(classify, "reasoning",
                    "Yes, it asks for a decision.",
                    "No, it is just conversation.");

                if (asksForDecision != true) return null;

                string[] options = ExtractOptions(playerText);
                var criteria = new (string letter, string criterion)[options.Length];
                for (int i = 0; i < options.Length; i++)
                    criteria[i] = (((char)('A' + i)).ToString(), options[i]);

                string state = BuildReasoningState(health, machine,
                    $"The traveller asks: \"{playerText}\"", "What do you decide?");

                string letter = await _reasoning.ChooseAsync(state, "decision", criteria);
                if (letter == null || letter.Length != 1) return null;

                int index = letter[0] - 'A';
                if (index < 0 || index >= options.Length) return null;

                Debug.Log($"[NPCBarkService] jev decided for {health.name}: {options[index]}");
                return options[index];
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NPCBarkService] Decision tool failed: {e.Message}");
                return null;
            }
        }

        private static string[] ExtractOptions(string text)
        {
            int orIndex = text.LastIndexOf(" or ", StringComparison.OrdinalIgnoreCase);
            if (orIndex > 2)
            {
                string left = TrimClause(text[..orIndex]);
                string right = TrimClause(text[(orIndex + 4)..]);
                if (left.Length is > 0 and <= 48 && right.Length is > 0 and <= 48)
                    return new[] { left, right };
            }

            return new[] { "Yes, you agree.", "No, you refuse." };
        }

        private static string TrimClause(string value)
        {
            value = value.Trim().TrimEnd('?', '.', '!', ',', ' ');

            string[] prefixes =
            {
                "should we ", "shall we ", "should i ", "do you ", "will you ",
                "would you ", "can you ", "are you ", "is it ", "tell me, "
            };

            foreach (string prefix in prefixes)
            {
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = value[prefix.Length..].Trim();
                    break;
                }
            }

            return value;
        }

        private string BuildChatSystemPrompt(Health health, NpcBarkStateMachine machine, string decision)
        {
            var sb = new StringBuilder();
            sb.Append(MasterDialoguePrompt);

            var personality = health.BarkPersonality;
            if (personality != null)
                sb.Append('\n').Append(personality.BuildSystemPrompt());

            sb.Append($"\n\nYou are {health.name}, speaking with the traveller. ");
            sb.Append($"Your mood is {MoodWord(machine.State)} (anger {machine.Anger:P0}, fear {machine.Fear:P0}), ");
            sb.Append($"and you want to {IntentWord(machine.Intent)}. ");
            sb.Append(BuildNpcStatus(health, machine));

            string memory = BuildMemoryContext(health);
            if (!string.IsNullOrEmpty(memory))
                sb.Append(' ').Append(memory.Replace('\n', ' '));

            if (!string.IsNullOrEmpty(decision))
            {
                sb.Append($" You have already decided: {decision} Express that decision and stay consistent with it. ");
            }
            else if (_lastDecision.TryGetValue(health, out string previous) && !string.IsNullOrEmpty(previous))
            {
                sb.Append($" Earlier in this conversation you decided: {previous} Stay consistent with it. ");
            }

            sb.Append(" This is a continuing conversation: remember what was already said, react to your own body and mood, ");
            sb.Append("and answer in 1-2 short spoken sentences.");
            return sb.ToString();
        }

        private async Task<(string dialogue, float volume)?> SendChatAsync(Health health, string system,
            string playerText)
        {
            var messages = new JArray();
            messages.Add(new JObject { ["role"] = "system", ["content"] = system });

            if (_chatHistory.TryGetValue(health, out var history))
            {
                foreach (var message in history)
                    messages.Add(message);
            }

            messages.Add(new JObject { ["role"] = "user", ["content"] = playerText });

            var body = new JObject
            {
                ["messages"] = messages,
                ["max_tokens"] = MaxChatTokens,
                ["temperature"] = 0.7f,
                ["chat_template_kwargs"] = new JObject { ["enable_thinking"] = false }
            };

            return await SendSingleRequestAsync($"{health.name} (chat)", body.ToString(Newtonsoft.Json.Formatting.None));
        }

        // ---------------------------------------------------------------- display

        private void DisplayBark(Health health, string bark, float volume, BarkSituation situation)
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

                // One voice at a time: the scheduler drops ambient chatter while busy and
                // replaces a queued line when this NPC speaks again.
                _speech.Enqueue(display, bark, volume, PriorityFor(situation));

                RecordDialogue(health, bark);
                BroadcastBark(health, bark);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPCBarkService] DisplayBark exception for {health?.name}: {e}");
            }
        }

        private static BarkPriority PriorityFor(BarkSituation situation) => situation switch
        {
            BarkSituation.Idle => BarkPriority.Ambient,
            BarkSituation.AngryIdle => BarkPriority.Ambient,
            BarkSituation.Reaction => BarkPriority.Ambient,
            _ => BarkPriority.Important
        };

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

        /// <summary>NPCs nearby may react to a bark they heard.</summary>
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
                if (InConversation(health)) continue;

                float dist = Vector3.Distance(sourcePos, health.transform.position);
                if (dist > HearingRange) continue;

                if (!_lastReactionTimes.TryGetValue(health, out float lastTime))
                    lastTime = 0f;
                if (Time.unscaledTime - lastTime < ReactionCooldownSeconds) continue;

                var personality = health.BarkPersonality;

                float chance = 0.3f
                    * (((personality?.Aggression ?? 0.5f)
                        + (personality?.Verbosity ?? 0.3f)
                        + (personality?.Cruelty ?? 0.5f)) / 3f + 0.5f);
                if (Random.value > chance) continue;

                _lastReactionTimes[health] = Time.unscaledTime;

                var machine = GetMachine(health);
                string context = $"You overheard {source.name} say: \"{bark}\". React in character. Keep it very short.";
                FireAndForgetBark(health, BuildPrompt(health, machine, context), BarkSituation.Reaction);
            }
        }

        // ---------------------------------------------------------------- helpers

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

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxLen) return s;
            return s[..maxLen] + "...";
        }
    }
}
