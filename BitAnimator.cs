
// Copyright © 2019 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 0.3 (02.12.2018)
// Version: 0.4 (01.06.2019)
// Version: 0.5 (25.06.2019)
// Version: 1.0 (14.07.2019)

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using DSPLib;
using System.Collections;
using AudioVisualization.Modificators;

namespace AudioVisualization
{
    [ExecuteInEditMode]
    public class BitAnimator : MonoBehaviour
    {
        [Tooltip("Root gameobject with Animator component")]
        public Animator animatorObject;
        [Tooltip("Gameobject with shader/particles")]
        public GameObject targetObject;
        [Tooltip("AudioClip (\"Load type\" must be: Decompress On Load)")]
        public AudioClip audioClip;
        public enum RecordMode
        {
            Clear, Overwrite
        };
        public bool recalculateSpectrum = true;
        public bool normalizeSpectrum = true;
        public bool calculateLoudness;
        public bool energyCorrection = true;
        public bool calculateVelocity;
        public RecordMode record = RecordMode.Overwrite;
        public DSP.Window.Type filter = DSP.Window.Type.Hann;
        [Range(7, 16)]
        public int FFTWindowLog = 11;
        public int FFTWindow;
        [Range(0, 200)]
        public float windowParam = 60.0f;
        [Range(1, 32)]
        public int multisamples = 2;
        [Range(0, 1)]
        public float quality = 0.5f;
        public float multiply = 1.0f;
        [Range(-1, 1)]
        public float deconvolution;
        public string animationAssetPath;
        public AnimationClip animationClip;
        public ComputeProgram core;
        public float animationTime;

