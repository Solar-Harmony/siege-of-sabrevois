using System;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    /// <summary>
    /// Small per-NPC state machine behind the bark system.
    ///
    /// Anger and fear are persisted floats that decay after a short hold. The visible mood
    /// (<see cref="State"/>) is derived from them plus health every tick, so an NPC that was
    /// hit stays angry for a while and keeps using angry idle lines long after the attacker
    /// left — that is the "persisted aggro" behaviour.
    ///
    /// <see cref="Intent"/> is the personality-driven decision (fight vs run) that the utility
    /// AI can read through <see cref="Health.Anger"/> / <see cref="Health.Fear"/>.
    /// </summary>
    public sealed class NpcBarkStateMachine
    {
        private const float AggroEnterThreshold = 0.4f;
        private const float AggroExitThreshold = 0.15f;
        private const float AngerHoldSeconds = 20f;
        private const float AngerDecayPerSecond = 0.10f;
        private const float FearDecayPerSecond = 0.12f;
        private const float WoundedHealthPercent = 0.9f;
        private const float CriticalHealthPercent = 0.2f;

        private float _healthPercent = 1f;
        private float _lastDamageTime = -999f;
        private bool _wasAngry;

        private NpcBarkIntent _decidedIntent = NpcBarkIntent.None;
        private bool _hasDecision;

        public NpcBarkState State { get; private set; } = NpcBarkState.Calm;
        public NpcBarkIntent Intent { get; private set; } = NpcBarkIntent.None;
        public float Anger { get; private set; }
        public float Fear { get; private set; }

        /// <summary>Closest character this NPC is aware of, if any.</summary>
        public Transform Threat { get; private set; }

        /// <summary>Raised whenever the derived mood changes.</summary>
        public event Action<NpcBarkState> StateEntered;

        /// <summary>The line set used when this NPC has nothing to react to.</summary>
        public BarkSituation IdleSituation => State switch
        {
            NpcBarkState.Angry => BarkSituation.AngryIdle,
            NpcBarkState.Wounded => BarkSituation.HurtModerate,
            NpcBarkState.Critical => BarkSituation.Desperate,
            // Wary of someone nearby: "who goes there" beats "god bless".
            NpcBarkState.Alert => BarkSituation.Sight,
            _ => BarkSituation.Idle
        };

        public static BarkSituation SituationFor(DamageLevel level) => level switch
        {
            DamageLevel.Light => BarkSituation.HurtLight,
            DamageLevel.Moderate => BarkSituation.HurtModerate,
            DamageLevel.Severe => BarkSituation.HurtHeavy,
            _ => BarkSituation.Desperate
        };

        /// <summary>Called every perception tick with the freshly sampled world state.</summary>
        public void Observe(float dt, float healthPercent, Transform threat, BarkPersonality personality)
        {
            _healthPercent = healthPercent;
            Threat = threat;

            bool holding = Time.unscaledTime - _lastDamageTime < AngerHoldSeconds;
            if (!holding)
            {
                Anger = Mathf.Max(0f, Anger - AngerDecayPerSecond * dt);
                Fear = Mathf.Max(0f, Fear - FearDecayPerSecond * dt);
            }

            Evaluate(personality);
        }

        public void OnDamaged(DamageLevel level, float healthPercent, BarkPersonality personality)
        {
            _healthPercent = healthPercent;
            _lastDamageTime = Time.unscaledTime;

            float aggression = personality != null ? personality.Aggression : 0.5f;
            float cowardice = personality != null ? personality.Cowardice : 0.5f;
            float severity = Severity(level);

            // A real hit must actually make them angry: Moderate and above crosses the
            // 0.4 aggro threshold for any personality, Light needs a second hit.
            Anger = Mathf.Clamp01(Anger + severity * Mathf.Lerp(0.90f, 1.30f, aggression));
            Fear = Mathf.Clamp01(Fear + severity * Mathf.Lerp(0.60f, 1.00f, cowardice));

            Evaluate(personality);
        }

        public void OnSighted(Transform target, BarkPersonality personality)
        {
            if (target != null) Threat = target;

            float aggression = personality != null ? personality.Aggression : 0.5f;
            Anger = Mathf.Clamp01(Anger + 0.12f * aggression);

            Evaluate(personality);
        }

        public void OnLimbSevered(BarkPersonality personality)
        {
            _lastDamageTime = Time.unscaledTime;
            Anger = Mathf.Clamp01(Anger + 0.35f);
            Fear = Mathf.Clamp01(Fear + 0.45f);

            Evaluate(personality);
        }

        public void Kill()
        {
            if (State == NpcBarkState.Dead) return;
            State = NpcBarkState.Dead;
            Intent = NpcBarkIntent.None;
            StateEntered?.Invoke(State);
        }

        private void Evaluate(BarkPersonality personality)
        {
            if (State == NpcBarkState.Dead) return;

            // Hysteresis: it takes a real hit to become angry, and anger lingers until it decays.
            bool angry = Anger >= (_wasAngry ? AggroExitThreshold : AggroEnterThreshold);
            _wasAngry = angry;

            NpcBarkState next;
            if (_healthPercent <= CriticalHealthPercent) next = NpcBarkState.Critical;
            else if (angry) next = NpcBarkState.Angry;
            else if (_healthPercent < WoundedHealthPercent) next = NpcBarkState.Wounded;
            else if (Threat != null) next = NpcBarkState.Alert;
            else next = NpcBarkState.Calm;

            // A decision from the reasoning model wins over the personality heuristic until
            // the NPC calms down again.
            if (next is NpcBarkState.Calm or NpcBarkState.Dead)
                _hasDecision = false;

            Intent = _hasDecision ? _decidedIntent : ResolveIntent(next, personality);

            if (next == State) return;

            State = next;
            StateEntered?.Invoke(State);
        }

        /// <summary>
        /// Applies an externally decided intent (from the reasoning model). It is kept until
        /// the NPC returns to Calm or dies, instead of being recomputed every tick.
        /// </summary>
        public void ApplyDecision(NpcBarkIntent intent)
        {
            _decidedIntent = intent;
            _hasDecision = true;
            Intent = intent;
        }

        /// <summary>Drops the external decision and falls back to the personality heuristic.</summary>
        public void ClearDecision()
        {
            _hasDecision = false;
        }

        private NpcBarkIntent ResolveIntent(NpcBarkState state, BarkPersonality personality)
        {
            if (state is NpcBarkState.Calm or NpcBarkState.Alert or NpcBarkState.Dead)
                return NpcBarkIntent.None;

            float aggression = personality != null ? personality.Aggression : 0.5f;
            float pride = personality != null ? personality.Pride : 0.5f;
            float cowardice = personality != null ? personality.Cowardice : 0.5f;

            float confront = aggression * 0.7f + pride * 0.2f + Anger * 0.7f;
            float retreat = cowardice * 0.8f + Fear * 0.6f + (1f - _healthPercent) * 0.5f;

            return confront >= retreat ? NpcBarkIntent.Confront : NpcBarkIntent.Retreat;
        }

        private static float Severity(DamageLevel level) => level switch
        {
            DamageLevel.Light => 0.25f,
            DamageLevel.Moderate => 0.5f,
            DamageLevel.Severe => 0.8f,
            _ => 1f
        };
    }
}
