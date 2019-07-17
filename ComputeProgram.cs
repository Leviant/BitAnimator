
// Copyright © 2019 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.0 (17.07.2019)

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using DSPLib;

namespace AudioVisualization
{
    public class ComputeProgram
    {
        const int MaxMultisamples = 32;
        #region Variables
        public static int cs_GridOffset;
        public static int cs_GridSize;
        public static int cs_Input;
        public static int cs_Input2;
        public static int cs_Output;
        public static int cs_Keyframes;
        public static int cs_OutputKeyframes;
        public static int cs_MinimumValues;
        public static int cs_MaximumValues;
        public static int cs_Window;
        public static int cs_buffer;
        public static int cs_FFTWindow;
        public static int cs_Channels;
        public static int cs_Frequency;
        public static int cs_Source;
        public static int cs_Multisamples;
        public static int cs_SampleStep;
        public static int cs_BufferStep;
        public static int cs_N;
        public static int cs_Scale;
        public static int cs_Time;
        public static int cs_RenderTexture;
        public static int cs_Lut;
        public static int cs_VolumeLUT;

        readonly ComputeShader computeShader;
        ComputeBuffer soundInput;
        ComputeBuffer monoSound;
        ComputeBuffer window;

        public ComputeBuffer tempBuffer;
        public ComputeBuffer tempBufferSmall;
        public ComputeBuffer buffer;
        public ComputeBuffer buffer2;
        public ComputeBuffer output;
        public ComputeBuffer keyframes;
        ComputeBuffer normalizeBuffer;
        ComputeBuffer finalSumBuffer;
        ComputeBuffer renderBuffer;
        ComputeBuffer deconvolutionBuffer;
        public RenderTexture lut;
        public Texture2D curveLUT;


        public RenderTexture texture;

        public AudioClip audioClip;
        int FFTWindow;
        int RealWindow;
        int FFTWindowLog;
        int multisamples;
        public int chunks;
        int bufferSwap;
        public int spectrumChunks;
        public float windowScale;
        public float resolveFactor;
        public float resolveCoeff;
        public VisualizationMode mode;
        DSP.Window.Type filter;

        readonly Kernel CopyBuffer;
        readonly Kernel MergeChannels2;
        readonly Kernel MergeChannelsN;
        readonly Kernel SaveTexture;
        readonly Kernel Derivative;
        readonly Kernel PowerKernel;
        readonly Kernel Transpose;
        readonly Kernel ReductionSum;
        readonly Kernel FinalSum;
        readonly Kernel ReductionMax;
        readonly Kernel FinalMax;
        readonly Kernel MultiplyKernel;
        readonly Kernel MultiplyBuffers;
        readonly Kernel DivideKernel;
        readonly Kernel PartialSumBig;
        readonly Kernel PartialSumSmall;
        readonly Kernel PrefixSum;
        readonly Kernel PrefixSumLocal;
        readonly Kernel DampingKernel;
        readonly Kernel CreateWindow;
        readonly Kernel FFT_Init;
        readonly Kernel FFT_Part;
        readonly Kernel FFT;
        readonly Kernel IFFT;
        readonly Kernel IFFT_Part;
        readonly Kernel DFT_Execute;
        readonly Kernel AbsSpectrum;
        readonly Kernel CalculateFons;
        readonly Kernel SpectrumLinearToLog;
        readonly Kernel AmplitudeSmooth;
        readonly Kernel FrequencySmooth;
        readonly Kernel DrawHistogram;
        readonly Kernel RenderBars;
        readonly Kernel RenderSpectrumKernel;
        readonly Kernel Precompute_Loudness_LUT;
        readonly Kernel CreateFonLUT;
        readonly Kernel DrawHistogramVolume;
        readonly Kernel DFT_BPM;
        readonly Kernel GetPeaks;
        readonly Kernel BeatFinder;
        readonly Kernel VisualizeBPM;
        readonly Kernel Resample;
        readonly Kernel FillKeyframes;
        readonly Kernel DecimateKeyframesKernel;
        readonly Kernel CurveFilter;
        readonly Kernel RemapKernel;
        readonly Kernel KeyframesCreator;
        readonly Kernel RemapGradientKernel;
        readonly Kernel ConvertToQuaternions;
        readonly Kernel DolphChebyshevWindow;
        readonly Kernel ComputeResolve;
        readonly Kernel ResolveMultisamplesKernel;
        #endregion
        public long UsedVRAM
        {
            get
            {
                long result = 0;
                result += soundInput == null ? 0 : (long)soundInput.count * soundInput.stride;
                result += monoSound == null ? 0 : (long)monoSound.count * monoSound.stride;
                result += window == null ? 0 : (long)window.count * window.stride;
                result += tempBuffer == null ? 0 : (long)tempBuffer.count * tempBuffer.stride;
                result += tempBufferSmall == null ? 0 : (long)tempBufferSmall.count * tempBufferSmall.stride;
                result += buffer == null ? 0 : (long)buffer.count * buffer.stride;
                result += buffer2 == null ? 0 : (long)buffer2.count * buffer2.stride;
                result += output == null ? 0 : (long)output.count * output.stride;
                result += keyframes == null ? 0 : (long)keyframes.count * keyframes.stride;
                result += normalizeBuffer == null ? 0 : (long)normalizeBuffer.count * normalizeBuffer.stride;
                result += finalSumBuffer == null ? 0 : (long)finalSumBuffer.count * finalSumBuffer.stride;
                result += renderBuffer == null ? 0 : (long)renderBuffer.count * renderBuffer.stride;
                result += lut == null ? 0 : (long)lut.width * lut.height * 4;
                result += curveLUT == null ? 0 : (long)curveLUT.width * curveLUT.height * 4;
                return result;
            }
        }

        [Flags]
        public enum VisualizationMode
        {
            RawSpectrum = 0,
            LogFrequency = 1,
            LogAmplitude = 2,
            EnergyCorrection = 4,
            CalculateFons = 8,
            UseRuntimeFFT = 16,
            RemapVolume = 32,
            ResolveMultisamles = 64,
            RuntimeNormalize = 128
        }