        protected float[] spectrumMap;
        protected int chunks;
        protected int bins;
        private string spectrumCacheFilename;
        [Serializable]
        public class RecordSlot
        {
            public enum PropertiesSet
            {
                Material, ParticleSystem, BlendShape, Transform
            };
            //Extension for ShaderUtil.ShaderPropertyType
            public enum PropertyType
            {
                Color, Vector, Float, Range, TexEnv, Vector3, Quaternion
            }
            public PropertyType type;
            public PropertiesSet typeSet;
            //[Tooltip("Gameobject with shader/particles")]
            //public GameObject targetObject;
            public string name;
            public string[] property;
            public string description;
            public int startFreq;
            public int endFreq;
            public Vector4 minValue, maxValue;
            public float rangeMin, rangeMax;
            public float ampSmoothness;
            [Range(0, 1)]
            public float damping;
            public Gradient colors;
            public AnimationCurve remap;
            public int channelMask;
            public int loops;
            public bool accumulate;
            public List<Modificator> modificators;
            //public byte[] serializedMods;
        }
        public List<RecordSlot> recordSlots = new List<RecordSlot>();
        [HideInInspector]
        public Shader shader;
        public float taskProgress;
        public string status = "Idle";
        public string presetName;
        public BitAnimatorEditor.InterfaceMode _interfaceMode;
        void Awake()
        {
            if (animationClip == null && !String.IsNullOrEmpty(animationAssetPath))
            {
                animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animationAssetPath);
                if (animationClip == null)
                {
                    animationClip = new AnimationClip();
                    AssetDatabase.CreateAsset(animationClip, animationAssetPath);
                }
            }
        }
        public void Update()
        {
            //if(core != null)
            //core.RenderHistogram(core.texture, animationTime, multiply, 0, audioClip.frequency / 2, ampSmoothness, freqSmoothness);
            //core.RenderSpectrum(core.texture, animationTime, 4.0f, multiply, 1000, audioClip.frequency / 2, ampSmoothness, freqSmoothness);
            //core.RenderPeaks(core.texture, animationTime, 3.0f, multiply*60, 57, 149, 0, 0, 0.0f);
            /*if (core == null || core.audioClip != audioClip)
            {
                FFTWindow = 1 << FFTWindowLog;
                if (core == null)
                    core = new ComputeProgram(computeShader);
                core.Initialize(FFTWindowLog, filter, multisamples, windowParam);
                core.LoadAudio(audioClip);
                core.resolveFactor = deconvolution;
                SetMode(ref core.mode, calculateLoudness, energyCorrection);
                chunks = core.chunks;
            }
            //Создаем группу задач для вычисления значений ключевых кадров
            Task[] tasks = new Task[recordSlots.Count];
            for (int p = 0; p < recordSlots.Count; p++)
            {
                tasks[p].SetRemapCurve(recordSlots[p].minValue, recordSlots[p].maxValue, recordSlots[p].remap, recordSlots[p].channelMask);

                //Устанавливаем соотношение амплитуды спектра и результирующих значений ключевых кадров
                if (recordSlots[p].type == RecordSlot.PropertyType.Color)
                    tasks[p].SetRemapGradient(recordSlots[p].colors, recordSlots[p].channelMask);

                tasks[p].values = new ComputeBuffer(chunks, 4);
                tasks[p].buffer = new ComputeBuffer(chunks, 4);
                //Резервируем буффер для ключей XYZW|RGBA
                tasks[p].keyframes = new ComputeBuffer(chunks * tasks[p].Channels, 32);
            }

            //разделяем вычисление спектра на блоки
            int maxUseVRAM = SystemInfo.graphicsMemorySize / 10; // use 10% of total video memory
            int calculatedMemory = chunks * FFTWindow / 1024 * 21 / 1024;
            int blocks = (calculatedMemory - 1) / maxUseVRAM + 1;
            int batches = (chunks - 1) / blocks + 1;
            float[] temp = new float[1];
            for (int block = 0; block < blocks; block++)
            {
                int offset = batches * block;
                //Вычисляем спектрограмму
                core.FFT_Execute(0, offset, Math.Min(batches, chunks - 1 - offset));
                //Преобразуем спектр из комплексных чисел в частотно-амплитудный спектр
                core.ConvertToSpectrum();
                core.ResolveMultisamples();
                for (int p = 0; p < recordSlots.Count; p++)
                {
                    //Интегрируем полосу частот в 1D массив
                    core.MergeBand(recordSlots[p].startFreq, recordSlots[p].endFreq, tasks[p].values, offset);
                }
                taskProgress += 0.5f / blocks;
            }

            ComputeBuffer mask = new ComputeBuffer(chunks, 4);

            for (int p = 0; p < recordSlots.Count; p++)
            {
                core.Normalize(tasks[p].values);

                core.ApplyRemap(tasks[p].values, tasks[p].buffer, tasks[p].values.count, tasks[p].remapKeyframes);
                ComputeProgram.Swap(ref tasks[p].values, ref tasks[p].buffer);
                //сглаживаем анимацию кривой
                if (recordSlots[p].ampSmoothness > 0.0f)
                {
                    core.SmoothSpectrum(tasks[p].values, tasks[p].buffer, recordSlots[p].ampSmoothness);
                    ComputeProgram.Swap(ref tasks[p].values, ref tasks[p].buffer);
                }
                if (recordSlots[p].damping >= 0.0001f)
                {
                    core.ApplyFading(tasks[p].values, tasks[p].buffer, tasks[p].values.count, 1.0f / recordSlots[p].damping - 1.0f);
                    ComputeProgram.Swap(ref tasks[p].values, ref tasks[p].buffer);
                }
                //создание неубывающей кривой (каждый пик потихоньку сдвигает позицию к конечному значению)
                if (recordSlots[p].accumulate)
                {
                    core.CalculatePrefixSum(tasks[p].values, tasks[p].buffer, tasks[p].values.count);
                    ComputeProgram.Swap(ref tasks[p].values, ref tasks[p].buffer);
                }
                int prop = p;
                if (recordSlots[prop].modificators.Count > 0)
                {
                    core.Multiply(mask, 0, mask.count, 0.0f);
                    foreach (Modificator mod in recordSlots[prop].modificators)
                    {
                        mod.Apply(core, mask, tasks[prop].buffer);
                        if (mod.useTempBuffer)
                            ComputeProgram.Swap(ref mask, ref tasks[prop].buffer);
                    }
                    core.Multiply(tasks[prop].values, mask);
                }

                core.Normalize(tasks[p].values);

                core.Multiply(tasks[p].values, 0, tasks[p].values.count, multiply);

                if (recordSlots[p].type == RecordSlot.PropertyType.Color)
                    core.RemapGradient(tasks[p].values, tasks[p].keyframes, tasks[p].gradientKeyframes, tasks[p].Channels, tasks[p].gradientKeys, recordSlots[p].loops);
                else
                    core.CreateKeyframes(tasks[p].values, tasks[p].keyframes, tasks[p].min, tasks[p].max, tasks[p].Channels, recordSlots[p].loops);
                //перевод углов Эйлера в кватернионы
                if (recordSlots[p].type == RecordSlot.PropertyType.Quaternion)
                    core.ConvertKeyframesRotation(tasks[p].keyframes, tasks[p].values.count);
            }
            mask.Dispose();
            for (int p = 0; p < recordSlots.Count; p++)
            {
                tasks[p].Release();
            }*/
        }
        void OnDestroy()
        {
            if (core != null) core.Release();
        }
        public void Setup()
        {
            FFTWindow = 1 << FFTWindowLog;
            if (core == null)
                core = new ComputeProgram(computeShader);

            core.Initialize(FFTWindowLog, filter, multisamples, windowParam);
            if (core.audioClip != audioClip)
                core.LoadAudio(audioClip);

            SetMode(ref core.mode, calculateLoudness, energyCorrection);
            chunks = core.chunks;
            core.resolveFactor = deconvolution;
        }
        public static void SetMode(ref ComputeProgram.VisualizationMode state, bool CalculateFons, bool EnergyCorrection)
        {
            state &= ~ComputeProgram.VisualizationMode.CalculateFons;
            state &= ~ComputeProgram.VisualizationMode.EnergyCorrection;
            state |= CalculateFons ? ComputeProgram.VisualizationMode.CalculateFons : 0;
            state |= EnergyCorrection ? ComputeProgram.VisualizationMode.EnergyCorrection : 0;
        }
        public static string GetRelativeHierarchyPath(Transform rootTransform, Transform targetTransform)
        {
            string returnName = targetTransform.name;
            Transform tempObj = targetTransform;

            // it is the root transform
            if (tempObj == rootTransform)
                return "";

            while (tempObj.parent != rootTransform)
            {
                returnName = tempObj.parent.name + "/" + returnName;
                tempObj = tempObj.parent;
            }

            return returnName;
        }

