
// Copyright © 2019 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.0 (17.07.2019)

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;
using AudioVisualization;
using System.IO;

public class BitAnimatorWindow : EditorWindow
{
    public enum PlotGraphic
    {
        Peaks,Spectrum, Histogram//, Wave, BPM, Tones
    }
    public TextAsset serializedPresets;
    public RenderTexture texture;

    ComputeProgram.VisualizationMode mode = ComputeProgram.VisualizationMode.UseRuntimeFFT | ComputeProgram.VisualizationMode.LogFrequency | ComputeProgram.VisualizationMode.RuntimeNormalize;
    PlotGraphic plotGraphic = PlotGraphic.Spectrum;
    Rect box = new Rect(0, 0, 1, 1);
    bool openSettings = true;
    Vector2 pos, mouseDown, scroll;
    float scale = 3.0f;
    float multiply = 1.0f;
    float autoMultiply = 1.0f;
    float smoothFrequency = 0.0f;
    float time;
    float autoNormilizeSpeed = 10.0f;
    float rms, median, maximum;
    int BPM = 0;
    int selectedBitAnimator = -1;

    public static float executionTime;
    public static int selectedSlot;
    public static BitAnimator target;
    public static Animation animation;
    public static AudioSource audio;
    public static bool isRunningTask;
    public static bool resetInNextFrame;
    public int targetID;
    

