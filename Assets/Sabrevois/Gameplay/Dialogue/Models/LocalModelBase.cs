using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Sabrevois.Gameplay
{
    /// <summary>
    /// Shared health-check / launch logic for the local model servers.
    /// Keeps the HTTP and process code in one place so each model only declares its defaults.
    /// </summary>
    [Serializable]
    public abstract class LocalModelBase : ILocalModel
    {
        [Tooltip("Launch this server automatically when the game boots and it is not already answering.")]
        public bool AutoStart = true;

        [Tooltip("Root URL the game uses to talk to this server.")]
        public string BaseUrl = "";

        [Tooltip("URL polled to find out whether the server is already running.")]
        public string HealthUrl = "";

        [Tooltip("Launch script for the server (.cmd or .ps1).")]
        public string StartScriptPath = "";

        [Tooltip("Working directory the launch script expects.")]
        public string WorkingDirectory = "";

        [Min(1f)]
        [Tooltip("Seconds to wait for the server to answer after launching it.")]
        public float StartupTimeoutSeconds = 90f;

        public abstract string Id { get; }
        public abstract string DisplayName { get; }

        bool ILocalModel.AutoStart => AutoStart;
        string ILocalModel.BaseUrl => BaseUrl;
        string ILocalModel.HealthUrl => HealthUrl;
        string ILocalModel.StartScriptPath => StartScriptPath;
        string ILocalModel.WorkingDirectory => WorkingDirectory;
        float ILocalModel.StartupTimeoutSeconds => StartupTimeoutSeconds;

        public async Task<bool> IsRunningAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(HealthUrl)) return false;

            using var request = UnityWebRequest.Get(HealthUrl);
            request.timeout = 2;

            var completion = new TaskCompletionSource<bool>();
            request.SendWebRequest().completed += _ =>
                completion.TrySetResult(request.result == UnityWebRequest.Result.Success);

            using var registration = cancellationToken.Register(() =>
            {
                request.Abort();
                completion.TrySetCanceled(cancellationToken);
            });

            try
            {
                return await completion.Task;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        public Task<bool> TryStartAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(false);

            if (string.IsNullOrWhiteSpace(StartScriptPath) || !File.Exists(StartScriptPath))
            {
                Debug.LogWarning($"[LocalModels] {DisplayName} launch script not found: '{StartScriptPath}'.");
                return Task.FromResult(false);
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                if (!string.IsNullOrWhiteSpace(WorkingDirectory) && Directory.Exists(WorkingDirectory))
                    startInfo.WorkingDirectory = WorkingDirectory;

                if (StartScriptPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                {
                    startInfo.FileName = "powershell.exe";
                    startInfo.Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{StartScriptPath}\"";
                }
                else
                {
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = $"/c \"\"{StartScriptPath}\"\"";
                }

                using Process process = Process.Start(startInfo);
                return Task.FromResult(process != null);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LocalModels] Failed to launch {DisplayName}: {e.Message}");
                return Task.FromResult(false);
            }
        }

        public async Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default)
        {
            if (await IsRunningAsync(cancellationToken))
            {
                Debug.Log($"[LocalModels] {DisplayName} is already running at {BaseUrl}.");
                return true;
            }

            if (!AutoStart)
            {
                Debug.Log($"[LocalModels] {DisplayName} is not running and auto-start is disabled.");
                return false;
            }

            if (!await TryStartAsync(cancellationToken))
                return false;

            Debug.Log($"[LocalModels] Launching {DisplayName}; waiting up to {StartupTimeoutSeconds:F0}s for {HealthUrl} ...");

            DateTime deadline = DateTime.UtcNow.AddSeconds(StartupTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }

                if (await IsRunningAsync(cancellationToken))
                {
                    Debug.Log($"[LocalModels] {DisplayName} is ready.");
                    return true;
                }
            }

            Debug.LogWarning(
                $"[LocalModels] {DisplayName} did not answer within {StartupTimeoutSeconds:F0}s. " +
                $"Check its server log next to '{StartScriptPath}'.");
            return false;
        }
    }
}