        void ResizeSpectrum(int i, int j)
        {
            if (spectrumMap == null || spectrumMap.Length != i * j)
                spectrumMap = new float[i * j];

            chunks = i;
            bins = j;
        }
        [Obsolete("Legacy single thread compute. Will be removed in next version. Use ComputeKernels functions. Use ComputeKernels functions")]
        static byte[] GetBytes(float[] values)
        {
            var result = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, result, 0, result.Length);
            return result;
        }
        [Obsolete("Legacy single thread compute. Will be removed in next version. Use ComputeKernels functions")]
        private void SaveCache()
        {
            using (BinaryWriter cache = new BinaryWriter(File.Open(spectrumCacheFilename, FileMode.Create)))
            {
                cache.Write(chunks);
                cache.Write(bins);
                cache.Write(GetBytes(spectrumMap));
            }
        }
        [Obsolete("Legacy single thread compute. Will be removed in next version. Use ComputeKernels functions")]
        private bool LoadCache()
        {
            if (!File.Exists(spectrumCacheFilename))
                return false;
            using (BinaryReader cache = new BinaryReader(File.Open(spectrumCacheFilename, FileMode.Open)))
            {
                chunks = cache.ReadInt32();
                bins = cache.ReadInt32();
                byte[] data = cache.ReadBytes(chunks * bins * sizeof(float));
                ResizeSpectrum(chunks, bins);
                Buffer.BlockCopy(data, 0, spectrumMap, 0, chunks * bins * sizeof(float));
                //for (int i = 0; i < spectrumMap.Length; i++) 
                //	spectrumMap[i] = BitConverter.ToSingle(data, i * sizeof(float));
            }
            return true;
        }
        public Type GetCurveAnimationType(RecordSlot.PropertiesSet set)
        {
            switch (set)
            {
                case RecordSlot.PropertiesSet.Material:
                    return targetObject.GetComponent<Renderer>().GetType();
                case RecordSlot.PropertiesSet.ParticleSystem:
                    return typeof(ParticleSystem);
                case RecordSlot.PropertiesSet.BlendShape:
                    return typeof(SkinnedMeshRenderer);
                case RecordSlot.PropertiesSet.Transform:
                    return typeof(Transform);
            }
            return null;
        }
        [Obsolete("Legacy single thread compute. Will be removed in next version. Use ComputeKernels functions")]
        public void LoadSpectrum()
        {
            spectrumCacheFilename = string.Format("{0}_{1}{2}{3}{4}{5}.spectrumCache", Path.Combine(Application.temporaryCachePath, audioClip.name), FFTWindow, (int)filter, multisamples, calculateLoudness, normalizeSpectrum);
            if (recalculateSpectrum && File.Exists(spectrumCacheFilename))
                FileUtil.DeleteFileOrDirectory(spectrumCacheFilename);
            if (recalculateSpectrum || spectrumMap == null && LoadCache() == false)
            {
                calculateSpectrum();
                SaveCache();
                recalculateSpectrum = false;
            }
        }
        public IEnumerator ComputeAnimation(AnimationClip clip, Action<BitAnimator> finishCallback = null, Action<BitAnimator> updateCallback = null)
        {
            float time = Time.realtimeSinceStartup;
            if (core == null || core.audioClip != audioClip)
            {
                status = "Loading audio";
                if(updateCallback != null) updateCallback(this);
                yield return null;

                FFTWindow = 1 << FFTWindowLog;
                if(core == null)
                    core = new ComputeProgram(computeShader);
                core.Initialize(FFTWindowLog, filter, multisamples, windowParam);
                core.LoadAudio(audioClip);
                core.resolveFactor = deconvolution;
                SetMode(ref core.mode, calculateLoudness, energyCorrection);
                chunks = core.chunks;
                Debug.LogFormat("[BitAnimator] Setup time = {0:F3}", Time.realtimeSinceStartup - time);
                time = Time.realtimeSinceStartup;
            }
            if (animatorObject == null)
                animatorObject = GetComponent<Animator>();
            if (targetObject == null)
                targetObject = gameObject;

            //Создаем группу задач для вычисления значений ключевых кадров
            Task[] tasks = new Task[recordSlots.Count];
            for (int p = 0; p < recordSlots.Count; p++)
            {
                tasks[p].SetRemapCurve(recordSlots[p].minValue, recordSlots[p].maxValue, recordSlots[p].remap, recordSlots[p].channelMask);

                //Устанавливаем соотношение амплитуды спектра и результирующих значений ключевых кадров
                if (recordSlots[p].type == RecordSlot.PropertyType.Color)
                    tasks[p].SetRemapGradient(recordSlots[p].colors, recordSlots[p].channelMask);

                tasks[p].values = new ComputeBuffer(chunks, 4);
                tasks[p].buffer = new ComputeBuffer(chunks, 4);
                //Резервируем буффер для ключей XYZW|RGBA
                tasks[p].keyframes = new ComputeBuffer(chunks * tasks[p].Channels, 32);
            }
            status = "Calculating spectrum";
            if (updateCallback != null) updateCallback(this);
            yield return null;

            //разделяем вычисление спектра на блоки
            int maxUseVRAM = SystemInfo.graphicsMemorySize / 10; // use 10% of total video memory
            int calculatedMemory = chunks * FFTWindow / 1024 * 21 / 1024;
            int blocks = (calculatedMemory - 1) / maxUseVRAM + 1;
            int batches = (chunks - 1) / blocks + 1;
            float[] temp = new float[1];
            for (int block = 0; block < blocks; block++)
            {
                int offset = batches * block;
                //Вычисляем спектрограмму
                core.FFT_Execute(0, offset, Math.Min(batches, chunks - 1 - offset));
                //Преобразуем спектр из комплексных чисел в частотно-амплитудный спектр
                core.ConvertToSpectrum();
                core.ResolveMultisamples();
                for (int p = 0; p < recordSlots.Count; p++)
                {
                    //Интегрируем полосу частот в 1D массив
                    core.MergeBand(recordSlots[p].startFreq, recordSlots[p].endFreq, tasks[p].values, offset);
                }
                taskProgress += 0.5f / blocks;
                if (updateCallback != null) updateCallback(this);
                yield return null;
            }

            Debug.LogFormat("[BitAnimator] Calculating spectrum time = {0:F3}", Time.realtimeSinceStartup - time);
            time = Time.realtimeSinceStartup;

            ComputeBuffer mask = new ComputeBuffer(chunks, 4);

            for (int p = 0; p < recordSlots.Count; p++)
            {    
                core.Normalize(tasks[p].values);

                core.ApplyRemap(tasks[p].values, tasks[p].buffer, tasks[p].values.count, tasks[p].remapKeyframes);
                ComputeProgram.Swap(ref tasks[p].values, ref tasks[p].buffer);
                //сглаживаем анимацию кривой
                if (recordSlots[p].ampSmoothness > 0.0f)
                {
                    core.SmoothSpectrum(tasks[p].values, tasks[p].buffer, recordSlots[p].ampSmoothness);
                    ComputeProgram.Swap(ref tasks[p].values, ref tasks[p].buffer);
                }
                if (recordSlots[p].damping >= 0.0001f)
                {
                    core.ApplyFading(tasks[p].values, tasks[p].buffer, tasks[p].values.count, 1.0f / recordSlots[p].damping - 1.0f);
                    ComputeProgram.Swap(ref tasks[p].values, ref tasks[p].buffer);
                }
                //создание неубывающей кривой (каждый пик потихоньку сдвигает позицию к конечному значению)
                if (recordSlots[p].accumulate)
                {
                    core.CalculatePrefixSum(tasks[p].values, tasks[p].buffer, tasks[p].values.count);
                    ComputeProgram.Swap(ref tasks[p].values, ref tasks[p].buffer);
                }
                int prop = p;
                if (recordSlots[prop].modificators.Count > 0)
                {
                    core.Multiply(mask, 0, mask.count, 0.0f);
                    foreach (Modificator mod in recordSlots[prop].modificators)
                    {
                        mod.Apply(core, mask, tasks[prop].buffer);
                        if (mod.useTempBuffer)
                            ComputeProgram.Swap(ref mask, ref tasks[prop].buffer);
                    }
                    core.Multiply(tasks[prop].values, mask);
                }

                core.Normalize(tasks[p].values);

                core.Multiply(tasks[p].values, 0, tasks[p].values.count, multiply);

                if (recordSlots[p].type == RecordSlot.PropertyType.Color)
                    core.RemapGradient(tasks[p].values, tasks[p].keyframes, tasks[p].gradientKeyframes, tasks[p].Channels, tasks[p].gradientKeys, recordSlots[p].loops);
                else
                    core.CreateKeyframes(tasks[p].values, tasks[p].keyframes, tasks[p].min, tasks[p].max, tasks[p].Channels, recordSlots[p].loops);
                //перевод углов Эйлера в кватернионы
                if (recordSlots[p].type == RecordSlot.PropertyType.Quaternion)
                    core.ConvertKeyframesRotation(tasks[p].keyframes, tasks[p].values.count);
            }
            mask.Dispose();
            tasks[0].values.GetData(temp);
            status = quality < 1.0f ? "Optimization keyframes" : "Writing animation";
            if (updateCallback != null) updateCallback(this);
            yield return null;

            string go_path = AnimationUtility.CalculateTransformPath(targetObject.transform, animatorObject.transform);
            //Получаем результат с GPU и записываем в анимацию
            for (int p = 0; p < recordSlots.Count; p++)
            {
                Type renderType = GetCurveAnimationType(recordSlots[p].typeSet);
                string[] animationProperties = recordSlots[p].property;
                for (int c = 0; c < animationProperties.Length; c++)
                {
                    yield return null;
                    if (updateCallback != null) updateCallback(this);
                    AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, audioClip.length, 0);
                    Keyframe[] keyframes = tasks[p].GetKeyframes(c);

                    if (quality < 1.0f)
                    {
                        float step = 1.0f / 32.0f;
                        float partsTaskProgress = 0;
                        float lastFrame = Time.realtimeSinceStartup;
                        float rmsQuality = Mathf.Pow(10.0f, -6.0f * quality * quality - 1.0f); //quality to RMS   [0..1] => [1e-1 .. 1e-7]
                        foreach (Keyframe[] k in core.DecimateAnimationCurve(keyframes, rmsQuality * (tasks[p].max[c] - tasks[p].min[c])))
                        {
                            partsTaskProgress += step;
                            taskProgress += 0.5f * step / recordSlots.Count / animationProperties.Length;
                            //Делаем паузу если уходит много времени на обработку
                            if (k == null && Time.realtimeSinceStartup - lastFrame >= Time.fixedUnscaledDeltaTime)
                            {
                                lastFrame = Time.realtimeSinceStartup;
                                if (updateCallback != null) updateCallback(this);
                                yield return null;
                            }
                            else
                                keyframes = k;
                        }
                        taskProgress += 0.5f * Mathf.Max(0, 1.0f - partsTaskProgress) / recordSlots.Count / animationProperties.Length;
                    }
                    else
                        taskProgress += 0.5f / recordSlots.Count / animationProperties.Length;

                    curve.keys = keyframes;
                    clip.SetCurve(go_path, renderType, animationProperties[c], curve);
                }
                tasks[p].Release();
            }
            Debug.LogFormat("[BitAnimator] Writing animation time = {0:F3}", Time.realtimeSinceStartup - time);
            status = "Done";
            taskProgress = 0;
            yield return null;
            if (finishCallback != null)
                finishCallback(this);
        }
        public IEnumerator CreateAnimation(Action<BitAnimator> finishCallback = null, Action<BitAnimator> updateCallback = null)
        {
            if (animationClip == null)
            //if (string.IsNullOrEmpty(animationAssetPath))
                yield break;

            if (animatorObject == null)
                animatorObject = GetComponent<Animator>();
            if (targetObject == null)
                targetObject = gameObject;

            //Декодируем аудио и загружаем в GPU
            Setup();
            /*
            string clipFile = animationAssetPath;
            animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipFile);
            if (animationClip == null)
            {
                animationClip = new AnimationClip();
                AssetDatabase.CreateAsset(animationClip, clipFile);
            }*/
            animationClip.frameRate = Mathf.Max(animationClip.frameRate, audioClip.frequency / (float)FFTWindow * multisamples);
            IEnumerator i = ComputeAnimation(animationClip, null, updateCallback);
            while (i.MoveNext())
                yield return null;
            AssetDatabase.SaveAssets();
            if(updateCallback != null) updateCallback(this);
            yield return null;
            AssetDatabase.Refresh();
            if (finishCallback != null)
                finishCallback(this);
        }
        public void ClearAnimation()
        {
            //string clipFile = animationAssetPath;
            //animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipFile);
            /*if (animationClip != null)
            {*/
                animationClip.ClearCurves();
                animationClip.frameRate = 1;
                AssetDatabase.SaveAssets();
            //}
        }

        /*delegate void TestDelegate(int end);
        public float[,] getFullSpectrum()
        {
            //SystemInfo.processorCount;
            Thread bgThread = new Thread (task);
            bgThread.Start (iterations);
        }*/
        //Algorithmic Beat Mapping in Unity: Preprocessed Audio Analysis
        //https://medium.com/giant-scam/algorithmic-beat-mapping-in-unity-preprocessed-audio-analysis-d41c339c135a
        [Obsolete("Legacy single thread compute. Will be removed in next version. Use ComputeKernels functions")]
        public void calculateSpectrum()
        {
            float watch = Time.realtimeSinceStartup;
            float[] preProcessedSamples = LoadMonoSound();

            // Once we have our audio sample data prepared, we can execute an FFT to return the spectrum data over the time domain
            FFTWindow = (int)Math.Pow(2, FFTWindowLog);
            ResizeSpectrum(preProcessedSamples.Length / FFTWindow * multisamples - (multisamples - 1), FFTWindow / 2);
            //float maximum = 0;

            FFT fft = new FFT();
            fft.Initialize((uint)FFTWindow);
            float[] windowCoefs = DSP.Window.Coefficients(filter, (uint)FFTWindow);
            float scaleFactor = DSP.Window.ScaleFactor.Signal(windowCoefs);
            //Debug.Log(string.Format("Processing {0} time domain samples for FFT", chunks));
            float[] sampleChunk = new float[FFTWindow];
            for (int i = 0; i < chunks; i++)
            {
                // Grab the current 1024 chunk of audio sample data
                Array.Copy(preProcessedSamples, i * FFTWindow / multisamples, sampleChunk, 0, FFTWindow);

                // Apply our chosen FFT Window
                float[] scaledSpectrumChunk = DSP.Math.Multiply(sampleChunk, windowCoefs);

                // Perform the FFT and convert output (complex numbers) to Magnitude
                System.Numerics.Complex[] fftSpectrum = fft.Execute(scaledSpectrumChunk);
                float[] scaledFFTSpectrum = DSP.ConvertComplex.ToMagnitude(fftSpectrum);
                scaledFFTSpectrum = DSP.Math.Multiply(scaledFFTSpectrum, scaleFactor);

                // These 1024 magnitude values correspond (roughly) to a single point in the audio timeline
                //float curSongTime = getTimeFromIndex(i) * FFTWindow / multisamples;

                //Spectrum energy correction
                float hzPerBin = audioClip.frequency / FFTWindow;
                /*for (int x = 0; x < scaledFFTSpectrum.Length; x++)
                scaledFFTSpectrum[x] *= (x + 0.0f)*hzPerBin;*/
                //scaledFFTSpectrum[x] = Math.Max(0.0, 20.0 + Math.Max(-20.0, Math.Log(scaledFFTSpectrum[x], 2.0)));

                for (int x = 0; x < bins; x++)
                {
                    float rawValue = scaledFFTSpectrum[x + 1];//spectrum[0] - DC component (offset)
                    float fons = calculateLoudness ? getFons((x + 1) * hzPerBin, rawValue) : rawValue;
                    //maximum = Mathf.Max (maximum, fons);
                    spectrumMap[i * bins + x] = fons;
                }

                // Send our magnitude data off to our Spectral Flux Analyzer to be analyzed for peaks
                //preProcessedSpectralFluxAnalyzer.analyzeSpectrum(Array.ConvertAll(scaledFFTSpectrum, x => (float)x), curSongTime);
            }
            Debug.Log("FFT execution time = " + (Time.realtimeSinceStartup - watch) + " s.");
        }
        [Obsolete("Legacy single thread compute. Will be removed in next version. Use ComputeKernels functions")]
        public float[] LoadMonoSound(bool zeroPadding = false)
        {
            // Need all audio samples.  If in stereo, samples will return with left and right channels interweaved
            // [L,R,L,R,L,R]
            float[] multiChannelSamples = new float[audioClip.samples * audioClip.channels];
            audioClip.LoadAudioData();
            audioClip.GetData(multiChannelSamples, 0);

            // We only need to retain the samples for combined channels over the time domain
            int samples = audioClip.samples;
            if (zeroPadding)
                samples += -audioClip.samples & FFTWindow - 1;
            float[] preProcessedSamples = new float[samples];
            int numProcessed = 0;
            float combinedChannelAverage = 0f;
            for (int i = 0; i < multiChannelSamples.Length; i++)
            {
                combinedChannelAverage += multiChannelSamples[i];

                // Each time we have processed all channels samples for a point in time, we will store the average of the channels combined
                if ((i + 1) % audioClip.channels == 0)
                {
                    preProcessedSamples[numProcessed] = combinedChannelAverage / audioClip.channels;
                    numProcessed++;
                    combinedChannelAverage = 0f;
                }
            }
            //Debug.Log("Combine Channels done");
            //Debug.Log(preProcessedSamples.Length);
            return preProcessedSamples;
        }
        /*public void analize()
        {
            int indexToAnalyze = getIndexFromTime(3.6f) / 1024;
            for (int i = indexToAnalyze - 30; i <= indexToAnalyze + 30; i++)
            {
                SpectralFluxInfo sfSample = preProcessedSpectralFluxAnalyzer.spectralFluxSamples[i];
                Debug.Log(string.Format("Index {0} : Time {1} Pruned Spectral Flux {2} : Is Peak {3}", i, getTimeFromIndex(i) * FFTWindow, sfSample.prunedSpectralFlux, sfSAmple.isPeak));
            }
        }*/
        public int getIndexFromTime(float curTime)
        {
            float lengthPerSample = audioClip.length / audioClip.samples;
            return Mathf.FloorToInt(curTime / lengthPerSample);
        }

        public float getTimeFromIndex(int index)
        {
            return 1f / audioClip.frequency * index;
        }
        public float getTimeFromChunk(int index)
        {
            return 1f / audioClip.frequency * index * FFTWindow / multisamples;
        }

        //Acoustics — Normal equal-loudness-level contours
        //http://libnorm.ru/Files2/1/4293820/4293820821.pdf

        //  Hz, Alpha_f, Lu, Tf
        private static float[] isofons = {
        20f, 0.532f, -31.6f, 78.5f,
        25f, 0.506f, -27.2f, 68.7f,
        31.5f, 0.480f, -23.0f, 59.5f,
        40f, 0.455f, -19.1f, 51.1f,
        50f, 0.432f, -15.9f, 44.0f,
        63f, 0.409f, -13.0f, 37.5f,
        80f, 0.387f, -10.3f, 31.5f,
        100f, 0.367f, -8.1f, 26.5f,
        125f, 0.349f, -6.2f, 22.1f,
        160f, 0.330f, -4.5f, 17.9f,
        200f, 0.315f, -3.1f, 14.4f,
        250f, 0.301f, -2.0f, 11.4f,
        315f, 0.288f, -1.1f, 8.6f,
        400f, 0.276f, -0.4f, 6.2f,
        500f, 0.267f, 0.0f, 4.4f,
        630f, 0.259f, 0.3f, 3.0f,
        800f, 0.253f, 0.5f, 2.2f,
        1000f, 0.250f, 0.0f, 2.4f,
        1250f, 0.246f, -2.7f, 3.5f,
        1600f, 0.244f, -4.1f, 1.7f,
        2000f, 0.243f, -1.0f, -1.3f,
        2500f, 0.243f, 1.7f, -4.2f,
        3150f, 0.243f, 2.5f, -6.0f,
        4000f, 0.242f, 1.2f, -5.4f,
        5000f, 0.242f, -2.1f, -1.5f,
        6300f, 0.245f, -7.1f, 6.0f,
        8000f, 0.254f, -11.2f, 12.6f,
        10000f, 0.271f, -10.7f, 13.9f,
        12500f, 0.301f, -3.1f, 12.3f
    };

        private static float[] Hz_data = null;
        [Obsolete("Legacy single thread compute. Will be removed in next version. Use ComputeKernels functions")]
        public float getFons(float hz, float Lp)
        {
            Lp = Mathf.Max(0.0f, Mathf.Log10(Lp) * 10.0f + 94.0f);

            int count = isofons.Length / 4;
            if (Hz_data == null)
            {
                Hz_data = new float[count];
                for (int i = 0; i < count; i++)
                    Hz_data[i] = isofons[i * 4];
            }
            int idx2 = Array.BinarySearch(Hz_data, hz);
            if (idx2 < 0)
                idx2 = ~idx2;

            int idx = Mathf.Max(idx2 - 1, 0);
            idx2 = Mathf.Min(idx2, count - 1);
            float w = idx != idx2 ? (hz - Hz_data[idx]) / (Hz_data[idx2] - Hz_data[idx]) : 0;
            idx *= 4; idx2 *= 4;
            float Alpha_f = Mathf.Lerp(isofons[idx + 1], isofons[idx2 + 1], w);
            float Lu = Mathf.Lerp(isofons[idx + 2], isofons[idx2 + 2], w);
            float Tf = Mathf.Lerp(isofons[idx + 3], isofons[idx2 + 3], w);
            //Convert dB to Fons
            float Bf = Mathf.Pow(0.4f * Mathf.Pow(10.0f, (Lp + Lu) * 0.1f - 9), Alpha_f) - Mathf.Pow(0.4f * Mathf.Pow(10.0f, (Tf + Lu) * 0.1f - 9), Alpha_f) + 0.005135f;
            //float Ln = 40.0f*Mathf.Log10 (Bf) + 94.0f;
            //convert Fons to dB
            //float Af = 0.00447f * (Mathf.Pow (10.0f, 0.025f * Ln) - 1.15f) + Mathf.Pow (0.4f * Mathf.Pow (10f, 0.1f * (Tf + Lu) - 9.0f), Alpha_f);
            //Af = Mathf.Max (Af, 0);
            //Lp = 10.0f / Alpha_f * Mathf.Log10 (Af) - Lu + 94.0f;

            //return Mathf.Pow (10.0f, (Ln - 94.0f)*0.1f);
            return Bf * Bf * Bf * Bf; //optimazed calculatation
        }

        [ContextMenu("Generte LUT")]
        public void GenerateLUT()
        {
            Texture2D tex = new Texture2D(1024, 1024, TextureFormat.RGBAFloat, false);
            for (int y = 0; y < 1024; y++)
                for (int x = 0; x < 1024; x++)
                {
                    Color color = Color.white * getFons(Mathf.Pow(2, 8 * x / 1023f) / (Mathf.Pow(2, 8) - 1) * 24000f, Mathf.Pow(2, 8 * y / 1023f) / (Mathf.Pow(2, 8) - 1));
                    color.a = 1.0f;
                    tex.SetPixel(x, y, color);
                }
            byte[] bytes = tex.EncodeToEXR();
            DestroyImmediate(tex);

            if (bytes != null)
            {
                File.WriteAllBytes("Assets/LUT.exr", bytes);
                AssetDatabase.Refresh();
            }
        }
        public ComputeShader computeShader;
        public RenderTexture computeShaderResult;
        public struct Task
        {
            public ComputeBuffer values;
            public ComputeBuffer buffer;
            public ComputeBuffer keyframes;
            public ComputeBuffer remapKeyframes;
            public ComputeBuffer gradientKeyframes;
            public Vector4 min;
            public Vector4 max;
            public int channelMask;
            public int gradientKeys;
            public int Channels
            {
                get
                {
                    return (channelMask & 1) + (channelMask >> 1 & 1) + (channelMask >> 2 & 1) + (channelMask >> 3 & 1);
                }
            }
            public void SetRemapCurve(Vector4 min, Vector4 max, AnimationCurve curve, int mask)
            {
                remapKeyframes = new ComputeBuffer(curve.length, 32);
                remapKeyframes.SetData(curve.keys);
                this.min = min;
                this.max = max;
                //channelMask = mask;
                channelMask = 0xF;
                gradientKeys = 0;
            }
            public void SetRemapGradient(Gradient gradient, int mask)
            {
                Keyframe[] ks = new Keyframe[gradient.colorKeys.Length * 3 + gradient.alphaKeys.Length];
                int offset = 0;
                for (int c = 0; c < 3; c++)
                {
                    offset = c * gradient.colorKeys.Length;
                    for (int i = 0; i < gradient.colorKeys.Length; i++)
                    {
                        ks[offset + i].time = gradient.colorKeys[i].time;
                        ks[offset + i].value = gradient.colorKeys[i].color[c];
                    }
                }
                offset = 3 * gradient.colorKeys.Length;
                for (int i = 0; i < gradient.alphaKeys.Length; i++)
                {
                    ks[offset + i].time = gradient.alphaKeys[i].time;
                    ks[offset + i].value = gradient.alphaKeys[i].alpha;
                }
                gradientKeyframes = new ComputeBuffer(ks.Length, 32);
                gradientKeyframes.SetData(ks);
                min = Vector4.zero;
                max = Vector4.one;
                //channelMask = mask;
                channelMask = 0xF;
                gradientKeys = gradient.colorKeys.Length;
            }
            public Keyframe[] GetKeyframes(int channel)
            {
                int count = keyframes.count / Channels;
                Keyframe[] result = new Keyframe[count];
                keyframes.GetData(result, 0, channel * count, count);
                return result;
            }

            internal void Release()
            {
                if (values != null) values.Dispose();
                if (buffer != null) buffer.Dispose();
                if (keyframes != null) keyframes.Dispose();
                if (remapKeyframes != null) remapKeyframes.Dispose();
            }
        }

        double RMSD(float[] src, float[] dst)
        {
            double dist = 0;
            for (int i = 0; i < src.Length; i++)
            {
                double v = src[i] - dst[i];
                dist += v * v;
            }
            return Math.Sqrt(dist) / src.Length;
            //return Mathf.Sqrt(dist / src.Length);
        }
        public class RuntimeBinding
        {
            public GameObject target;
            public RecordSlot.PropertyType type;
            public int channelMask;
            public Vector2Int freqBand;
            public virtual void EvaluatePropertyValue(float value)
            {

            }
        }
        public class ParticleSystemBind : RuntimeBinding
        {
            public struct Field
            {
                public string path;
                public string property;
                public RecordSlot.PropertyType type;
                public bool separateAxis;
                public int mainAxis;
            }
            public ParticleSystem particleSystem;
            public Field field;
        }
        public class MaterialBind : RuntimeBinding
        {
            public struct Field
            {
                public string property;
                public RecordSlot.PropertyType type;
            }
            public Material material;
            public Field field;
        }
        public class ShapeBind : RuntimeBinding
        {
            public SkinnedMeshRenderer mesh;
            public string shape;
            public float min, max;
            int index;
            public ShapeBind(SkinnedMeshRenderer skinnedMesh, string _shape)
            {
                mesh = skinnedMesh;
                shape = _shape;
                index = mesh.sharedMesh.GetBlendShapeIndex(shape);
            }
            public override void EvaluatePropertyValue(float value)
            {
                mesh.SetBlendShapeWeight(index, value);
            }
        }
        public class TransformBind : RuntimeBinding
        {
            public enum Field
            {
                Position, Rotation, Scale
            }
            Vector3 min, max;
            public Field field;
            public override void EvaluatePropertyValue(float value)
            {
                switch (field)
                {
                    case Field.Position:
                        target.transform.localPosition = Vector3.LerpUnclamped(min, max, value); break;
                    case Field.Rotation:
                        target.transform.localRotation = Quaternion.Euler(Vector3.LerpUnclamped(min, max, value)); break;
                    case Field.Scale:
                        target.transform.localScale = Vector3.LerpUnclamped(min, max, value); break;
                }
            }
        }
        public static float ToLogFrequency(float normalizedFrequency)
        {
            return normalizedFrequency * Mathf.Pow(2.0f, 6.9f * (normalizedFrequency - 1.0f));
        }
        public static float FromLogFrequency(float x)
        {
            return 0.209086f * LambertW(571.191f * x);
        }
        public static float LambertW(float x, float prec = 1e-6f)
        {
            float w = 0;
            float wTimesExpW = 0;
            float wPlusOneTimesExpW = 0;
            for (int i = 0; i < 64; i++)
            {
                wTimesExpW = w * Mathf.Exp(w);
                wPlusOneTimesExpW = (w + 1) * Mathf.Exp(w);
                w -= (wTimesExpW - x) / (wPlusOneTimesExpW - (w + 2) * (wTimesExpW - x) / (2 * w + 2));
                if (prec > Mathf.Abs((x - wTimesExpW) / wPlusOneTimesExpW))
                    break;
            }
            if (prec <= Mathf.Abs((x - wTimesExpW) / wPlusOneTimesExpW))
                throw new ArithmeticException("W(x) не сходится достаточно быстро при x = " + x);
            return w;
        }

        /*float calculateRectifiedSpectralFlux()
        {
            float sum = 0f;

            // Aggregate positive changes in spectrum data
            for (int i = 0; i < numSamples; i++)
            {
                sum += Mathf.Max(0f, curSpectrum[i] - prevSpectrum[i]);
            }
            return sum;
        }
        float getFluxThreshold(int spectralFluxIndex)
        {
            // How many samples in the past and future we include in our average
            int windowStartIndex = Mathf.Max(0, spectralFluxIndex - thresholdWindowSize / 2);
            int windowEndIndex = Mathf.Min(spectralFluxSamples.Count - 1, spectralFluxIndex + thresholdWindowSize / 2);

            // Add up our spectral flux over the window
            float sum = 0f;
            for (int i = windowStartIndex; i < windowEndIndex; i++)
            {
                sum += spectralFluxSamples[i].spectralFlux;
            }

            // Return the average multiplied by our sensitivity multiplier
            float avg = sum / (windowEndIndex - windowStartIndex);
            return avg * thresholdMultiplier;
        }
        float getPrunedSpectralFlux(int spectralFluxIndex)
        {
            return Mathf.Max(0f, spectralFluxSamples[spectralFluxIndex].spectralFlux - spectralFluxSamples[spectralFluxIndex].threshold);
        }
        bool isPeak(int spectralFluxIndex)
        {
            if (spectralFluxSamples[spectralFluxIndex].prunedSpectralFlux > spectralFluxSamples[spectralFluxIndex + 1].prunedSpectralFlux &&
                spectralFluxSamples[spectralFluxIndex].prunedSpectralFlux > spectralFluxSamples[spectralFluxIndex - 1].prunedSpectralFlux)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public void analyzeSpectrum(float[] spectrum, float time)
        {
            // Set spectrum
            setCurSpectrum(spectrum);

            // Get current spectral flux from spectrum
            SpectralFluxInfo curInfo = new SpectralFluxInfo();
            curInfo.time = time;
            curInfo.spectralFlux = calculateRectifiedSpectralFlux();
            spectralFluxSamples.Add(curInfo);

            // We have enough samples to detect a peak
            if (spectralFluxSamples.Count >= thresholdWindowSize)
            {
                // Get Flux threshold of time window surrounding index to process
                spectralFluxSamples[indexToProcess].threshold = getFluxThreshold(indexToProcess);

                // Only keep amp amount above threshold to allow peak filtering
                spectralFluxSamples[indexToProcess].prunedSpectralFlux = getPrunedSpectralFlux(indexToProcess);

                // Now that we are processed at n, n-1 has neighbors (n-2, n) to determine peak
                int indexToDetectPeak = indexToProcess - 1;

                bool curPeak = isPeak(indexToDetectPeak);

                if (curPeak)
                {
                    spectralFluxSamples[indexToDetectPeak].isPeak = true;
                }
                indexToProcess++;
            }
            else
            {
                Debug.Log(string.Format("Not ready yet.  At spectral flux sample size of {0} growing to {1}", spectralFluxSamples.Count, thresholdWindowSize));
            }
        }*/
    }
}
#endif

