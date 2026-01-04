using System;
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
        [field: SerializeField] 
        public ActionCandidate[] Actions { get; private set; }
        
        [field: SerializeField]
        public float DecisionMakingInterval { get; private set; } = 1f;
    }
}