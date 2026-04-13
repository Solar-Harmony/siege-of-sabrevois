using System;
using ArtificeToolkit.Attributes;
using UnityEngine;

namespace Sabrevois.AI
{
    [CreateAssetMenu(menuName = "Sabrevois/AI/Settings")]
    public class AISettings : ScriptableObject
    {
        // 0: Parallel Auto Processor Count
        // 1: Sequential Single-Processor
        // 2+: Parallel Manual Processor Count
        public uint ProcessorCount = 0;
    }
}