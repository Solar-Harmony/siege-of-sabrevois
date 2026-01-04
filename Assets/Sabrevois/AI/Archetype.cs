using System;
using ArtificeToolkit.Attributes;
using Sabrevois.AI.Actions;
using UnityEngine;

namespace Sabrevois.AI
{
    /// <summary>
    /// Behavior template for a Utility AI agent.
    /// </summary>
    [CreateAssetMenu(menuName = "Sabrevois/AI Archetype")]
    public class Archetype : ScriptableObject
    {
        [InfoBox("If multiple actions have the same desirability score, the first one will be chosen as per order in the list.")]
        public ActionCandidate[] Actions;
        
        public float DecisionMakingInterval = 1f;
    }
}