        static ComputeProgram()
        {
            cs_GridOffset = Shader.PropertyToID("GridOffset");
            cs_GridSize = Shader.PropertyToID("GridSize");
            cs_Input = Shader.PropertyToID("Input");
            cs_Input2 = Shader.PropertyToID("Input2");
            cs_Output = Shader.PropertyToID("Output");
            cs_Keyframes = Shader.PropertyToID("Keyframes");
            cs_OutputKeyframes = Shader.PropertyToID("OutputKeyframes");
            cs_Window = Shader.PropertyToID("Window");
            cs_buffer = Shader.PropertyToID("buffer");
            cs_MinimumValues = Shader.PropertyToID("MinimumValues");
            cs_MaximumValues = Shader.PropertyToID("MaximumValues");
            cs_Source = Shader.PropertyToID("Source");
            cs_N = Shader.PropertyToID("N");
            cs_FFTWindow = Shader.PropertyToID("FFTWindow");
            cs_Channels = Shader.PropertyToID("Channels");
            cs_Frequency = Shader.PropertyToID("Frequency");
            cs_Scale = Shader.PropertyToID("Scale");
            cs_Time = Shader.PropertyToID("Time");
            cs_Multisamples = Shader.PropertyToID("Multisamples");
            cs_SampleStep = Shader.PropertyToID("SampleStep");
            cs_BufferStep = Shader.PropertyToID("BufferStep");
            cs_RenderTexture = Shader.PropertyToID("RenderTexture");
            cs_Lut = Shader.PropertyToID("Lut");
            cs_VolumeLUT = Shader.PropertyToID("VolumeLUT");
        }
        public ComputeProgram(ComputeShader shader)
        {
            computeShader = shader;
            CopyBuffer = new Kernel(shader, "CopyBuffer");
            MergeChannels2 = new Kernel(shader, "MergeChannels2");
            MergeChannelsN = new Kernel(shader, "MergeChannelsN");
            SaveTexture = new Kernel(shader, "SaveTexture");
            Derivative = new Kernel(shader, "Derivative");
            PowerKernel = new Kernel(shader, "PowerKernel");
            Transpose = new Kernel(shader, "Transpose");
            ReductionSum = new Kernel(shader, "ReductionSum");
            FinalSum = new Kernel(shader, "FinalSum");
            ReductionMax = new Kernel(shader, "ReductionMax");
            FinalMax = new Kernel(shader, "FinalMax");
            MultiplyKernel = new Kernel(shader, "MultiplyKernel");
            MultiplyBuffers = new Kernel(shader, "MultiplyBuffers");
            DivideKernel = new Kernel(shader, "DivideKernel");
            PartialSumBig = new Kernel(shader, "PartialSumBig");
            PartialSumSmall = new Kernel(shader, "PartialSumSmall");
            PrefixSum = new Kernel(shader, "PrefixSum");
            PrefixSumLocal = new Kernel(shader, "PrefixSumLocal");
            DampingKernel = new Kernel(shader, "DampingKernel");
            CreateWindow = new Kernel(shader, "CreateWindow");
            FFT_Init = new Kernel(shader, "FFT_Init");
            FFT_Part = new Kernel(shader, "FFT_Part");
            FFT = new Kernel(shader, "FFT_Execute");
            IFFT = new Kernel(shader, "IFFT_Execute");
            IFFT_Part = new Kernel(shader, "IFFT_Part");
            DFT_Execute = new Kernel(shader, "DFT_Execute");
            AbsSpectrum = new Kernel(shader, "AbsSpectrum");
            Resample = new Kernel(shader, "Resample");
            DrawHistogram = new Kernel(shader, "DrawHistogram");
            RenderBars = new Kernel(shader, "RenderBars");
            RenderSpectrumKernel = new Kernel(shader, "RenderSpectrumKernel");
            CalculateFons = new Kernel(shader, "CalculateFons");
            SpectrumLinearToLog = new Kernel(shader, "SpectrumLinearToLog");
            AmplitudeSmooth = new Kernel(shader, "AmplitudeSmooth");
            FrequencySmooth = new Kernel(shader, "FrequencySmooth");
            Precompute_Loudness_LUT = new Kernel(shader, "Precompute_Loudness_LUT");
            CreateFonLUT = new Kernel(shader, "CreateFonLUT");
            DrawHistogramVolume = new Kernel(shader, "DrawHistogramVolume");
            DFT_BPM = new Kernel(shader, "DFT_BPM");
            GetPeaks = new Kernel(shader, "GetPeaks");
            BeatFinder = new Kernel(shader, "BeatFinder");
            VisualizeBPM = new Kernel(shader, "VisualizeBPM");
            FillKeyframes = new Kernel(shader, "FillKeyframes");
            DecimateKeyframesKernel = new Kernel(shader, "DecimateKeyframesKernel");
            CurveFilter = new Kernel(shader, "CurveFilter");
            RemapKernel = new Kernel(shader, "RemapKernel");
            KeyframesCreator = new Kernel(shader, "KeyframesCreator");
            RemapGradientKernel = new Kernel(shader, "RemapGradientKernel");
            ConvertToQuaternions = new Kernel(shader, "ConvertToQuaternions");
            DolphChebyshevWindow = new Kernel(shader, "DolphChebyshevWindow");
            ComputeResolve = new Kernel(shader, "ComputeResolve");
            ResolveMultisamplesKernel = new Kernel(shader, "ResolveMultisamplesKernel");
        }
        void InitializeBuffer(ref ComputeBuffer buffer, int newCount, int stride)
        {
            if (buffer == null)
                buffer = new ComputeBuffer(newCount, stride);
            else if (buffer.count < newCount)
            {
                buffer.Release();
                buffer = new ComputeBuffer(newCount, stride);
            }
        }
        //Декодирует и загружает аудио затем передает в GPU
        public void LoadAudio(AudioClip clip, int offset = 0, int samples = 0)
        {
            audioClip = clip;
            if (samples <= 0)
                samples = audioClip.samples * audioClip.channels;
            float[] multiChannelSamples = new float[samples];
            if (!audioClip.preloadAudioData)
                audioClip.LoadAudioData();
            audioClip.GetData(multiChannelSamples, offset);

            InitializeBuffer(ref soundInput, samples, 4);

            soundInput.SetData(multiChannelSamples);
            computeShader.SetInt(cs_Frequency, audioClip.frequency);

            //int allignedSamples = audioClip.samples / FFTWindow * multisamples - (multisamples - 1);
            int allignedSamples = audioClip.samples;
            allignedSamples += -allignedSamples & FFTWindow - 1;
            chunks = allignedSamples / FFTWindow * multisamples;

            int partSumCount = (allignedSamples * multisamples - 1) / ReductionMax.size.x + 1;
            InitializeBuffer(ref tempBuffer, partSumCount, 4);
            InitializeBuffer(ref tempBufferSmall, partSumCount, 4);

            Kernel kernelPrepass = MergeChannelsN;
            if (audioClip.channels == 1)
                kernelPrepass = CopyBuffer;
            else if (audioClip.channels == 2)
                kernelPrepass = MergeChannels2;

            InitializeBuffer(ref monoSound, allignedSamples, 4);
            computeShader.SetBuffer(kernelPrepass.ID, cs_Input, soundInput);
            computeShader.SetBuffer(kernelPrepass.ID, cs_Output, monoSound);
            computeShader.SetInts(cs_GridOffset, new int[] { 0, 0, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { soundInput.count, 1, 1 });
            computeShader.SetInt(cs_Channels, audioClip.channels);
            computeShader.DispatchGrid(kernelPrepass, allignedSamples);
            //for (int i = 0; i < allignedSamples; i++)
            //    multiChannelSamples[i] = Mathf.Sin(2.0f * Mathf.PI * i * 1000.0f / audioClip.frequency);
            //monoSound.SetData(multiChannelSamples, 0, 0, allignedSamples);
            soundInput.Release();
            soundInput = null;
        }

        //Создает оконную функцию
        public void Initialize(int windowLogSize, DSP.Window.Type filter = DSP.Window.Type.Welch, int multisamples = 1, float windowParam = 0)
        {
            /*if (FFTWindowLog != windowLogSize || this.filter != filter 
                || filter == DSP.Window.Type.DolphChebyshev
                || filter == DSP.Window.Type.Exp
                || filter == DSP.Window.Type.HannPoisson)*/
            this.multisamples = multisamples;
            computeShader.SetInt(cs_Multisamples, multisamples);
            {
                FFTWindowLog = windowLogSize;
                FFTWindow = 1 << FFTWindowLog;
                RealWindow = FFTWindow / 2;
                computeShader.SetInt(cs_FFTWindow, FFTWindow);
                this.filter = filter;
                //float[] windowCoefs = DSP.Window.Coefficients(filter, (uint)FFTWindow);
                //windowScale = DSP.Window.ScaleFactor.Signal(windowCoefs);
                //window.SetData(windowCoefs);
                InitializeBuffer(ref window, FFTWindow, 4);
                computeShader.SetFloat(cs_Scale, windowParam);
                if (filter == DSP.Window.Type.DolphChebyshev)
                {
                    computeShader.SetBuffer(DolphChebyshevWindow.ID, cs_Output, window);
                    computeShader.Dispatch(DolphChebyshevWindow.ID, FFTWindow, 1, 1);
                    Normalize(window);
                }
                else
                {
                    computeShader.SetBuffer(CreateWindow.ID, cs_Output, window);
                    computeShader.SetInt(cs_N, (int)filter);
                    computeShader.DispatchGrid(CreateWindow, FFTWindow);
                }
                if (multisamples > 1)
                {
                    int deconvSize = multisamples / 2 * 2 + 1;
                    InitializeBuffer(ref deconvolutionBuffer, deconvSize * deconvSize, 4);
                    computeShader.SetBuffer(ComputeResolve.ID, cs_Window, window);
                    computeShader.SetBuffer(ComputeResolve.ID, cs_Output, deconvolutionBuffer);
                    computeShader.Dispatch(ComputeResolve.ID, 1, multisamples / 2 + 1, 1);
                }
                InitializeBuffer(ref finalSumBuffer, 1, 4);
                computeShader.SetBuffer(FinalSum.ID, cs_Input, window);
                computeShader.SetBuffer(FinalSum.ID, cs_Output, finalSumBuffer);
                computeShader.SetInts(cs_GridSize, new int[] { FFTWindow, 1, 1 });
                computeShader.Dispatch(FinalSum.ID, 1, 1, 1);
                float[] windowSum = new float[1];
                finalSumBuffer.GetData(windowSum);
                windowScale = FFTWindow / windowSum[0];
                if (multisamples > 1)
                {
                    int deconvSize = multisamples / 2 * 2 + 1;
                    float[] windowParts = new float[deconvSize * deconvSize];
                    deconvolutionBuffer.GetData(windowParts, 0, 0, deconvSize);
                    //StringBuilder strW = new StringBuilder();
                    //strW.AppendLine("Multisamples = " + multisamples);
                    //strW.AppendLine();
                    //strW.AppendLine(String.Join(", ", windowParts.Take(deconvSize).Select(v => v.ToString()).ToArray()) + ";");
                    //strW.AppendLine();
                    //Debug.Log("Convolution kernel = " + String.Join(", ", windowParts.Take(multisamples / 2 + 1).Select(v => v.ToString("F3")).ToArray()));
                    double[][] matrix = MatrixMath.CreateToeplitz(windowParts.Take(deconvSize).ToArray());
                    double[][] inverseM = MatrixMath.MatrixInverse(matrix);
                    //int center = multisamples / 2;
                    for (int i = 0; i < deconvSize; i++)
                    {
                        //strW.AppendLine(String.Join(", ", inverseM[i].Select(v => v.ToString()).ToArray()) + ";");
                        //double s = Math.Abs(inverseM[i].Sum());
                        for (int j = 0; j < deconvSize; j++)
                            windowParts[i * deconvSize + j] = (float)inverseM[i][j];
                        //windowParts[i * deconvSize + j] = (float)(inverseM[i][j] / s);
                        //windowParts[i * deconvSize + j] = Mathf.LerpUnclamped((i == j ? 1 : 0), (float)(inverseM[i][j] / s), resolveFactor);
                    }
                    //float[] centerRow = windowParts.Skip(center * deconvSize).Take(deconvSize).ToArray();
                    //File.WriteAllText("Deconvolution.txt", strW.ToString());
                    /*float sum = centerRow.Sum();
                    float sign = Mathf.Sign(sum);
                    for (int i = 0; i < windowParts.Length; i++)
                        windowParts[i] *= sign;*/
                    //float max = windowParts.Max();
                    //Debug.Log("Deconvolution kernel = " + String.Join(", ", centerRow.Select(v => v.ToString("F3")).ToArray()));
                    deconvolutionBuffer.SetData(windowParts);
                }
                //OpenCLCore.SaveEXR_visualize("FFTWindow.exr", windowCoefs);
            }
            if (monoSound != null)
                chunks = monoSound.count / FFTWindow * multisamples;
        }

        public void FFT_Execute(int sampleOffset = 0, int chunkOffset = 0, int batches = 0)
        {
            if (batches < 1)
                batches = chunks;
            spectrumChunks = batches;
            InitializeBuffer(ref buffer, batches * FFTWindow, 16);
            computeShader.SetInts(cs_GridOffset, new int[] { sampleOffset, chunkOffset, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { FFTWindow, batches, 1 });
            computeShader.SetInt(cs_BufferStep, 1);
            computeShader.SetInt(cs_SampleStep, 1);
            computeShader.SetBuffer(FFT_Init.ID, cs_Input, monoSound);
            computeShader.SetBuffer(FFT_Init.ID, cs_Window, window);
            computeShader.SetBuffer(FFT_Init.ID, cs_buffer, buffer);
            computeShader.DispatchGrid(FFT_Init, FFTWindow, batches);
            bufferSwap = 0;
            if (FFTWindow > FFT.size.x)
            {
                computeShader.SetBuffer(FFT_Part.ID, cs_buffer, buffer);
                for (int n = 2; n <= FFTWindow; n *= 2)
                {
                    computeShader.SetInt(cs_Source, bufferSwap);
                    computeShader.SetInt(cs_N, n);
                    computeShader.DispatchGrid(FFT_Part, FFTWindow, batches);
                    bufferSwap ^= 1;
                }
            }
            else
            {
                computeShader.SetBuffer(FFT.ID, cs_Window, window);
                computeShader.SetBuffer(FFT.ID, cs_buffer, buffer);
                computeShader.SetInt(cs_Source, bufferSwap);
                computeShader.Dispatch(FFT.ID, 1, batches, 1);
                bufferSwap ^= 1;
            }
        }
        public void ComputeDolphChebyshevWindow()
        {
            InitializeBuffer(ref buffer, FFTWindow, 16);
            computeShader.SetInts(cs_GridOffset, new int[] { 0, 0, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { FFTWindow, 1, 1 });
            computeShader.SetBuffer(DolphChebyshevWindow.ID, cs_buffer, buffer);
            computeShader.DispatchGrid(DolphChebyshevWindow, FFTWindow);
            bufferSwap = 0;
            if (FFTWindow > IFFT.size.x)
            {
                computeShader.SetBuffer(IFFT_Part.ID, cs_buffer, buffer);
                for (int n = 2; n <= FFTWindow; n *= 2)
                {
                    computeShader.SetInt(cs_Source, bufferSwap);
                    computeShader.SetInt(cs_N, n);
                    computeShader.DispatchGrid(FFT_Part, FFTWindow);
                    bufferSwap ^= 1;
                }
            }
            else
            {
                computeShader.SetBuffer(IFFT.ID, cs_Window, window);
                computeShader.SetBuffer(IFFT.ID, cs_buffer, buffer);
                computeShader.SetInt(cs_Source, bufferSwap);
                computeShader.DispatchGrid(IFFT, FFTWindow);
                bufferSwap ^= 1;
            }
        }
        public float GetMax(ComputeBuffer _input, int count, int offset = 0)
        {
            int partSumCount = (count - offset - 1) / ReductionMax.size.x + 1;
            InitializeBuffer(ref normalizeBuffer, partSumCount, 4);
            InitializeBuffer(ref finalSumBuffer, 1, 4);

            computeShader.SetBuffer(ReductionMax.ID, cs_Input, _input);
            computeShader.SetBuffer(ReductionMax.ID, cs_Output, normalizeBuffer);
            computeShader.SetInts(cs_GridOffset, new int[] { offset, 0, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { count, 1, 1 });
            computeShader.SetInt(cs_N, 1);
            computeShader.Dispatch(ReductionMax.ID, partSumCount, 1, 1);

            computeShader.SetBuffer(FinalMax.ID, cs_Input, normalizeBuffer);
            computeShader.SetBuffer(FinalMax.ID, cs_Output, finalSumBuffer);
            computeShader.SetInts(cs_GridSize, new int[] { partSumCount, 1, 1 });
            computeShader.Dispatch(FinalMax.ID, 1, 1, 1);
            float[] max = new float[1];
            finalSumBuffer.GetData(max);
            return max[0];
        }
        public void Normalize(ComputeBuffer _input)
        {
            int partSumCount = (_input.count - 1) / ReductionMax.size.x + 1;
            InitializeBuffer(ref normalizeBuffer, partSumCount, 4);
            InitializeBuffer(ref finalSumBuffer, 1, 4);

            computeShader.SetBuffer(ReductionMax.ID, cs_Input, _input);
            computeShader.SetBuffer(ReductionMax.ID, cs_Output, normalizeBuffer);
            computeShader.SetInts(cs_GridOffset, new int[] { 0, 0, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { _input.count, 1, 1 });
            computeShader.SetInt(cs_N, 1);
            computeShader.Dispatch(ReductionMax.ID, partSumCount, 1, 1);

            computeShader.SetBuffer(FinalMax.ID, cs_Input, normalizeBuffer);
            computeShader.SetBuffer(FinalMax.ID, cs_Output, finalSumBuffer);
            computeShader.SetInts(cs_GridSize, new int[] { partSumCount, 1, 1 });
            computeShader.Dispatch(FinalMax.ID, 1, 1, 1);

            computeShader.SetBuffer(DivideKernel.ID, cs_Input, finalSumBuffer);
            computeShader.SetBuffer(DivideKernel.ID, cs_Output, _input);
            computeShader.DispatchGrid(DivideKernel, _input.count);
        }
        public void Normalize(ComputeBuffer _input, RectInt region)
        {
            int partSumCount = (region.width * region.height - 1) / ReductionMax.size.x + 1;
            InitializeBuffer(ref normalizeBuffer, partSumCount, 4);
            InitializeBuffer(ref finalSumBuffer, region.height, 4);

            computeShader.SetBuffer(ReductionMax.ID, cs_Input, _input);
            computeShader.SetBuffer(ReductionMax.ID, cs_Output, normalizeBuffer);
            computeShader.SetInts(cs_GridOffset, new int[] { region.x, region.y, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { region.width, region.height, 1 });
            computeShader.SetInt(cs_N, region.width);
            computeShader.DispatchGrid(ReductionMax, region.width * region.height);

            computeShader.SetBuffer(FinalMax.ID, cs_Input, normalizeBuffer);
            computeShader.SetBuffer(FinalMax.ID, cs_Output, finalSumBuffer);
            computeShader.SetInts(cs_GridSize, new int[] { partSumCount, 1, 1 });
            computeShader.Dispatch(FinalMax.ID, 1, region.height, 1);

            computeShader.SetBuffer(DivideKernel.ID, cs_Input, finalSumBuffer);
            computeShader.SetBuffer(DivideKernel.ID, cs_Output, _input);
            computeShader.DispatchGrid(DivideKernel, region.width * region.height);
        }
        public void SaveToRenderTexture(RenderTexture texture)
        {
            computeShader.SetBuffer(SaveTexture.ID, cs_Input, output);
            computeShader.SetTexture(SaveTexture.ID, cs_RenderTexture, texture);
            computeShader.DispatchGrid(SaveTexture, texture.width, texture.height, 1);
        }
        public ComputeBuffer RenderHistogram(RenderTexture texture, float time, float scale = 1.0f, int startFrequencyBand = 0, int endFrequencyBand = 22050, float smoothness = 0, float freqSmooth = 0)
        {
            if ((mode & VisualizationMode.UseRuntimeFFT) != 0)
            {
                int updateChunks = 1;
                if (multisamples > 1 && (mode & VisualizationMode.ResolveMultisamles) != 0)
                    updateChunks += multisamples * 2;
                int smoothChunks = Mathf.CeilToInt(smoothness * audioClip.frequency * multisamples / FFTWindow);
                updateChunks = Math.Max(updateChunks, smoothChunks);
                int range = (updateChunks + (multisamples - 1)) / multisamples * FFTWindow;
                int samplesOffset = Mathf.FloorToInt(time * audioClip.frequency) - range / 2 + RealWindow * (multisamples - 1) / multisamples;
                samplesOffset = Math.Max(0, samplesOffset);
                int availableChunks = (monoSound.count - samplesOffset) / FFTWindow * multisamples - (multisamples - 1);
                updateChunks = Math.Min(256, Math.Min(updateChunks, availableChunks));
                if (updateChunks <= 0)
                    return null;
                FFT_Execute(samplesOffset, 0, updateChunks);
                ConvertToSpectrum();
                if ((mode & VisualizationMode.ResolveMultisamles) != 0)
                {
                    ResolveMultisamples(output, updateChunks, RealWindow);
                }
                InitializeBuffer(ref renderBuffer, updateChunks * RealWindow, 4);
                ComputeBuffer input = output;
                ComputeBuffer result = renderBuffer;
                if (smoothness > 0)
                {
                    computeShader.SetBuffer(AmplitudeSmooth.ID, cs_Input, input);
                    computeShader.SetBuffer(AmplitudeSmooth.ID, cs_Output, result);
                    computeShader.SetInts(cs_GridSize, new int[] { RealWindow, updateChunks, 1 });
                    computeShader.SetFloat(cs_Scale, smoothness * audioClip.frequency / FFTWindow / 2.0f);
                    computeShader.DispatchGrid(AmplitudeSmooth, RealWindow);
                    Swap(ref input, ref result);
                }
                else if (updateChunks > 1)
                {
                    computeShader.SetBuffer(CopyBuffer.ID, cs_Input, input);
                    computeShader.SetBuffer(CopyBuffer.ID, cs_Output, result);
                    computeShader.SetInts(cs_GridOffset, new int[] { RealWindow * (updateChunks / 2), 0, 0 });
                    computeShader.DispatchGrid(CopyBuffer, RealWindow);
                    Swap(ref input, ref result);
                }
                if ((mode & VisualizationMode.CalculateFons) != 0)
                {
                    computeShader.SetBuffer(CalculateFons.ID, cs_Input, input);
                    computeShader.SetBuffer(CalculateFons.ID, cs_Output, result);
                    computeShader.DispatchGrid(CalculateFons, RealWindow);
                    Swap(ref input, ref result);
                }
                if ((mode & VisualizationMode.LogFrequency) != 0)
                {
                    computeShader.SetBuffer(SpectrumLinearToLog.ID, cs_Input, input);
                    computeShader.SetBuffer(SpectrumLinearToLog.ID, cs_Output, result);
                    computeShader.SetInts(cs_GridOffset, new int[] { 0, 0, 0 });
                    computeShader.DispatchGrid(SpectrumLinearToLog, RealWindow);
                    Swap(ref input, ref result);
                }
                if (freqSmooth > 0)
                {
                    computeShader.SetBuffer(FrequencySmooth.ID, cs_Input, input);
                    computeShader.SetBuffer(FrequencySmooth.ID, cs_Output, result);
                    computeShader.SetInts(cs_GridSize, new int[] { RealWindow, 1, 1 });
                    computeShader.SetInt(cs_N, (int)(freqSmooth * RealWindow));
                    computeShader.DispatchGrid(FrequencySmooth, RealWindow);
                    Swap(ref input, ref result);
                }
                /*float rms = 1.0f;
                if ((mode & VisualizationMode.RuntimeNormalize) != 0)
                {
                    if ((mode & VisualizationMode.LogFrequency) != 0)
                    {
                        float x = 2.0f * startFrequencyBand / audioClip.frequency; // normalized Hz
                        int left = Mathf.FloorToInt(RealWindow * BitAnimator.FromLogFrequency(x));
                        float x2 = 2.0f * endFrequencyBand / audioClip.frequency;
                        int right = Mathf.FloorToInt(RealWindow * BitAnimator.FromLogFrequency(x2));
                        rms = GetRMS(input, right - left + 1, left);
                    }
                    else
                    {
                        float hzPerBin = (float)audioClip.frequency / FFTWindow;
                        rms = GetRMS(input, Mathf.FloorToInt((endFrequencyBand - startFrequencyBand) / hzPerBin) + 1, Mathf.FloorToInt(startFrequencyBand / hzPerBin));
                    }
                }*/
                Multiply(input, 0, RealWindow, scale);

                computeShader.SetBuffer(DrawHistogram.ID, cs_Input, input);
                computeShader.SetTexture(DrawHistogram.ID, cs_RenderTexture, texture);
                computeShader.SetInts(cs_GridOffset, new int[] { 0, 0, 0 });
                computeShader.SetInts(cs_GridSize, new int[] { texture.width, texture.height, 0 });
                computeShader.SetFloat(cs_MinimumValues, startFrequencyBand);
                computeShader.SetFloat(cs_MaximumValues, endFrequencyBand);
                computeShader.SetFloat(cs_Time, samplesOffset);
                computeShader.SetFloat(cs_Scale, 1.0f);
                computeShader.SetInt(cs_N, (int)mode);
                computeShader.DispatchGrid(DrawHistogram, texture.width, texture.height);
                return input;
            }
            else
            {
                return null;
            }
        }
        public float GetRMS(ComputeBuffer input, int count, int offset)
        {
            /*float[] values = new float[count];
            input.GetData(values);
            float cpuRMS = GetRMS(values);*/
            int partSumCount = (count - 1) / ReductionSum.size.x + 1;
            InitializeBuffer(ref tempBuffer, count, 4);
            InitializeBuffer(ref normalizeBuffer, partSumCount, 4);
            InitializeBuffer(ref finalSumBuffer, 1, 4);
            computeShader.SetBuffer(PowerKernel.ID, cs_Input, input);
            computeShader.SetBuffer(PowerKernel.ID, cs_Output, tempBuffer);
            computeShader.SetInts(cs_GridOffset, new int[] { offset, 0, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { count, 0, 0 });
            computeShader.SetFloat(cs_Scale, 2.0f);
            computeShader.DispatchGrid(PowerKernel, count);

            if (Mathf.NextPowerOfTwo(count) < ReductionSum.size.x)
            {
                computeShader.SetBuffer(PartialSumSmall.ID, cs_Input, tempBuffer);
                computeShader.SetBuffer(PartialSumSmall.ID, cs_Output, normalizeBuffer);
                computeShader.SetInts(cs_GridOffset, new int[] { offset, 0, 0 });
                computeShader.SetInts(cs_GridSize, new int[] { count, 1, 1 });
                computeShader.Dispatch(PartialSumSmall.ID, partSumCount, 1, 1);
            }
            else
            {
                computeShader.SetBuffer(PartialSumBig.ID, cs_Input, tempBuffer);
                computeShader.SetBuffer(PartialSumBig.ID, cs_Output, normalizeBuffer);
                computeShader.SetInts(cs_GridOffset, new int[] { offset, 0, 0 });
                computeShader.SetInts(cs_GridSize, new int[] { count, 1, 1 });
                computeShader.Dispatch(PartialSumBig.ID, partSumCount, 1, 1);
            }
            computeShader.SetBuffer(FinalMax.ID, cs_Input, normalizeBuffer);
            computeShader.SetBuffer(FinalMax.ID, cs_Output, finalSumBuffer);
            computeShader.SetInts(cs_GridSize, new int[] { partSumCount, 1, 1 });
            computeShader.Dispatch(FinalMax.ID, 1, 1, 1);

            float[] rms = new float[1];
            finalSumBuffer.GetData(rms);
            rms[0] = Mathf.Sqrt(rms[0] / count);
            return rms[0];
        }
        public float GetRMS(float[] values)
        {
            double dist = 0;
            for (int i = 0; i < values.Length; i++)
            {
                double v = values[i];
                dist += v * v;
            }
            return (float)Math.Sqrt(dist / values.Length);
        }
        ComputeBuffer CalcBPM()
        {
            int peaks = monoSound.count / 1024;
            InitializeBuffer(ref tempBuffer, peaks, 4);
            InitializeBuffer(ref tempBufferSmall, 512, 4);
            InitializeBuffer(ref finalSumBuffer, 1, 4);
            computeShader.SetBuffer(GetPeaks.ID, cs_Input, monoSound);
            computeShader.SetBuffer(GetPeaks.ID, cs_Output, tempBuffer);
            computeShader.SetInts(cs_GridSize, new int[] { monoSound.count, 1, 1 });
            computeShader.DispatchGrid(GetPeaks, monoSound.count, 1, 1);

            computeShader.SetBuffer(BeatFinder.ID, cs_Input, tempBuffer);
            computeShader.SetBuffer(BeatFinder.ID, cs_Output, tempBufferSmall);
            computeShader.SetInts(cs_GridSize, new int[] { peaks, 1, 1 });
            computeShader.DispatchGrid(BeatFinder, 256, 1, 1);

            computeShader.SetBuffer(FinalMax.ID, cs_Input, tempBufferSmall);
            computeShader.SetBuffer(FinalMax.ID, cs_Output, finalSumBuffer);
            computeShader.SetInts(cs_GridSize, new int[] { 256, 1, 1 });
            computeShader.Dispatch(FinalMax.ID, 1, 1, 1);

            computeShader.SetBuffer(DivideKernel.ID, cs_Input, finalSumBuffer);
            computeShader.SetBuffer(DivideKernel.ID, cs_Output, tempBufferSmall);
            computeShader.DispatchGrid(DivideKernel, 256);
            return tempBufferSmall;
        }
        public int GetBPM()
        {
            if (audioClip == null)
                return 0;
            /*InitializeBuffer(ref tempBufferSmall, 256, 4);
            float watch = Time.realtimeSinceStartup;
            computeShader.SetBuffer(DFT_BPM.ID, cs_Input, monoSound);
            computeShader.SetBuffer(DFT_BPM.ID, cs_Output, tempBufferSmall);
            computeShader.SetInts(cs_GridSize, new int[] { monoSound.count, 256, 1 });
            computeShader.Dispatch(DFT_BPM.ID, 1, 256, 1);
            float[] bpms = new float[256];
            tempBufferSmall.GetData(bpms);
            Debug.Log("BPM compute time = " + (Time.realtimeSinceStartup - watch) + " sec");
            StringBuilder bpms_s = new StringBuilder(8192);
            bpms_s.AppendLine("---DFT_BPM---");
            int bpm = 0;
            float value = 0;
            for (int i = 0; i < bpms.Length; i++)
            {
                if(bpms[i] > value)
                {
                    bpm = i;
                    value = bpms[i];
                }
                bpms_s.AppendFormat("{0} bpm: value={1:F6}\n", i + 40, bpms[i]);
            }
            bpms_s.AppendLine("Best match = " + (bpm + 40));
            Debug.Log(bpms_s);*/
            float[] bpms = new float[512];
            CalcBPM().GetData(bpms);
            int bpm = 0;
            float value = 0;
            StringBuilder bpms_s = new StringBuilder(12000);
            bpms_s.AppendLine("---BeatFinder---");
            for (int i = 0; i < 256; i++)
            {
                float v = bpms[i];
                if (v > value)
                {
                    bpm = i;
                    value = v;
                }
                bpms_s.AppendFormat("{0,3} bpm: value={1:F6} phase={2,6:F3}\n", i + 40, v, bpms[i + 256]);
            }
            bpms_s.Insert(0, "Best match = " + (bpm + 40) + "\n");
            Debug.Log(bpms_s);
            return bpm + 40;
        }
        public void RenderBPM(RenderTexture texture)
        {
            computeShader.SetBuffer(VisualizeBPM.ID, cs_Input, CalcBPM());
            computeShader.SetTexture(VisualizeBPM.ID, cs_RenderTexture, texture);
            computeShader.SetInts(cs_GridSize, new int[] { texture.width, texture.height, 1 });
            //computeShader.SetFloat(cs_Scale, 1.0f);
            computeShader.DispatchGrid(VisualizeBPM, texture.width, texture.height, 1);
        }
        public ComputeBuffer RenderPeaks(RenderTexture texture, float time, float duration, float scale = 1.0f, int startFrequencyBand = 0, int endFrequencyBand = 22050, float smoothness = 0, float freqSmooth = 0, float damping = 0)
        {
            bool resolving = (mode & VisualizationMode.ResolveMultisamles) != 0;
            int samples = Mathf.FloorToInt(duration * audioClip.frequency);
            int peaks = Math.Max(samples * multisamples / FFTWindow - (multisamples - 1), 2);

            int offset = Mathf.FloorToInt(time * audioClip.frequency * multisamples / FFTWindow) - peaks / 2 - multisamples / 2;
            offset = Math.Max(0, offset);
            offset = offset * FFTWindow / multisamples;
            int availableChunks = (monoSound.count - offset) / FFTWindow * multisamples - (multisamples - 1);
            int updateChunks = Math.Min(peaks, availableChunks);
            if (updateChunks <= 0)
                return null;

            FFT_Execute(offset, 0, updateChunks);
            ConvertToSpectrum();
            if (resolving)
                ResolveMultisamples(output, peaks, RealWindow);
            InitializeBuffer(ref renderBuffer, peaks, 4);
            InitializeBuffer(ref tempBuffer, peaks, 4);
            MergeBand(startFrequencyBand, endFrequencyBand, renderBuffer, 0);
                
            ComputeBuffer input = renderBuffer;
            ComputeBuffer result = tempBuffer;
            if (damping > 0)
            {
                ApplyFading(input, result, peaks, 1.0f / damping - 1.0f);
                Swap(ref input, ref result);
            }
            if (smoothness > 0)
            {
                float chunksPerSecond = (float)audioClip.frequency * multisamples / FFTWindow;
                computeShader.SetBuffer(FrequencySmooth.ID, cs_Input, input);
                computeShader.SetBuffer(FrequencySmooth.ID, cs_Output, result);
                computeShader.SetInts(cs_GridSize, new int[] { peaks, 1, 1 });
                computeShader.SetInt(cs_N, Mathf.CeilToInt(smoothness * chunksPerSecond));
                computeShader.DispatchGrid(FrequencySmooth, peaks);
                Swap(ref input, ref result);
            }
            Multiply(input, 0, peaks, scale);
            if(keyframes != null)
            {
                ApplyRemap(input, result, peaks, keyframes);
                Swap(ref input, ref result);
            }
            /*float rms = 1.0f;
            if ((mode & VisualizationMode.RuntimeNormalize) != 0)
                rms = GetRMS(renderBuffer, 0, peaks);*/

            computeShader.SetTexture(RenderBars.ID, cs_RenderTexture, texture);
            computeShader.SetBuffer(RenderBars.ID, cs_Input, input);
            computeShader.SetInts(cs_GridOffset, new int[] { 0, offset, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { texture.width, texture.height, peaks });
            computeShader.SetFloat(cs_Scale, 1.0f);
            computeShader.SetFloat(cs_Time, time);
            computeShader.SetInt(cs_N, (int)mode | 2048);
            computeShader.DispatchGrid(RenderBars, texture.width, texture.height);
            return input;
        }
        internal ComputeBuffer RenderSpectrum(RenderTexture texture, float time, float duration, float multiply, int start, int end, float smoothSpectrum, float smoothFrequency)
        {
            bool resolving = (mode & VisualizationMode.ResolveMultisamles) != 0;
            int samples = Mathf.FloorToInt(duration * audioClip.frequency);
            int peaks = Math.Max(samples * multisamples / FFTWindow - (multisamples - 1), 2);

            int offset = Mathf.FloorToInt(time * audioClip.frequency * multisamples / FFTWindow) - peaks / 2 - multisamples / 2;
            offset = Math.Max(0, offset);
            offset = offset * FFTWindow / multisamples;
            int availableChunks = (monoSound.count - offset) / FFTWindow * multisamples - (multisamples - 1);
            int updateChunks = Math.Min(peaks, availableChunks);
            if (updateChunks <= 0)
                return null;

            FFT_Execute(offset, 0, updateChunks);
            ConvertToSpectrum();
            InitializeBuffer(ref renderBuffer, peaks * RealWindow, 4);
            if (resolving)
                ResolveMultisamples(output, peaks, RealWindow);
            ComputeBuffer input = output;
            ComputeBuffer result = renderBuffer;
            float maximum = 1.0f;
            if ((mode & VisualizationMode.RuntimeNormalize) != 0)
                maximum = GetMax(output, peaks * RealWindow, 0);
            if ((mode & VisualizationMode.LogFrequency) != 0)
            {
                computeShader.SetBuffer(SpectrumLinearToLog.ID, cs_Input, input);
                computeShader.SetBuffer(SpectrumLinearToLog.ID, cs_Output, result);
                computeShader.SetInts(cs_GridOffset, new int[] { 0, 0, 0 });
                computeShader.DispatchGrid(SpectrumLinearToLog, RealWindow, peaks);
                Swap(ref input, ref result);
            }
            Multiply(input, 0, RealWindow * peaks, multiply);

            computeShader.SetTexture(RenderSpectrumKernel.ID, cs_RenderTexture, texture);
            computeShader.SetBuffer(RenderSpectrumKernel.ID, cs_Input, input);
            computeShader.SetInts(cs_GridOffset, new int[] { 0, offset, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { texture.width, texture.height, peaks });
            computeShader.SetFloat(cs_Scale, 1.0f);
            computeShader.SetFloat(cs_MinimumValues, start);
            computeShader.SetFloat(cs_MaximumValues, end);
            computeShader.SetFloat(cs_Time, time);
            computeShader.SetInt(cs_N, (int)mode | 2048);
            computeShader.DispatchGrid(RenderSpectrumKernel, texture.width, texture.height);
            return input;
        }
        public ComputeBuffer RenderMainTone(RenderTexture texture, float time, float duration, float scale = 1.0f, float smoothness = 0, float freqSmooth = 0)
        {
            return null;
        }
        public ComputeBuffer RenderWave(RenderTexture texture, float time, float duration, float scale = 1.0f, float smoothness = 0, float freqSmooth = 0)
        {
            int offset = Mathf.FloorToInt((time - duration / 2) * audioClip.frequency);
            offset -= offset & 1024 - 1;
            int samples = Mathf.FloorToInt(duration * audioClip.frequency);
            int peaks = samples / 1024;
            int peaksInChunk = 1024;
            InitializeBuffer(ref renderBuffer, peaks, 4);
            computeShader.SetBuffer(GetPeaks.ID, cs_Input, monoSound);
            computeShader.SetBuffer(GetPeaks.ID, cs_Output, renderBuffer);
            computeShader.SetInts(cs_GridOffset, new int[] { Math.Max(0, offset), 0, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { monoSound.count, 1, 1 });
            computeShader.SetInt(cs_N, peaksInChunk);
            computeShader.DispatchGrid(GetPeaks, samples);

            computeShader.SetTexture(RenderBars.ID, cs_RenderTexture, texture);
            computeShader.SetBuffer(RenderBars.ID, cs_Input, renderBuffer);
            computeShader.SetInts(cs_GridOffset, new int[] { 0, 0, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { texture.width, texture.height, peaks });
            computeShader.SetFloat(cs_Scale, scale);
            computeShader.SetFloat(cs_Time, time);
            computeShader.SetInt(cs_N, (int)mode);
            computeShader.DispatchGrid(RenderBars, texture.width, texture.height);
            return renderBuffer;
        }

        //  Unity uses Hermite spline for curves
        //  https://en.wikipedia.org/wiki/Cubic_Hermite_spline
        public static float Evaluate(Keyframe left, Keyframe right, float time)
        {
            float scale = right.time - left.time;
            float t = (time - left.time) / scale;
            float t2 = t * t;
            float t3 = t2 * t;

            float h10 = t3 - 2.0f * t2 + t;
            float h01 = 3.0f * t2 - 2.0f * t3;
            float h00 = 1.0f - h01;
            float h11 = t3 - t2;
            return h00 * left.value
                    + h10 * scale * left.outTangent
                    + h01 * right.value
                    + h11 * scale * right.inTangent;
        }
        public static float Evaluate(Keyframe[] keyframes, float[] times, float time)
        {
            int idx2 = Array.BinarySearch(times, time);
            if (idx2 < 0)
                idx2 = ~idx2;
            int idx = Mathf.Max(idx2 - 1, 0);
            idx2 = Mathf.Min(idx2, keyframes.Length - 1);
            return Evaluate(keyframes[idx], keyframes[idx2], time);
        }
        float h10(float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return t3 - 2.0f * t2 + t;
        }
        float h11(float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return t3 - t2;
        }
        public void LeastSquares2(float[] xA, float[] yA)
        {
            //y ~ a*h10(x) + b*h11(x)
            // inTangent = a
            //outTangent = b

            xA = new float[] { 0.05f, 0.1f, 0.25f, 0.4f, 0.45f, 0.65f, 0.7f, 0.9f };
            yA = new float[] { 0.1f, 0.35f, 0.35f, 0.4f, 0.15f, 0.1f, -0.1f, 0.1f };
            float t01 = 0;
            float t11 = 0;
            float t00 = 0;
            float yt0 = 0;
            float yt1 = 0;
            for (int i = 0; i < xA.Length; i++)
            {
                float x = xA[i];
                float y = yA[i];
                float t0 = h10(x);
                float t1 = h11(x);
                yt0 += y * t0;
                yt1 += y * t1;
                t00 += t0 * t0;
                t01 += t0 * t1;
                t11 += t1 * t1;
            }
            //  |t01  t11|
            //  |t00  t01| 
            float det = t01 * t01 - t11 * t00;
            //  |yt1  t11|
            //  |yt0  t01|
            float det_a = yt1 * t01 - t11 * yt0;
            //  |t01  yt1|
            //  |t00  yt0|
            float det_b = t01 * yt0 - yt1 * t00;
            float inTangent = det_a / det;
            float outTangent = det_b / det;
            Debug.LogFormat("inTangent = {0:F3}, outTangent = {1:F3}", inTangent, outTangent);
        }
        public void AproximateKeyframes(int left, int right, Keyframe[] k, out float outTangent, out float inTangent)
        {
            //y ~ a*h10(x) + b*h11(x)
            // inTangent = a
            //outTangent = b
            float start = k[left].time;
            float end = k[right].time;
            float lValue = k[left].value;
            float rValue = k[right].value;
            float scale = k[right].time - start;
            float t01 = 0;
            float t11 = 0;
            float t00 = 0;
            float yt0 = 0;
            float yt1 = 0;
            //если апроксимируем 1 ключ то решение будет неоднозначно (нулевой определитель матрицы)
            if (right - left <= 2)
            {
                //поэтому добавляем еще 2 ключа
                //и линейно интерполируем эти ключи left - k[0]' - k[0] - k[0]' - right
                float x = k[left + 1].time;
                float v = k[left + 1].value;

                float t = 0.5f * (x - start) / scale;
                float t2 = t * t;
                float t3 = t2 * t;

                float h10 = t3 - 2.0f * t2 + t;
                float h01 = 3.0f * t2 - 2.0f * t3;
                float h00 = 1.0f - h01;
                float h11 = t3 - t2;

                float y = v - (h00 * lValue + h01 * rValue);
                y *= 0.5f / scale;

                yt0 += y * h10;
                yt1 += y * h11;
                t00 += h10 * h10;
                t01 += h10 * h11;
                t11 += h11 * h11;

                t = 1.0f - 0.5f * (x - start) / scale;
                t2 = t * t;
                t3 = t2 * t;

                h10 = t3 - 2.0f * t2 + t;
                h01 = 3.0f * t2 - 2.0f * t3;
                h00 = 1.0f - h01;
                h11 = t3 - t2;

                y = v - (h00 * lValue + h01 * rValue);
                y *= 0.5f / scale;

                yt0 += y * h10;
                yt1 += y * h11;
                t00 += h10 * h10;
                t01 += h10 * h11;
                t11 += h11 * h11;
            }
            for (int i = left + 1; i < right; i++)
            {
                float x = k[i].time;
                float y = k[i].value;

                float t = (x - start) / scale;
                float t2 = t * t;
                float t3 = t2 * t;

                float h10 = t3 - 2.0f * t2 + t;
                float h01 = 3.0f * t2 - 2.0f * t3;
                float h00 = 1.0f - h01;
                float h11 = t3 - t2;

                y -= h00 * lValue + h01 * rValue;
                y /= scale;

                yt0 += y * h10;
                yt1 += y * h11;
                t00 += h10 * h10;
                t01 += h10 * h11;
                t11 += h11 * h11;
            }
            //  |t01  t11|
            //  |t00  t01| 
            float det = t01 * t01 - t11 * t00;
            //  |yt1  t11|
            //  |yt0  t01|
            float det_a = yt1 * t01 - t11 * yt0;
            //  |t01  yt1|
            //  |t00  yt0|
            float det_b = t01 * yt0 - yt1 * t00;
            outTangent = det_a / det;
            inTangent = det_b / det;
            //Debug.LogFormat("inTangent = {0:F3}, outTangent = {1:F3}", inTangent, outTangent);
        }

        public void OptimizeAnimationCurve(AnimationCurve curve, float quality)
        {

        }
        public void SmoothSpectrum(ComputeBuffer input, ComputeBuffer output, float factor)
        {
            float chunksPerSecond = (float)audioClip.frequency * multisamples / FFTWindow;
            computeShader.SetBuffer(FrequencySmooth.ID, cs_Input, input);
            computeShader.SetBuffer(FrequencySmooth.ID, cs_Output, output);
            computeShader.SetInts(cs_GridSize, new int[] { input.count, 1, 1 });
            computeShader.SetInt(cs_N, Mathf.CeilToInt(factor * chunksPerSecond));
            computeShader.DispatchGrid(FrequencySmooth, input.count);
        }
        public IEnumerable<Keyframe[]> DecimateAnimationCurve(Keyframe[] keyframes, float maxError)
        {
            float executionTime = Time.realtimeSinceStartup;
            maxError *= maxError; //Converting to RMS
            int size = keyframes.Length;
            int[] k = Enumerable.Range(0, size).ToArray();
            StringBuilder str = new StringBuilder();
            str.AppendLine("[BitAnimator] Keyframes count before decimation = " + size);
            float[] rms = new float[size];
            bool[] decimate = new bool[size];
            rms[0] = rms[size - 1] = maxError;
            int iterations = 0;
            int j, r;
            //этап 1 - поиск опорных ключевых кадров
            for (; iterations < 32; iterations++)
            {
                // j - left key index
                // r - right key index
                //пропускаем 1 ключ и вычисляем ошибку (в точке где был ключ) между оригиналом и апроксимацией
                for (int i = 1; i < size - 1; i++)
                {
                    j = k[i - 1];
                    r = k[i + 1];
                    float otg;
                    float itg;
                    AproximateKeyframes(j, r, keyframes, out otg, out itg);
                    Keyframe left = keyframes[j];
                    Keyframe right = keyframes[r];
                    left.outTangent = otg;
                    right.inTangent = itg;
                    float error = 0;
                    for (int x = j + 1; x < r; x++)
                    {
                        float aproximated = Evaluate(left, right, keyframes[x].time);
                        float original = keyframes[x].value;
                        float R = aproximated - original;
                        error += R * R;
                    }
                    rms[i] = error / (r - j - 1);
                }
                //(Аггрегатная функция) маркируем на удаление ключ если его удаление имеет меньше влияния и ниже порога
                for (int i = 1; i < size - 1; i++)
                {
                    if (rms[i] <= maxError && rms[i] <= rms[i - 1] && rms[i] <= rms[i + 1])
                        decimate[i] = true;
                    else
                        decimate[i] = false;
                }
                //перемещаем оставшиеся ключи к началу массива
                j = 1;
                double currentRMS = 0;
                for (int i = 1; i < size; i++)
                {
                    if (!decimate[i])
                    {
                        k[j] = k[i];
                        j++;
                    }
                    else
                        currentRMS += rms[i];
                    rms[i] = 0;
                }
                yield return null;
                //str.AppendLine(string.Format("Iteration: {0}, Keyframes = {1}, RMSD = {2:F6}", iterations, j, size != j ? Math.Sqrt(currentRMS / (size - j)) : 0));
                //при малом сжатии прерываем оптимизацию
                if (j < 0.99f * size)
                    size = j;
                else
                {
                    size = j;
                    break;
                }
            }
            //апроксимируем и сохраняем результат
            //и вычисляем среднеквадратическую ошибку кривой между оригиналом и апроксимацией
            double RMS = 0;
            for (int i = 1; i < size; i++)
            {
                j = k[i - 1];
                r = k[i];
                if (r - j <= 1)
                    continue;

                float otg;
                float itg;
                AproximateKeyframes(j, r, keyframes, out otg, out itg);
                keyframes[j].outTangent = otg;
                keyframes[r].inTangent = itg;
                Keyframe left = keyframes[j];
                Keyframe right = keyframes[r];
                for (int x = j + 1; x < r; x++)
                {
                    double aproximated = Evaluate(left, right, keyframes[x].time);
                    double original = keyframes[x].value;
                    double R = aproximated - original;
                    RMS += R * R;
                }
            }
            Keyframe[] result = k.Take(size).Select((idx) => keyframes[idx]).ToArray();
            executionTime = Time.realtimeSinceStartup - executionTime;
            str.AppendLine(string.Format("Keyframes after {0} iterations = {1}, RMSE = {2:F6}, time = {3:F3}", iterations, result.Length, Math.Sqrt(RMS / keyframes.Length), executionTime));
            Debug.Log(str);
            yield return result;
        }
        public void ResolveMultisamples(ComputeBuffer input, int inputCount, int bandSize)
        {
            if (multisamples <= 1)
                return;

            InitializeBuffer(ref buffer2, bandSize * inputCount, 4);
            computeShader.SetBuffer(ResolveMultisamplesKernel.ID, cs_Input, input);
            computeShader.SetBuffer(ResolveMultisamplesKernel.ID, cs_Window, deconvolutionBuffer);
            computeShader.SetBuffer(ResolveMultisamplesKernel.ID, cs_Output, buffer2);
            computeShader.SetInts(cs_GridSize, new int[] { bandSize, inputCount, 1 });
            computeShader.SetFloat(cs_MaximumValues, resolveCoeff);
            computeShader.SetFloat(cs_Scale, resolveFactor);
            computeShader.DispatchGrid(ResolveMultisamplesKernel, bandSize, inputCount);

            computeShader.SetBuffer(CopyBuffer.ID, cs_Input, buffer2);
            computeShader.SetBuffer(CopyBuffer.ID, cs_Output, input);
            computeShader.SetInts(cs_GridOffset, new int[] { 0, 0, 0 });
            computeShader.DispatchGrid(CopyBuffer, bandSize * inputCount);
        }
        public void ApplyFading(Keyframe[] keyframes, float minValue, float maxValue, float fadeSpeed)
        {
            float lastValue = 0;
            float lastTime = 0;
            float time;
            float value;
            float factor;
            if (maxValue > minValue)
                for (int i = 0; i < keyframes.Length; i++)
                {
                    time = keyframes[i].time;
                    factor = Mathf.Exp(-fadeSpeed * (time - lastTime));
                    value = keyframes[i].value;
                    lastValue = lastValue * factor + minValue * (1.0f - factor);
                    if (value > lastValue)
                        lastValue = value;
                    else
                        lastValue = keyframes[i].value = lastValue * factor + value * (1.0f - factor);

                    lastTime = time;
                }
            else
                for (int i = 0; i < keyframes.Length; i++)
                {
                    time = keyframes[i].time;
                    factor = Mathf.Exp(-fadeSpeed * (time - lastTime));
                    value = keyframes[i].value;
                    lastValue = lastValue * factor + minValue * (1.0f - factor);
                    if (value < lastValue)
                        lastValue = value;
                    else
                        lastValue = keyframes[i].value = lastValue * factor + value * (1.0f - factor);

                    lastTime = time;
                }
        }
        public void ApplyFading(ComputeBuffer input, ComputeBuffer output, int count, float fadeSpeed)
        {
            computeShader.SetInts(cs_GridOffset, 0, 0, 0);
            computeShader.SetInts(cs_GridSize, count, 1, 1);
            float chunkTime = (float)FFTWindow / multisamples / audioClip.frequency;
            bool swap = true;
            for (int offset = 1; offset < count; offset *= 2)
            {
                float factor = Mathf.Exp(-fadeSpeed * chunkTime * offset);
                computeShader.SetBuffer(DampingKernel.ID, cs_Input, input);
                computeShader.SetBuffer(DampingKernel.ID, cs_Output, output);
                computeShader.SetFloat(cs_Scale, factor);
                computeShader.SetInt(cs_N, offset);
                computeShader.DispatchGrid(DampingKernel, count);
                Swap(ref input, ref output);
                swap ^= true;
            }
            if (swap)
            {
                computeShader.SetBuffer(CopyBuffer.ID, cs_Input, input);
                computeShader.SetBuffer(CopyBuffer.ID, cs_Output, output);
                computeShader.DispatchGrid(CopyBuffer, count);
            }
        }
        public void Release()
        {
            if (monoSound != null) monoSound.Dispose();
            if (deconvolutionBuffer != null) deconvolutionBuffer.Dispose();
            if (window != null) window.Dispose();
            FreeMemory();
        }
        public void FreeMemory()
        {
            if (soundInput != null) soundInput.Dispose();
            if (tempBuffer != null) tempBuffer.Dispose();
            if (tempBufferSmall != null) tempBufferSmall.Dispose();
            if (buffer != null) buffer.Dispose();
            if (buffer2 != null) buffer2.Dispose();
            if (output != null) output.Dispose();
            if (normalizeBuffer != null) normalizeBuffer.Dispose();
            if (finalSumBuffer != null) finalSumBuffer.Dispose();
            if (renderBuffer != null) renderBuffer.Dispose();  
            if (keyframes != null) keyframes.Dispose();
            soundInput = null;
            keyframes = null;
            buffer = null;
            buffer2 = null;
            tempBufferSmall = null;
            tempBuffer = null;
            finalSumBuffer = null;
            normalizeBuffer = null;
            output = null;
            renderBuffer = null;
        }
        public static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp = lhs;
            lhs = rhs;
            rhs = temp;
        }

        public void ConvertToSpectrum()
        {
            InitializeBuffer(ref output, buffer.count / 2, 4);
            computeShader.SetBuffer(AbsSpectrum.ID, cs_buffer, buffer);
            computeShader.SetBuffer(AbsSpectrum.ID, cs_Output, output);
            computeShader.SetInts(cs_GridOffset, new int[] { 0, 0, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { RealWindow, spectrumChunks, 1 });
            computeShader.SetFloat(cs_Scale, windowScale);
            computeShader.SetInt(cs_Source, bufferSwap);
            computeShader.SetInt(cs_N, (int)mode);
            computeShader.DispatchGrid(AbsSpectrum, FFTWindow / 2, spectrumChunks);
        }

        public void Multiply(ComputeBuffer buffer, int offset, int count, float multiply)
        {
            computeShader.SetBuffer(MultiplyKernel.ID, cs_Output, buffer);
            computeShader.SetInts(cs_GridOffset, offset, 0, 0);
            computeShader.SetInts(cs_GridSize, count, 1, 1 );
            computeShader.SetFloat(cs_Scale, multiply);
            computeShader.DispatchGrid(MultiplyKernel, count);
        }

        public void MergeBand(int startFreq, int endFreq, ComputeBuffer output, int offset)
        {
            float hzPerBin = (float)audioClip.frequency / FFTWindow;
            int start = Mathf.FloorToInt(startFreq / hzPerBin);
            int end = Mathf.CeilToInt(endFreq / hzPerBin);
            int count = Mathf.NextPowerOfTwo(end - start);
            if (count < ReductionSum.size.x)
            {
                /*int N = ReductionSum.size.x / count;
                computeShader.SetBuffer(ReductionSum.ID, cs_Input, this.output);
                computeShader.SetBuffer(ReductionSum.ID, cs_Output, output);
                computeShader.SetInts(cs_GridOffset, new int[] { start, 0, 0 });
                computeShader.SetInts(cs_GridSize, new int[] { end, spectrumChunks, 1 });
                computeShader.SetInt(cs_N, N);
                computeShader.Dispatch(ReductionSum.ID, (spectrumChunks - 1) / N + 1, 1, 1);*/
                computeShader.SetBuffer(PartialSumSmall.ID, cs_Input, this.output);
                computeShader.SetBuffer(PartialSumSmall.ID, cs_Output, output);
                computeShader.SetInts(cs_GridOffset, start, 0, offset);
                computeShader.SetInts(cs_GridSize, end, spectrumChunks, 1);
                computeShader.DispatchGrid(PartialSumSmall, spectrumChunks);
            }
            else
            {
                computeShader.SetBuffer(PartialSumBig.ID, cs_Input, this.output);
                computeShader.SetBuffer(PartialSumBig.ID, cs_Output, output);
                computeShader.SetInts(cs_GridOffset, start, 0, offset);
                computeShader.SetInts(cs_GridSize, end, spectrumChunks, 1);
                computeShader.Dispatch(PartialSumBig.ID, 1, spectrumChunks, 1);
            }
        }

        public void CalculateLoudnessVelocity(ComputeBuffer input, ComputeBuffer output)
        {
            computeShader.SetBuffer(Derivative.ID, cs_Input, input);
            computeShader.SetBuffer(Derivative.ID, cs_Output, output);
            computeShader.SetInts(cs_GridOffset, new int[] { 0, 0, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { input.count, 1, 1 });
            computeShader.DispatchGrid(Derivative, input.count);
        }
        public void CalculatePrefixSum(ComputeBuffer input, ComputeBuffer output, int count)
        {
            computeShader.SetBuffer(PrefixSum.ID, cs_Input, input);
            computeShader.SetBuffer(PrefixSum.ID, cs_Output, output);
            computeShader.SetInts(cs_GridOffset, 0, 0, 0);
            computeShader.SetInts(cs_GridSize, count, 1, 1);
            bool swap = true;
            for (int offset = 1; offset < count; offset *= 2)
            {
                computeShader.SetBuffer(PrefixSum.ID, cs_Input, input);
                computeShader.SetBuffer(PrefixSum.ID, cs_Output, output);
                computeShader.SetInt(cs_N, offset);
                computeShader.DispatchGrid(PrefixSum, count);
                Swap(ref input, ref output);
                swap ^= true;
            }
            if (swap)
            {
                computeShader.SetBuffer(CopyBuffer.ID, cs_Input, input);
                computeShader.SetBuffer(CopyBuffer.ID, cs_Output, output);
                computeShader.DispatchGrid(CopyBuffer, count);
            }
        }
        public void SetRemap(AnimationCurve curve)
        {
            if (keyframes != null)
                keyframes.Dispose();
            if(curve == null)
            {
                keyframes = null;
                return;
            }
            keyframes = new ComputeBuffer(curve.length, 20);
            keyframes.SetData(curve.keys);
        }
        public void ApplyRemap(ComputeBuffer input, ComputeBuffer output, int count, ComputeBuffer remap)
        {
            computeShader.SetBuffer(RemapKernel.ID, cs_Input, input);
            computeShader.SetBuffer(RemapKernel.ID, cs_Output, output);
            computeShader.SetBuffer(RemapKernel.ID, cs_Keyframes, remap);
            computeShader.SetInts(cs_GridOffset, 0, 0, 0);
            computeShader.SetInts(cs_GridSize, count, 1, remap.count);
            computeShader.DispatchGrid(RemapKernel, count);
        }
        public void CreateKeyframes(ComputeBuffer input, ComputeBuffer output, Vector4 min, Vector4 max, int channels, int loopCount)
        {
            computeShader.SetBuffer(KeyframesCreator.ID, cs_Input, input);
            computeShader.SetBuffer(KeyframesCreator.ID, cs_OutputKeyframes, output);
            computeShader.SetFloats(cs_MinimumValues, min.x, min.y, min.z, min.w );
            computeShader.SetFloats(cs_MaximumValues, max.x, max.y, max.z, max.w );
            computeShader.SetInts(cs_GridOffset, 0, 0, 0);
            computeShader.SetInts(cs_GridSize, input.count, channels, 1);
            computeShader.SetInt(cs_Source, loopCount);
            computeShader.DispatchGrid(KeyframesCreator, input.count);
        }
        public void RemapGradient(ComputeBuffer input, ComputeBuffer output, ComputeBuffer remap, int channels, int gradientKeys, int loopCount)
        {
            computeShader.SetBuffer(RemapGradientKernel.ID, cs_Input, input);
            computeShader.SetBuffer(RemapGradientKernel.ID, cs_OutputKeyframes, output);
            computeShader.SetBuffer(RemapGradientKernel.ID, cs_Keyframes, remap);
            computeShader.SetInts(cs_GridOffset, new int[] { 0, 0, 0 });
            computeShader.SetInts(cs_GridSize, new int[] { input.count, channels, gradientKeys });
            computeShader.SetInt(cs_Source, loopCount);
            computeShader.SetInt(cs_N, remap.count - gradientKeys*3);
            computeShader.DispatchGrid(RemapGradientKernel, input.count);
        }
        public void ConvertKeyframesRotation(ComputeBuffer output, int count)
        {
            computeShader.SetBuffer(ConvertToQuaternions.ID, cs_OutputKeyframes, output);
            computeShader.SetInts(cs_GridOffset, 0, 0, 0);
            computeShader.SetInts(cs_GridSize, count, 1, 1);
            computeShader.DispatchGrid(ConvertToQuaternions, count);
        }
        public void DecimateKeyframes(ComputeBuffer keyframes)
        {
            throw new NotImplementedException();
        }

        internal void ResolveMultisamples()
        {
            if ((mode & VisualizationMode.ResolveMultisamles) != 0 && !Mathf.Approximately(resolveFactor, 0))
                ResolveMultisamples(output, spectrumChunks, RealWindow);
        }

        internal void ApplyCurveFilter(ComputeBuffer input, float fadeIn, float start, float end, float fadeOut)
        {
            Keyframe[] keys = new Keyframe[4];
            keys[0].time = fadeIn;
            keys[0].value = 0;
            keys[1].time = start;
            keys[1].value = 1;
            keys[2].time = end;
            keys[2].value = 1;
            keys[3].time = fadeOut;
            keys[3].value = 0;
            InitializeBuffer(ref keyframes, keys.Length, 20);
            keyframes.SetData(keys);
            computeShader.SetBuffer(CurveFilter.ID, cs_Output, input);
            computeShader.SetBuffer(CurveFilter.ID, cs_Keyframes, keyframes);
            computeShader.SetInts(cs_GridOffset, 0, 0, 0);
            computeShader.SetInts(cs_GridSize, input.count, 1, keys.Length);
            computeShader.DispatchGrid(CurveFilter, input.count);
        }

        internal void Multiply(ComputeBuffer values, ComputeBuffer mask)
        {
            computeShader.SetBuffer(MultiplyBuffers.ID, cs_Input, mask);
            computeShader.SetBuffer(MultiplyBuffers.ID, cs_Output, values);
            computeShader.DispatchGrid(MultiplyBuffers, values.count);
        }
    }
}
#endif

