using System;

namespace Sabrevois.Gameplay
{
    /// <summary>
    /// RAON-1-nano served by the KRAFTON llama.cpp fork on :8000.
    /// It only turns an already-decided bark into natural speech.
    /// </summary>
    [Serializable]
    public sealed class RaonModel : LocalModelBase
    {
        public override string Id => "raon";
        public override string DisplayName => "RAON-1 (text)";

        public RaonModel()
        {
            BaseUrl = "http://127.0.0.1:8000";
            HealthUrl = BaseUrl + "/health";
            StartScriptPath = "F:/Apps/KraftonLlamaCpp/start-server.cmd";
            WorkingDirectory = "F:/Apps/KraftonLlamaCpp";
            StartupTimeoutSeconds = 120f;
        }
    }
}