    [MenuItem("Window/BitAnimator")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        BitAnimatorWindow window = EditorWindow.GetWindow<BitAnimatorWindow>("BitAnimator");
        window.Show();
    }
    void Awake()
    {
        wantsMouseMove = true;
    }
    static BitAnimatorWindow()
    {
        EditorApplication.playModeStateChanged += PlayModeStateChanged;
    }
    void OnGUI()
    {
        if (target == null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool createNew = GUILayout.Button("Create new BitAnimator", GUILayout.MinHeight(40), GUILayout.MaxWidth(180));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (createNew)
            {
                GameObject animatorGO = new GameObject("New Animator");
                target = animatorGO.AddComponent<BitAnimator>();
                target.animatorObject = animatorGO.AddComponent<Animator>();
                AudioSource audioSource = animatorGO.AddComponent<AudioSource>();
                audioSource.minDistance = 10;
                audioSource.volume = 0.25f;
                audioSource.dopplerLevel = 0;
                audioSource.spatialize = false;
                GameObject targetGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                targetGO.transform.parent = animatorGO.transform;
                DestroyImmediate(targetGO.GetComponent<BoxCollider>());
                target.targetObject = targetGO;
                Selection.activeObject = target;
            }
            else
            {
                BitAnimator[] bitAnimators = Resources.FindObjectsOfTypeAll<BitAnimator>();
                if (bitAnimators.Length > 0)
                {
                    GUILayout.FlexibleSpace();
                    target = (BitAnimator)EditorGUILayout.ObjectField("BitAnimator", target, typeof(BitAnimator), true);
                    GUILayout.Label("Or select exists animator:");
                    scroll = GUILayout.BeginScrollView(scroll, EditorStyles.helpBox);
                    selectedBitAnimator = GUILayout.SelectionGrid(selectedBitAnimator, bitAnimators.Select(c =>
                    {
                        AudioSource audio = c.GetComponentInChildren<AudioSource>();
                        return c.gameObject.name + (c.audioClip != null ? " | " + c.audioClip.name : "");
                    }).ToArray(), 1, EditorStyles.radioButton);
                    GUILayout.EndScrollView();
                    if (selectedBitAnimator >= 0)
                    {
                        Selection.activeObject = bitAnimators[selectedBitAnimator];
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Open", GUILayout.MinHeight(40), GUILayout.MaxWidth(180)))
                        {
                            target = bitAnimators[selectedBitAnimator];
                            selectedBitAnimator = -1;
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        else if(target.recordSlots.Count == 0)
        {
            target = (BitAnimator)EditorGUILayout.ObjectField("BitAnimator", target, typeof(BitAnimator), true);
            EditorGUILayout.HelpBox("This window only for visualization. Setup BitAnimator in the inspector window", MessageType.Info);
        }
        else
        {
            openSettings = EditorGUILayout.Foldout(openSettings, "Show settings", true);
            if (openSettings)
            {
                EditorGUI.indentLevel += 1;
                target = (BitAnimator)EditorGUILayout.ObjectField("BitAnimator", target, typeof(BitAnimator), true);
                mode = (ComputeProgram.VisualizationMode)EditorGUILayout.EnumFlagsField("Visualization modes", mode);
                scale = EditorGUILayout.Slider("Scale", scale, 1, 30);
                multiply = Mathf.Pow(10.0f, EditorGUILayout.Slider("Multiply Log", Mathf.Log10(multiply), -5, 5));
                EditorGUILayout.LabelField(String.Format("Multiply = {0:F6}, AutoMultiply = {1:F6}, RMS = {2:F6}, Max = {3:F6}, Median = {4:F6}", multiply, autoMultiply, rms, maximum, median));
                if ((mode & ComputeProgram.VisualizationMode.RuntimeNormalize) != 0)
                    autoNormilizeSpeed = EditorGUILayout.Slider("AutoNormilize Speed", autoNormilizeSpeed, 0, 50);
                smoothFrequency = EditorGUILayout.Slider("FrequencySmooth", smoothFrequency, 0, 1);
                using (new EditorGUI.DisabledScope(audio == null || animation == null))
                {
                    if (audio != null && audio.clip != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        time = audio.time;
                        time = EditorGUILayout.Slider("Animation play time", time, 0, audio.clip.length);
                        if (animation != null)
                        {
                            AnimationState anim = animation["BitAnimator.RuntimeAnimation"];
                            anim.speed = audio.pitch;
                            if (EditorGUI.EndChangeCheck())
                            {
                                bool oldState = anim.enabled;
                                anim.enabled = true;
                                audio.time = time;
                                anim.time = time;
                                animation.Sample();
                                anim.enabled = oldState;
                            }
                            else if (Mathf.Abs(anim.time - audio.time) > 0.05f)
                                anim.time = audio.time;
                        }
                        else if (EditorGUI.EndChangeCheck())
                        {
                            audio.time = time;
                        }
                    }
                    else
                    {
                        time = EditorGUILayout.Slider("Sound clip play time", time, 0, 0);
                    }
                    GUILayout.BeginHorizontal();
                    if (audio != null && animation != null)
                    {
                        if (audio.isPlaying)
                        {
                            if (GUILayout.Button("Pause", GUILayout.MaxWidth(128)))
                            {
                                audio.Pause();
                                animation.Stop();
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("Play", GUILayout.MaxWidth(128)))
                            {
                                audio.Play();
                                animation.Play();
                                animation["BitAnimator.RuntimeAnimation"].time = audio.time;
                            }
                        }
                    }
                    else
                    {
                        GUILayout.Button("Play", GUILayout.MaxWidth(128));
                    }

                    if (GUILayout.Button("Reset view", GUILayout.MaxWidth(128)))
                        ResetView();

                    if (BPM > 0)
                        GUILayout.Label("BPM: " + BPM);
                    GUILayout.EndHorizontal();
                }
                if (target != null)
                {
                    if (target.core != null)
                    {
                        //volumeCurve = EditorGUILayout.CurveField(volumeCurve);
                        /*if (GUILayout.Button("Decimation test", GUILayout.MaxWidth(128)))
                        {

                            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(target.animationAssetPath);
                            EditorCurveBinding[] b = AnimationUtility.GetCurveBindings(clip);
                            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, b[1]);
                            StringBuilder str = new StringBuilder();
                            Keyframe[] keyframes = curve.keys;
                            //float rmsQuality = -(float)Math.Log(Math.Max(0.001, target.quality), Math.E) / 8.0f;
                            var values = keyframes.Select(k => (double)k.value);
                            double mean = values.Sum() / keyframes.Length;
                            double sd = Math.Sqrt(values.Select(v => (v - mean) * (v - mean)).Sum() / keyframes.Length);
                            double rms = Math.Sqrt(values.Select(v => v * v).Sum() / keyframes.Length);
                            double avg_sqrV = 0, v1 = 0;
                            foreach (double v in values)
                            {
                                double dv = v - v1;
                                avg_sqrV += dv * dv;
                                v1 = v;
                            }
                            avg_sqrV = Math.Sqrt(avg_sqrV / keyframes.Length);
                            str.AppendLine(String.Format("[Original] Mean = {0:F4}, SD = {1:F4}, RMS = {2:F4}, v^2 = {3:F4}, Count = {4}", mean, sd, rms, avg_sqrV, keyframes.Length));

                            float rq = Mathf.Pow(10.0f, -6.0f * target.quality * target.quality - 1.0f);
                            keyframes = target.core.DecimateAnimationCurve(curve.keys, rq).Where(k => k != null).Single();
                            values = keyframes.Select(k => (double)k.value);
                            mean = values.Sum() / keyframes.Length;
                            sd = Math.Sqrt(values.Select(v => (v - mean) * (v - mean)).Sum() / keyframes.Length);
                            rms = Math.Sqrt(values.Select(v => v * v).Sum() / keyframes.Length);
                            avg_sqrV = 0; v1 = 0;
                            foreach (double v in values)
                            {
                                double dv = v - v1;
                                avg_sqrV += dv * dv;
                                v1 = v;
                            }
                            avg_sqrV = Math.Sqrt(avg_sqrV / keyframes.Length);
                            str.AppendLine(String.Format("[Deciamated] Quality = {0}, Mean = {1:F4}, SD = {2:F4}, RMS = {3:F4}, v^2 = {4:F4}, Count = {5}", rq, mean, sd, rms, avg_sqrV, keyframes.Length));
                            curve.keys = keyframes;
                            Debug.Log(str);
                            AnimationUtility.SetEditorCurve(clip, b[0], curve);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                        }*/
                        if (GUILayout.Button("Get BPM", GUILayout.MaxWidth(128)))
                        {
                            BPM = target.core.GetBPM();
                        }
                        EditorGUI.BeginChangeCheck();
                        plotGraphic = (PlotGraphic)EditorGUILayout.EnumPopup("Plot graphic", plotGraphic);
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (texture == null)
                            {
                                texture = new RenderTexture(512, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                                texture.enableRandomWrite = true;
                                texture.Create();
                                target.core.texture = texture;
                            }
                            target.core.RenderBPM(texture);
                            autoMultiply = 1.0f / rms;
                        }
                        if (texture != null)
                        {
                            if (Event.current.type == EventType.Repaint)
                            {
                                box = GUILayoutUtility.GetRect(position.width, position.height);
                                GUI.Box(box, texture);

                                int w = Mathf.FloorToInt(box.width / 8.0f) * 8;
                                int h = Mathf.FloorToInt(box.height / 8.0f) * 8;
                                if (texture.width != w || texture.height != h)
                                {
                                    texture.Release();
                                    texture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                                    texture.enableRandomWrite = true;
                                    texture.Create();
                                    target.core.texture = texture;
                                }
                            }
                            else
                                GUILayout.Box(texture, GUILayout.MinWidth(32), GUILayout.MinHeight(32), GUILayout.MaxWidth(2048), GUILayout.MaxHeight(2048));
                        }
                    }
                }
                int vram = SystemInfo.graphicsMemorySize;
                GUILayout.Label(string.Format("Available VRAM: {0:F3}GB | Used: {1:F3}GB", vram / 1024.0f, target != null && target.core != null ? target.core.UsedVRAM / 1024.0f / 1024.0f / 1024.0f : 0), EditorStyles.boldLabel);
                EditorGUI.indentLevel -= 1;
            }
            else if(texture != null)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    Rect rect = GUILayoutUtility.GetRect(position.width, position.height);
                    GUI.Box(rect, texture);

                    int w = Mathf.FloorToInt(rect.width / 8.0f) * 8;
                    int h = Mathf.FloorToInt(rect.height / 8.0f) * 8;
                    if (texture.width != w || texture.height != h)
                    {
                        texture.Release();
                        texture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                        texture.enableRandomWrite = true;
                        texture.Create();
                        target.core.texture = texture;
                    }
                }
                else
                    GUILayout.Box(texture, GUILayout.MinWidth(32), GUILayout.MinHeight(32), GUILayout.MaxWidth(2048), GUILayout.MaxHeight(2048));
            }
            EditorGUIUtility.AddCursorRect(box, MouseCursor.SlideArrow);
            if (Event.current.type == EventType.MouseDown)
            {
                mouseDown = Event.current.mousePosition;
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                mouseDown = -Vector2.one;
            }
            else if (Event.current.type == EventType.MouseDrag && box.Contains(mouseDown))
            {
                if (mouseDown.x >= 0 && audio != null)
                {
                    audio.time -= Event.current.delta.x / box.width * scale;
                    if (animation != null)
                    {
                        AnimationState anim = animation["BitAnimator.RuntimeAnimation"];
                        bool state = anim.enabled;
                        anim.enabled = true;
                        anim.time = audio.time;
                        animation.Sample();
                        anim.enabled = state;
                    }
                    Event.current.Use();
                }
            }
            else if (Event.current.type == EventType.MouseMove)
            {
                pos = Event.current.mousePosition;
                if (box.Contains(pos))
                    pos -= box.position;
                else
                    pos = Vector2.zero;
                /*if (plotGraphic == PlotGraphic.BPM)
                {
                    float offset = (box.width - texture.width) / 2.0f;
                    pos.x -= offset;
                    BPM = Mathf.FloorToInt(pos.x / texture.width * 256 + 40);
                    Event.current.Use();
                }*/
            }
            else if (Event.current.type == EventType.ScrollWheel)
            {
                if (box.Contains(Event.current.mousePosition))
                {
                    scale *= Event.current.delta.y < 0 ? 1.125f : 1.0f / 1.125f;
                    Event.current.Use();
                }
            }
        }
    }
    public static void ResetState()
    {
        AnimationState runtimeAnimation = animation["BitAnimator.RuntimeAnimation"];
        if (runtimeAnimation != null)
        {
            runtimeAnimation.time = 0;
            animation.Sample();
            animation.Stop();
        }
        DestroyImmediate(animation);
        if (audio != null)
            audio.Stop();
    }

    void Update()
    {
        if (!isRunningTask)
        {
            if (!EditorApplication.isPlaying && animation != null)
            {
                animation["BitAnimator.RuntimeAnimation"].time = audio.time;
                animation.Sample();
            }
            if (!isRunningTask && !EditorApplication.isPaused && target != null && target.core != null && target.recordSlots.Count > 0 && animation != null && audio != null)
            {
                target.animationTime = audio.time;
                
                if (texture == null)
                {
                    texture = new RenderTexture(512, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    texture.enableRandomWrite = true;
                    texture.Create();
                    target.core.texture = texture;
                }
                BitAnimator.RecordSlot slot = target.recordSlots[selectedSlot];
                BitAnimator.SetMode(ref mode, target.calculateLoudness, target.energyCorrection);
                target.core.mode = mode;
                target.core.resolveFactor = target.deconvolution;
                target.core.SetRemap(slot.remap);
                float mul = autoMultiply * multiply;
                ComputeBuffer viewValues = null;
                switch (plotGraphic)
                {
                    case PlotGraphic.Histogram:
                        viewValues = target.core.RenderHistogram(
                            texture,
                            audio.time,
                            mul,
                            slot.startFreq,
                            slot.endFreq,
                            slot.ampSmoothness,
                            smoothFrequency);
                        break;
                    case PlotGraphic.Peaks:
                        viewValues = target.core.RenderPeaks(
                            texture,
                            audio.time,
                            scale,
                            mul,
                            slot.startFreq,
                            slot.endFreq,
                            slot.ampSmoothness,
                            smoothFrequency,
                            slot.damping);
                        break;
                    case PlotGraphic.Spectrum:
                        viewValues = target.core.RenderSpectrum(
                            texture,
                            audio.time,
                            scale,
                            mul,
                            slot.startFreq,
                            slot.endFreq,
                            slot.ampSmoothness,
                            smoothFrequency);
                        break;
                    /*case PlotGraphic.Tones:
                        target.core.RenderMainTone(
                            texture,
                            audio.time,
                            scale,
                            mul,
                            slot.ampSmoothness,
                            smoothFrequency);
                        break;
                    case PlotGraphic.Wave:
                        rms = target.core.RenderWave(texture, audio.time, 2.0f, mul, slot.ampSmoothness, smoothFrequency); break;*/
                }
                if (viewValues != null)
                {
                    float[] values = new float[target.core.spectrumChunks];
                    viewValues.GetData(values);
                    Array.Sort(values);
                    median = values[target.core.spectrumChunks / 2];
                    maximum = Math.Max(0.00001f, values[target.core.spectrumChunks - 1]);
                    rms = target.core.GetRMS(values);
                    if(resetInNextFrame)
                    {
                        if (float.IsInfinity(multiply) || float.IsNaN(multiply))
                            multiply = 1.0f;
                        if (float.IsInfinity(autoMultiply) || float.IsNaN(autoMultiply))
                            autoMultiply = 1.0f;
                        if (float.IsInfinity(maximum) || float.IsNaN(maximum))
                            maximum = 1.0f;
                        autoMultiply = 0.95f / (maximum / autoMultiply / multiply);
                        resetInNextFrame = false;
                    }
                    if (audio.isPlaying && (mode & ComputeProgram.VisualizationMode.RuntimeNormalize) != 0)
                    {
                        float delta = 0.95f / (maximum / autoMultiply / multiply) - autoMultiply;
                        if (delta < 0)
                        {
                            float k = 1.0f - Mathf.Exp(-Time.deltaTime * autoNormilizeSpeed);
                            autoMultiply += k * delta;
                        }
                    }
                }
                Repaint();
            }
        }
    }
    public static void ResetView()
    {
        resetInNextFrame = true;
    }
    static void PlayModeStateChanged(PlayModeStateChange state)
    {
        BitAnimatorWindow window = GetWindow<BitAnimatorWindow>();
        switch (state)
        {
            case PlayModeStateChange.EnteredEditMode:
                isRunningTask = false;
                EditorUtility.ClearProgressBar();
                break;
            case PlayModeStateChange.ExitingEditMode:
                break;
            case PlayModeStateChange.EnteredPlayMode:
                if (window.targetID == 0)
                    return;
                target = EditorUtility.InstanceIDToObject(window.targetID) as BitAnimator;
                window.targetID = 0;
                if (target != null)
                    SetupRuntimeAnimation(target);
                break;
            case PlayModeStateChange.ExitingPlayMode:
                break;
        }
    }
    public static void SetupRuntimeAnimation(BitAnimator bitAnimator)
    {
        Animator animator = bitAnimator.animatorObject;
        //animator.runtimeAnimatorController = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(Path.ChangeExtension(bitAnimator.animationAssetPath, "controller"), clip);
        animation = animator.gameObject.GetComponent<Animation>();
        if(animation == null)
            animation = animator.gameObject.AddComponent<Animation>();
        animation.playAutomatically = false;
        AnimationState runtimeAnimation = animation["BitAnimator.RuntimeAnimation"];
        if (runtimeAnimation == null)
        {
            AnimationClip clip = new AnimationClip
            {
                name = bitAnimator.audioClip.name,
                legacy = true
            };
            animation.AddClip(clip, "BitAnimator.RuntimeAnimation");
            runtimeAnimation = animation["BitAnimator.RuntimeAnimation"];
        }
        animation.clip = runtimeAnimation.clip;
        animation.clip.frameRate = (float)bitAnimator.audioClip.frequency / (1 << bitAnimator.FFTWindowLog) * bitAnimator.multisamples;
        Action<BitAnimator> updateCallback = (BitAnimator b) =>
        {
            EditorUtility.DisplayProgressBar("Creating animation", b.status, b.taskProgress);
        };
        Action<BitAnimator> finishCallback = (BitAnimator b) =>
        {
            b.core.FreeMemory();
            isRunningTask = false;
            executionTime = Time.realtimeSinceStartup - executionTime;
            EditorUtility.ClearProgressBar();
            animation.Play();
            //audio.enabled = true;
            if(!audio.isPlaying)
                audio.Play();
        };
        bitAnimator.StartCoroutine(bitAnimator.ComputeAnimation(runtimeAnimation.clip, finishCallback, updateCallback));
        if (EditorApplication.isPlaying)
            bitAnimator.animatorObject.enabled = false;
        isRunningTask = true;
        executionTime = Time.realtimeSinceStartup;
        
        audio = bitAnimator.animatorObject.gameObject.GetComponentInChildren<AudioSource>();
        if (audio == null)
            audio = bitAnimator.animatorObject.gameObject.AddComponent<AudioSource>();

        if (audio.clip != bitAnimator.audioClip)
        {
            audio.clip = bitAnimator.audioClip;
            audio.volume = 0.25f;
            audio.minDistance = 10;
            audio.dopplerLevel = 0;
            audio.spatialBlend = 0;
            audio.spatialize = false;
        }
    }
    public static void WriteAnimation(BitAnimator bitAnimator)
    {
        Action<BitAnimator> updateCallback = (BitAnimator b) =>
        {
            EditorUtility.DisplayProgressBar("Creating animation", b.status, b.taskProgress);
        };
        Action<BitAnimator> finishCallback = (BitAnimator b) =>
        {
            b.core.FreeMemory();
            isRunningTask = false;
            executionTime = Time.realtimeSinceStartup - executionTime;
            EditorUtility.ClearProgressBar();
        };
        bitAnimator.StartCoroutine(bitAnimator.CreateAnimation(finishCallback, updateCallback));
        executionTime = Time.realtimeSinceStartup;
        isRunningTask = true;
    }
}
#endif