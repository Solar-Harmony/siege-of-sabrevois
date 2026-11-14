using System.Threading;
using System.Threading.Tasks;

namespace Sabrevois.Gameplay
{
    /// <summary>
    /// A locally hosted model server (jev, RAON-1, ...) the game can talk to.
    ///
    /// Implementations are <see cref="System.SerializableAttribute">serializable</see> so they
    /// can be added and picked from the type dropdown of the Solar Harmony game config.
    /// The model list there decides which servers are launched on scene start.
    /// </summary>
    public interface ILocalModel
    {
        /// <summary>Stable id used to look the model up from code and settings.</summary>
        string Id { get; }

        /// <summary>Name shown in the game config inspector and in logs.</summary>
        string DisplayName { get; }

        /// <summary>Start the server automatically when it is not already answering.</summary>
        bool AutoStart { get; }

        /// <summary>Root URL of the server, for example http://127.0.0.1:8000.</summary>
        string BaseUrl { get; }

        /// <summary>URL polled to check whether the server is already running.</summary>
        string HealthUrl { get; }

        /// <summary>Script that launches the server (a .cmd or .ps1 file).</summary>
        string StartScriptPath { get; }

        /// <summary>Working directory expected by the launch script.</summary>
        string WorkingDirectory { get; }

        /// <summary>How long to wait for the server to answer after launching it.</summary>
        float StartupTimeoutSeconds { get; }

        /// <summary>True when the health endpoint answers successfully.</summary>
        Task<bool> IsRunningAsync(CancellationToken cancellationToken = default);

        /// <summary>Runs the launch script. Does not wait for the server to become ready.</summary>
        Task<bool> TryStartAsync(CancellationToken cancellationToken = default);

        /// <summary>Checks the health endpoint, launches the server if needed, then waits for it.</summary>
        Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default);
    }
}
