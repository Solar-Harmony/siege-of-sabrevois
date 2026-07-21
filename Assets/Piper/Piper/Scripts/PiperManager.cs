using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Piper
{
    public enum Provider { CPU, DirectML }

    public class PiperManager : MonoBehaviour
    {
        public int sampleRate = 22050;

        public float scaleNoise = 0.667f;
        public float scaleLength = 1.0f;
        public float scaleNoiseW = 0.8f;

        public string espeakNgRelativePath = "espeak-ng-data";
        public string modelOnnxRelativePath = "piper/en_US-lessac-medium.onnx";
        public string voice = "en-us";

        public Provider provider = Provider.DirectML;

        private InferenceSession _session;
        private string _modelPath;
        private Task _initTask;
        private SemaphoreSlim _semaphore = new(1, 1);

        private readonly Queue<AudioClip> _clipPool = new();
        private const int MaxPoolSize = 8;
        private const int MaxPoolSampleCount = 22050 * 10;

        public bool IsReady { get; private set; }

        private void Awake()
        {
            _modelPath = Path.Combine(Application.streamingAssetsPath, modelOnnxRelativePath);
            _initTask = InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                string espeakPath = ResolveEspeakPath(espeakNgRelativePath);
                bool initOk = await Task.Run(() => PiperWrapper.InitPiper(espeakPath));
                if (!initOk)
                    throw new InvalidOperationException(
                        $"Piper native init failed. espeak-ng data path: '{espeakPath}'");

                _session = await Task.Run(() => CreateSession(_modelPath));

                sw.Stop();
                Debug.Log(
                    $"[PiperManager] Initialisation took {sw.Elapsed.TotalSeconds:F2}s " +
                    $"(provider={provider}, model={modelOnnxRelativePath}).");
                IsReady = true;
            }
            catch (Exception e)
            {
                sw.Stop();
                Debug.LogError(
                    $"[PiperManager] Initialisation failed after {sw.Elapsed.TotalSeconds:F2}s: {e.Message}");
                throw;
            }
        }

        private static string ResolveEspeakPath(string relativePath)
        {
            string path = Path.Combine(Application.streamingAssetsPath, relativePath);

            if (Directory.Exists(path))
                return path;

            if (Application.platform is RuntimePlatform.Android or RuntimePlatform.WebGLPlayer)
                throw new InvalidOperationException(
                    $"espeak-ng data path '{path}' is not directly accessible on {Application.platform}. " +
                    "Copy the espeak-ng-data folder to Application.persistentDataPath before calling InitPiper.");

            throw new InvalidOperationException(
                $"espeak-ng data not found at '{path}'. " +
                $"Ensure '{relativePath}' exists under StreamingAssets.");
        }

        private InferenceSession CreateSession(string path)
        {
            if (provider == Provider.DirectML)
                return CreateDirectMLSession(path);
            return new InferenceSession(path);
        }

        private static InferenceSession CreateDirectMLSession(string path)
        {
            var opts = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                EnableMemoryPattern = false,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
            };
            opts.AppendExecutionProvider_DML(0);
            var session = new InferenceSession(path, opts);

            // Warmup: first Run() compiles DML kernels — do it now to avoid freeze later
            var warmupIds = new DenseTensor<long>(new long[] { 0, 1, 2 }, new[] { 1, 3 });
            var warmupLen = new DenseTensor<long>(new long[] { 3 }, new[] { 1 });
            var warmupSc  = new DenseTensor<float>(new[] { 0.667f, 1f, 0.8f }, new[] { 3 });
            var warmupIn = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", warmupIds),
                NamedOnnxValue.CreateFromTensor("input_lengths", warmupLen),
                NamedOnnxValue.CreateFromTensor("scales", warmupSc)
            };
            using (session.Run(warmupIn)) { }

            return session;
        }

        public void ReleaseClip(AudioClip clip)
        {
            if (clip == null) return;

            if (_clipPool.Count < MaxPoolSize)
            {
                _clipPool.Enqueue(clip);
            }
            else
            {
                Destroy(clip);
            }
        }

        public async Task<AudioClip> TextToSpeechAsync(string text, CancellationToken ct = default)
        {
            await _initTask;

            if (_session == null)
                throw new InvalidOperationException(
                    "PiperManager is not initialized. ONNX session is null.");

            await _semaphore.WaitAsync(ct);
            try
            {
                var totalSw = Stopwatch.StartNew();
                float phonemeMs = 0;
                float inferMs = 0;

                var samples = await Task.Run(() =>
                {
                    var phSw = Stopwatch.StartNew();
                    var phonemeResult = PiperWrapper.ProcessText(text, voice);
                    phSw.Stop();
                    phonemeMs = (float)phSw.Elapsed.TotalMilliseconds;

                    if (phonemeResult == null)
                        throw new InvalidOperationException(
                            $"Piper phonemisation failed for text: \"{Truncate(text, 40)}\"");

                    var allSamples = new List<float>();

                    for (int s = 0; s < phonemeResult.Sentences.Length; s++)
                    {
                        ct.ThrowIfCancellationRequested();

                        var sentence = phonemeResult.Sentences[s];
                        int[] phonemeIds = sentence.PhonemesIds;
                        int c = phonemeIds.Length;

                        var idsTensor = new DenseTensor<long>(ToLong(phonemeIds), new[] { 1, c });
                        var lenTensor = new DenseTensor<long>(new long[] { c }, new[] { 1 });
                        var scTensor = new DenseTensor<float>(
                            new[] { scaleNoise, scaleLength, scaleNoiseW }, new[] { 3 });

                        var inputs = new List<NamedOnnxValue>
                        {
                            NamedOnnxValue.CreateFromTensor("input", idsTensor),
                            NamedOnnxValue.CreateFromTensor("input_lengths", lenTensor),
                            NamedOnnxValue.CreateFromTensor("scales", scTensor)
                        };

                        var infSw = Stopwatch.StartNew();
                        using var results = _session.Run(inputs);
                        infSw.Stop();
                        inferMs += (float)infSw.Elapsed.TotalMilliseconds;

                        var output = results[0].AsTensor<float>();
                        allSamples.AddRange(output.ToArray());
                    }

                    return allSamples;
                }, ct);

                var clip = GetPooledClip(samples.Count);
                clip.SetData(samples.ToArray(), 0);

                totalSw.Stop();
                Debug.Log(
                    $"[PiperManager] TTS \"{Truncate(text, 40)}\": " +
                    $"{totalSw.Elapsed.TotalMilliseconds:F0}ms total " +
                    $"(phonemisation {phonemeMs:F0}ms, inference {inferMs:F0}ms, " +
                    $"{samples.Count / (float)sampleRate:F1}s audio).");

                return clip;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private AudioClip GetPooledClip(int sampleCount)
        {
            while (_clipPool.Count > 0)
            {
                var clip = _clipPool.Dequeue();
                if (clip != null && clip.samples >= sampleCount)
                    return clip;
                if (clip != null)
                    Destroy(clip);
            }

            int size = Mathf.NextPowerOfTwo(Mathf.Max(sampleCount, MaxPoolSampleCount));
            return AudioClip.Create("PiperTTS", size, 1, sampleRate, false);
        }

        private static long[] ToLong(int[] arr)
        {
            var r = new long[arr.Length];
            for (int i = 0; i < arr.Length; i++) r[i] = arr[i];
            return r;
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxLen) return s;
            return s[..maxLen] + "...";
        }

        private void OnDestroy()
        {
            PiperWrapper.FreePiper();
            _session?.Dispose();

            while (_clipPool.Count > 0)
            {
                var clip = _clipPool.Dequeue();
                if (clip != null)
                    Destroy(clip);
            }
        }
    }
}
