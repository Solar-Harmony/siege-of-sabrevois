using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Sabrevois.Gameplay
{
    /// <summary>
    /// On scene start, checks the configured local model servers and launches the ones that
    /// are not already answering. A server that is already up (started by hand or from a
    /// previous session) is left untouched.
    ///
    /// Set SABREVOIS_AUTOSTART_MODELS=0 to skip the automatic launch entirely.
    /// </summary>
    public sealed class LocalModelLauncher : IInitializable, IDisposable
    {
        private static readonly ILocalModel[] FallbackModels = { new JevModel(), new RaonModel() };

        private readonly CancellationTokenSource _lifetime = new();

        [Inject(Optional = true)] private LocalModelSettings _settings;

        public void Initialize()
        {
            if (_settings != null && !_settings.AutoStartOnLoad) return;
            if (IsDisabledByEnvironment()) return;

            _ = StartMissingModelsAsync(_lifetime.Token);
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private async Task StartMissingModelsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<ILocalModel> models = FallbackModels;
            if (_settings?.Models is { Count: > 0 } configured)
                models = configured;

            var launches = new List<Task<bool>>(models.Count);
            foreach (ILocalModel model in models)
            {
                if (model != null)
                    launches.Add(model.EnsureRunningAsync(cancellationToken));
            }

            try
            {
                bool[] results = await Task.WhenAll(launches);
                int ready = 0;
                foreach (bool result in results)
                {
                    if (result) ready++;
                }

                if (ready < launches.Count)
                {
                    Debug.LogWarning(
                        $"[LocalModels] {launches.Count - ready} of {launches.Count} local model(s) are unavailable; " +
                        "NPC dialogue will use the authored fallbacks.");
                }
            }
            catch (OperationCanceledException)
            {
                // Application is quitting while the servers were starting.
            }
            catch (Exception e)
            {
                Debug.LogError($"[LocalModels] Model startup failed: {e}");
            }
        }

        private static bool IsDisabledByEnvironment()
        {
            string value = Environment.GetEnvironmentVariable("SABREVOIS_AUTOSTART_MODELS");
            return !string.IsNullOrEmpty(value) && value == "0";
        }
    }
}
