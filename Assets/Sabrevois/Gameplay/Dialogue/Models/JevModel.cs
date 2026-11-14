using System;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    /// <summary>
    /// jevos-v4 typed decisions (fight / flee / hold, speak / stay silent) on :8017.
    /// It does not generate text, so <see cref="NpcReasoningService"/> uses it before the
    /// wording model picks an actual line.
    /// </summary>
    [Serializable]
    public sealed class JevModel : LocalModelBase
    {
        [Tooltip("Model name expected by the jev server.")]
        public string ModelName = "jevos-v4";

        public override string Id => "jev";
        public override string DisplayName => "jev (typed decisions)";

        public JevModel()
        {
            BaseUrl = "http://127.0.0.1:8017";
            HealthUrl = BaseUrl + "/health";
            StartScriptPath = "F:/Apps/Jev/start-server.cmd";
            WorkingDirectory = "F:/Apps/Jev";
            StartupTimeoutSeconds = 60f;
        }
    }
}
