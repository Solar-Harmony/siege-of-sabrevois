using System;
using SolarHarmony.Config;
using UnityEngine;

namespace Sabrevois.AI
{
    [Serializable, GameConfig]
    public class AIGameConfig
    {
        [Tooltip("When enabled, all utility AI agents will stop making decisions and executing actions.")]
        public bool DisableAllAgents;
    }
}
