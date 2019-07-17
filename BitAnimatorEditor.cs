
// Copyright © 2019 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.0 (17.07.2019)

#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AudioVisualization;
using AudioVisualization.Modificators;
using System.IO;
using System.Text;

[CustomEditor(typeof(BitAnimator))]
public class BitAnimatorEditor : Editor 
{
    public enum InterfaceMode
    {
        Simple, Normal, Expert
    }
    public class FrequencyBand
    {
        public static string[] name = new string[] { "Low-Bit", "Bit", "High-Bit", "OverHigh-Bit", "Mid", "Low-Highs", "Highs", "Over-Highs(16k)", "YouGotThat(20k)", "Peak" };
        public static int[] bandStart  = new int[] {         0,    50,        600,           1000,  2000,        2400,    4000,             16000,             16500,  20000 };
        public static int[] bandEnd    = new int[] {        50,   150,        800,           2000,  2400,        4000,   16000,             16500,             20000,  22050 };
    }
    public struct Preset
    {
        public string name;
        public int fftWindowSize;
        public int multisamples;
        public float multiply;
        public DSPLib.DSP.Window.Type window;
        public float windowParameter;
        public float smoothSpectrum;
        public float damping;
        public float deСonvolution;
        public float convolution;
        public float frequencyBandStart;
        public float frequencyBandEnd;
        public float curveQuality;
        public bool calcFons;
        public bool deltaLoudness;
        public bool energyCorrection;
    }
    SerializedProperty _interfaceMode;
    SerializedProperty animatorObject;
	SerializedProperty targetObject;
	SerializedProperty audioClip;
    SerializedProperty animationClip;
    SerializedProperty record;
	SerializedProperty FFTWindowLog;
	SerializedProperty multisamples;
	SerializedProperty quality;
	SerializedProperty filter;
    SerializedProperty windowParam;
    SerializedProperty multiply;
    SerializedProperty deconvolution;
    SerializedProperty calculateLoudness;
    SerializedProperty energyCorrection;
    SerializedProperty calculateVelocity;
    SerializedProperty normalizeSpectrum;
	SerializedProperty animationAsset;
	SerializedProperty recordSlots;
	SerializedProperty recalculateSpectrum;
    SerializedProperty presetName;

    BitAnimator bitAnimator;
	Animator animatorGO;
	GameObject targetGO;
	Renderer renderer;
	ParticleSystem particles;
	AudioClip audio;
    InterfaceMode interfaceMode;
	bool advancedSettingsToggle;
	bool updateList = true;
	List<BitAnimator.RecordSlot> availableVariables = new List<BitAnimator.RecordSlot>();
    string[] availableProperties;
    GenericMenu menu = new GenericMenu();
    GenericMenu modMenu = new GenericMenu();
    bool[] expanded = new bool[0];
    Shader editorShader;
    public TextAsset serializedPresets;
    static Preset[] presets;
    static bool updateAnimation;

    public BitAnimatorEditor()
    {
        if (serializedPresets != null)
            presets = JsonUtility.FromJson<Preset[]>(serializedPresets.text);
        else
            presets = new Preset[0];
    }
    void OnEnable()
	{
        // Setup the SerializedProperties.
        _interfaceMode = serializedObject.FindProperty("_interfaceMode");
        animatorObject = serializedObject.FindProperty ("animatorObject");
		targetObject = serializedObject.FindProperty ("targetObject");
		audioClip = serializedObject.FindProperty ("audioClip");
        animationClip = serializedObject.FindProperty("animationClip");
        record = serializedObject.FindProperty ("record");
		FFTWindowLog = serializedObject.FindProperty ("FFTWindowLog");
		multisamples = serializedObject.FindProperty ("multisamples");
		quality = serializedObject.FindProperty ("quality");
		filter = serializedObject.FindProperty ("filter");
        windowParam = serializedObject.FindProperty("windowParam");
        multiply = serializedObject.FindProperty ("multiply");
        deconvolution = serializedObject.FindProperty("deconvolution");
        calculateLoudness = serializedObject.FindProperty ("calculateLoudness");
        energyCorrection = serializedObject.FindProperty("energyCorrection");
        calculateVelocity = serializedObject.FindProperty("calculateVelocity");
        normalizeSpectrum = serializedObject.FindProperty ("normalizeSpectrum");
		animationAsset = serializedObject.FindProperty ("animationAssetPath");
		recordSlots = serializedObject.FindProperty ("recordSlots");
		recalculateSpectrum = serializedObject.FindProperty ("recalculateSpectrum");
        presetName = serializedObject.FindProperty("presetName");

        bitAnimator = target as BitAnimator;
        /*foreach(var slot in bitAnimator.recordSlots)
        {
            slot.modificators = JsonUtility.FromJson(slot.serializedMods)
        }*/
        modMenu.AddItem(new GUIContent("Time range"), false, () => 
        {
            int i = BitAnimatorWindow.selectedSlot;
            TimeRange timeRange = ScriptableObject.CreateInstance<TimeRange>();
            timeRange.name = "Time range [" + bitAnimator.recordSlots[i].modificators.Count + "]";
            timeRange.end = timeRange.fadeOut = audio.length;
            bitAnimator.recordSlots[i].modificators.Add(timeRange);
        });
    }
	public override void OnInspectorGUI()
	{
		serializedObject.Update ();
        
        EditorGUI.BeginChangeCheck ();
		EditorGUILayout.PropertyField(audioClip, new GUIContent("Source Audioclip"));
		audio = audioClip.objectReferenceValue as AudioClip;

		EditorGUI.BeginChangeCheck ();
		EditorGUILayout.PropertyField(animatorObject, new GUIContent("Animator"));
		animatorGO = animatorObject.objectReferenceValue as Animator;
		if (animatorGO == null) 
		{
			animatorGO = bitAnimator.GetComponent<Animator> ();
			if (animatorGO != null) 
			{
				animatorObject.objectReferenceValue = animatorGO;
				GUI.changed = true;
			}
			else 
			{
				EditorGUI.EndChangeCheck ();
				EditorGUI.EndChangeCheck ();
				serializedObject.ApplyModifiedProperties ();
				return;
			}
		}
		if (EditorGUI.EndChangeCheck () && audio == null && animatorGO != null)
		{
			AudioSource audioSource = animatorGO.GetComponentInChildren<AudioSource> ();
			audio = audioSource != null ? audioSource.clip : null;
			audioClip.objectReferenceValue = audio;
		}

		EditorGUILayout.PropertyField(targetObject, new GUIContent("Target GameObject"));
		targetGO = targetObject.objectReferenceValue as GameObject;
		if (targetGO == null) 
		{
			targetObject.objectReferenceValue = targetGO = animatorGO.gameObject;
			GUI.changed = true;
		}
		else if (targetGO != animatorGO && !targetGO.transform.IsChildOf(animatorGO.transform)) 
		{
			EditorGUI.EndChangeCheck ();
			EditorGUILayout.HelpBox("Target gameobject must be a child of animator object", MessageType.Warning);
			serializedObject.ApplyModifiedProperties ();
			return;
		}
		{
			Renderer currentRenderer = targetGO.GetComponent<Renderer> ();
			if (currentRenderer != renderer)
				updateList = true;
			else if (currentRenderer != null && renderer != null) 
			{
				Material currentMaterial = currentRenderer.sharedMaterial;
				Material material = renderer.sharedMaterial;
				if (currentMaterial != material)
					updateList = true;
				else if (currentMaterial != null && material != null) 
				{
					Shader currentShader = currentMaterial.shader;
					if (editorShader != currentShader)
					{
						editorShader = currentShader;
						updateList = true;
					}
				}
			}
			ParticleSystem currentParticles = targetGO.GetComponent<ParticleSystem> ();
			if (currentParticles != particles)
				updateList = true;
			renderer = currentRenderer;
			particles = currentParticles;
		}

        if (EditorGUI.EndChangeCheck())
        {
            recalculateSpectrum.boolValue = updateAnimation = true;
        }
		
		if (audio == null) 
		{
			serializedObject.ApplyModifiedProperties ();
			return;
		}
        EditorGUILayout.PropertyField(_interfaceMode, new GUIContent("Interface mode", "How much details do you want to control"));
        interfaceMode = (InterfaceMode)_interfaceMode.enumValueIndex;

        
        //EditorGUILayout.PropertyField(record, new GUIContent("Record mode", "Clear - clear animation and write new\nOverwrite - add new properties and rewrite already exists"));
        //EditorGUILayout.LabelField ("Frequency limits", string.Format("Low limit - {0:F1}Hz --- High limit - {1}Hz", minFreq, audio.frequency / 2));

        RenderAdvancedSettings ();
		if (updateList) 
		{
            menu = new GenericMenu();
            availableVariables.Clear();
			UpdateBlendShapeProperties ();
			UpdateParticleProperties ();
			UpdateShaderProperties ();
			UpdateTransformProperties ();
            availableProperties = availableVariables.Select(x => x.description).ToArray();
            updateList = false;
        }

		RenderShaderProperties ();
		RenderAddShaderProperties ();

		EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(animationClip, new GUIContent("Animation clip", "Animation will be recorded to this animation clip"));
        //EditorGUILayout.PropertyField (animationAsset, new GUIContent ("Save path", "Animation save path (*.anim)"));
        if (GUILayout.Button(new GUIContent("...", "Save as..."), GUILayout.MaxWidth(24), GUILayout.MaxHeight(16)))
        {
            animationAsset.stringValue = EditorUtility.SaveFilePanelInProject("BitAnimator", targetGO.name + ".anim", "anim", "Save animation clip");
            LoadOrCreateAnimationFile();
        }
		EditorGUILayout.EndHorizontal();

		serializedObject.ApplyModifiedProperties ();

		EditorGUILayout.Space ();
		if(audio.loadType != AudioClipLoadType.DecompressOnLoad)
			EditorGUILayout.HelpBox("Cannot get data on compressed samples. Change load type to DecompressOnLoad on the Audioclip", MessageType.Error);

        using (new EditorGUI.DisabledScope(audio.loadType != AudioClipLoadType.DecompressOnLoad || BitAnimatorWindow.isRunningTask))
        {
            EditorGUILayout.BeginHorizontal();
            Color oldBackground = GUI.backgroundColor;
            if (EditorApplication.isPlaying)
            {
                GUI.backgroundColor = Color.Lerp(GUI.backgroundColor, 0.75f * Color.green, 0.5f);
                if (GUILayout.Button(updateAnimation && BitAnimatorWindow.animation != null ? "*Update animation*" : "Test animation", GUILayout.MinHeight(30)))
                {
                    BitAnimatorWindow.SetupRuntimeAnimation(bitAnimator);
                    updateAnimation = false;
                }
            }
            else
            {
                GUI.backgroundColor = Color.Lerp(GUI.backgroundColor, 0.75f * Color.green, 0.5f);
                if (GUILayout.Button(new GUIContent("Test in PlayMode", "Launch playmode, create an animation and test"), GUILayout.MinHeight(30)))
                {
                    BitAnimatorWindow window = EditorWindow.GetWindow<BitAnimatorWindow>();
                    window.targetID = bitAnimator.GetInstanceID();
                    EditorApplication.isPlaying = true;
                }
                GUI.backgroundColor = oldBackground;
                GUI.backgroundColor = Color.Lerp(GUI.backgroundColor, Color.blue + 0.5f * Color.green, 0.3f);
                GUIContent testInEditMode;
                if (updateAnimation && BitAnimatorWindow.animation != null)
                    testInEditMode = new GUIContent("*Update animation*");
                else
                    testInEditMode = new GUIContent("Test in EditMode", "Create an animation and test it now");
                if (GUILayout.Button(testInEditMode, GUILayout.MinHeight(30)))
                {
                    BitAnimatorWindow.SetupRuntimeAnimation(bitAnimator);
                    updateAnimation = false;
                }
            }
            GUI.backgroundColor = oldBackground;
            if (GUILayout.Button("Record animation", GUILayout.MinHeight(30)))
            {
                if (animationClip.objectReferenceValue == null)
                {
                    animationAsset.stringValue = EditorUtility.SaveFilePanelInProject("Save as...", targetGO.name + ".anim", "anim", "Save animation clip");
                    LoadOrCreateAnimationFile();
                    serializedObject.ApplyModifiedProperties();
                }
                if(animationClip.objectReferenceValue != null)
                    BitAnimatorWindow.WriteAnimation(bitAnimator);
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (BitAnimatorWindow.executionTime > 0 && !BitAnimatorWindow.isRunningTask)
                EditorGUILayout.LabelField(string.Format("Recording time: {0:F3} sec.", BitAnimatorWindow.executionTime));
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.Lerp(GUI.backgroundColor, Color.red, 0.25f);
            if (!EditorApplication.isPlaying && BitAnimatorWindow.animation != null && BitAnimatorWindow.target == bitAnimator)
            {
                if (GUILayout.Button("Stop test", GUILayout.MinHeight(30), GUILayout.MaxWidth(80)))
                    BitAnimatorWindow.ResetState();
            }
            GUI.backgroundColor = Color.Lerp(GUI.backgroundColor, Color.red, 0.25f);
            AnimationClip clip = animationClip.objectReferenceValue as AnimationClip;
            if (clip != null && !clip.empty && GUILayout.Button(new GUIContent("Clear animation clip")))
                bitAnimator.ClearAnimation();
            GUI.backgroundColor = oldBackground;
            EditorGUILayout.EndHorizontal();
        }
        
    }
    void LoadOrCreateAnimationFile()
    {
        if (!String.IsNullOrEmpty(animationAsset.stringValue))
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animationAsset.stringValue);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, animationAsset.stringValue);
            }
            animationClip.objectReferenceValue = clip;
        }
    }
	private void UpdateShaderProperties()
	{
        if (editorShader == null)
        {
            menu.AddDisabledItem(new GUIContent("Material"));
            return;
        }
		for (int i = 0; i < ShaderUtil.GetPropertyCount (editorShader); i++) 
		{
            BitAnimator.RecordSlot prop = new BitAnimator.RecordSlot ();
			prop.type = (BitAnimator.RecordSlot.PropertyType)(int)ShaderUtil.GetPropertyType (editorShader, i);
			if (ShaderUtil.IsShaderPropertyHidden (editorShader, i) || prop.type == BitAnimator.RecordSlot.PropertyType.TexEnv)
				continue;
			prop.typeSet = BitAnimator.RecordSlot.PropertiesSet.Material;
			prop.name = ShaderUtil.GetPropertyName (editorShader, i);
			prop.property = GetPropertyString (prop.type, "material." + prop.name);
			prop.description = string.Format("Material/{0} ({1})", ShaderUtil.GetPropertyDescription (editorShader, i), prop.name);
			prop.startFreq = 0;
			prop.endFreq = 120;
			prop.rangeMin = ShaderUtil.GetRangeLimits (editorShader, i, 1);
			prop.rangeMax = ShaderUtil.GetRangeLimits (editorShader, i, 2);
            prop.remap = new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 3, 0));
            prop.ampSmoothness = 0;
            prop.damping = 0.3f;
            prop.channelMask = 0xFF;
            prop.accumulate = false;
            prop.loops = 1;
            availableVariables.Add (prop);
            menu.AddItem(new GUIContent(prop.description), false, AddProperty, prop);
        }
	}
	public struct ParticleAnimationProperty
	{
		public string name;
		public string propertyName;
		public BitAnimator.RecordSlot.PropertyType type;
	}
	private void UpdateParticleProperties()
	{
        if (particles == null)
        {
            menu.AddDisabledItem(new GUIContent("Particle System"));
            return;
        }
		var d = new []{
			new { name = "Particle System/Start Size/X (main)", propertyName = "InitialModule.startSize.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Start Size/Y", propertyName = "InitialModule.startSizeY.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Start Size/Z", propertyName = "InitialModule.startSizeZ.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Start Rotation/Z (main)", propertyName = "InitialModule.startRotation.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Start Rotation/X", propertyName = "InitialModule.startRotationX.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Start Rotation/Y", propertyName = "InitialModule.startRotationY.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Start Speed", propertyName = "InitialModule.startSpeed.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Start Lifetime", propertyName = "InitialModule.startLifetime.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Start Color", propertyName = "InitialModule.startColor.maxColor", type = BitAnimator.RecordSlot.PropertyType.Color},
			new { name = "Particle System/Start Color 2", propertyName = "InitialModule.startColor.minColor", type = BitAnimator.RecordSlot.PropertyType.Color},
			new { name = "Particle System/Gravity", propertyName = "InitialModule.gravityModifier.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Simulation Speed", propertyName = "simulationSpeed", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Emission/Rate Over Time", propertyName = "EmissionModule.rateOverTime.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Emission/Rate Over Distance", propertyName = "EmissionModule.rateOverDistance.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Shape/Angle", propertyName = "ShapeModule.angle", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Shape/Radius", propertyName = "ShapeModule.radius.value", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Shape/Arc/Angle", propertyName = "ShapeModule.arc.value", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Shape/Arc/Spread", propertyName = "ShapeModule.arc.spread", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Shape/Arc/Speed", propertyName = "ShapeModule.arc.speed.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Shape/Box size/X", propertyName = "ShapeModule.boxX", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Shape/Box size/Y", propertyName = "ShapeModule.boxY", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Shape/Box size/Z", propertyName = "ShapeModule.boxZ", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Shape/Randomize direction", propertyName = "ShapeModule.randomDirectionAmount", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Shape/Spherize direction", propertyName = "ShapeModule.sphericalDirectionAmount", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Velocity Over Lifetime/X", propertyName = "VelocityModule.x.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Velocity Over Lifetime/Y", propertyName = "VelocityModule.y.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Velocity Over Lifetime/Z", propertyName = "VelocityModule.z.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Limit Velocity over Lifetime/Dampen", propertyName = "ClampVelocityModule.dampen", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Limit Velocity over Lifetime/X", propertyName = "ClampVelocityModule.x.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Limit Velocity over Lifetime/Y", propertyName = "ClampVelocityModule.y.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Limit Velocity over Lifetime/Z", propertyName = "ClampVelocityModule.z.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			//new { name = "Particle System/Inherit Velocity", propertyName = "InheritVelocityModule.Curve.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Size over Lifetime/X (main)", propertyName = "SizeModule.curve.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Size over Lifetime/Y", propertyName = "SizeModule.y.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Size over Lifetime/Z", propertyName = "SizeModule.z.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Force over Lifetime/X", propertyName = "ForceModule.x.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Force over Lifetime/Y", propertyName = "ForceModule.y.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Force over Lifetime/Z", propertyName = "ForceModule.z.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Rotation over Lifetime/Z (main)", propertyName = "RotationModule.curve.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Rotation over Lifetime/X", propertyName = "RotationModule.x.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Rotation over Lifetime/Y", propertyName = "RotationModule.y.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Rotation by Speed/Z (main)", propertyName = "RotationBySpeedModule.curve.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Rotation by Speed/X", propertyName = "RotationBySpeedModule.x.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Rotation by Speed/Y", propertyName = "RotationBySpeedModule.y.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/External Forces", propertyName = "ExternalForcesModule.multiplier", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Noise/Strength/X (main)", propertyName = "NoiseModule.strength.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Noise/Strength/Y", propertyName = "NoiseModule.strengthY.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Noise/Strength/Z", propertyName = "NoiseModule.strengthZ.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Noise/Frequency", propertyName = "NoiseModule.frequency", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Noise/Scroll Speed", propertyName = "NoiseModule.scrollSpeed.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Noise/Octave multiplier", propertyName = "NoiseModule.octaveMultiplier", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Noise/Octave scale", propertyName = "NoiseModule.octaveScale", type = BitAnimator.RecordSlot.PropertyType.Float},
			//new { name = "Particle System/Collision/Bounce", propertyName = "CollisionModule.Bounce.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			//new { name = "Particle System/Collision/Dampen", propertyName = "CollisionModule.Dampen.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			//new { name = "Particle System/Collision/Energy Loss on Collision", propertyName = "CollisionModule.EnergyLossOnCollision.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			//new { name = "Particle System/Collision/Max kill speed", propertyName = "CollisionModule.maxKillSpeed", type = BitAnimator.RecordSlot.PropertyType.Float},
			//new { name = "Particle System/Collision/Min kill speed", propertyName = "CollisionModule.minKillSpeed", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Lights/Ratio", propertyName = "LightsModule.ratio", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Lights/Intensity", propertyName = "LightsModule.intensityCurve.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Lights/Range", propertyName = "LightsModule.rangeCurve.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Trails/Ratio", propertyName = "TrailModule.ratio", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Trails/Lifetime", propertyName = "TrailModule.lifetime.scalar", type = BitAnimator.RecordSlot.PropertyType.Float},
			new { name = "Particle System/Trails/Width Over Trail", propertyName = "TrailModule.widthOverTrail.scalar", type = BitAnimator.RecordSlot.PropertyType.Float}
		};
		foreach (var particleVariable in d) 
		{
            BitAnimator.RecordSlot prop = new BitAnimator.RecordSlot ();
			prop.type = particleVariable.type;
			prop.typeSet = BitAnimator.RecordSlot.PropertiesSet.ParticleSystem;
			prop.name = particleVariable.name;
			prop.property = GetPropertyString(prop.type, particleVariable.propertyName);
			prop.description = particleVariable.name;
			prop.startFreq = 0;
			prop.endFreq = audio.frequency / 2;
            prop.remap = new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 3, 0));
            prop.ampSmoothness = 0;
            prop.damping = 0;
            prop.channelMask = 0xFF;
            prop.accumulate = false;
            prop.loops = 1;
            availableVariables.Add (prop);
            menu.AddItem(new GUIContent(prop.description), false, AddProperty, prop);
        }
	}

	public void UpdateTransformProperties()
	{
		var d = new []{
			new { name = "Transform/Position", propertyName = "localPosition", type = BitAnimator.RecordSlot.PropertyType.Vector3, minValue = Vector3.zero, maxValue = Vector3.zero},
			new { name = "Transform/Rotation", propertyName = "m_LocalRotation", type = BitAnimator.RecordSlot.PropertyType.Quaternion, minValue = Vector3.zero, maxValue = Vector3.zero},
			new { name = "Transform/Scale", propertyName = "localScale", type = BitAnimator.RecordSlot.PropertyType.Vector3, minValue = Vector3.one, maxValue = Vector3.one},
		};
		foreach (var transformVariable in d) 
		{
            BitAnimator.RecordSlot prop = new BitAnimator.RecordSlot ();
			prop.type = transformVariable.type;
			prop.typeSet = BitAnimator.RecordSlot.PropertiesSet.Transform;
			prop.name = transformVariable.name;
			prop.property = GetPropertyString(prop.type, transformVariable.propertyName);
			prop.description = transformVariable.name;
			prop.startFreq = 0;
			prop.endFreq = 300;
            prop.minValue = transformVariable.minValue;
            prop.maxValue = transformVariable.maxValue;
            prop.remap = AnimationCurve.Linear(0, 0, 1, 1);
            prop.ampSmoothness = 0.2f;
            prop.damping = 0.2f;
            prop.channelMask = 0xFF;
            prop.accumulate = false;
            prop.loops = 1;
            availableVariables.Add (prop);
            menu.AddItem(new GUIContent(prop.description), false, AddProperty, prop);
        }
	}

	public void UpdateBlendShapeProperties() 
	{
		SkinnedMeshRenderer skinMeshObj = renderer as SkinnedMeshRenderer;
        if (skinMeshObj == null)
        {
            menu.AddDisabledItem(new GUIContent("Blend Shapes"));
            return;
        }
		Mesh blendShapeMesh = skinMeshObj.sharedMesh;
		int blendShapeCount = blendShapeMesh.blendShapeCount;
		for (int i = 0; i < blendShapeCount; i++) {
			string blendShapeName = blendShapeMesh.GetBlendShapeName (i);
            BitAnimator.RecordSlot prop = new BitAnimator.RecordSlot ();
			prop.type = BitAnimator.RecordSlot.PropertyType.Float;
			prop.typeSet = BitAnimator.RecordSlot.PropertiesSet.BlendShape;
			prop.name = blendShapeName;
			prop.property = new string[1]{"blendShape." + blendShapeName};
			prop.description = "Blend Shapes/" + blendShapeName;
			prop.startFreq = 500;
			prop.endFreq = 1000;
			prop.rangeMin = 0;
			prop.rangeMax = 100;
            prop.remap = AnimationCurve.Linear(0, 0, 1, 1);
            prop.ampSmoothness = 0.2f;
            prop.damping = 0;
            prop.channelMask = 0xFF;
            prop.accumulate = false;
            prop.loops = 1;
            availableVariables.Add (prop);
            menu.AddItem(new GUIContent(prop.description), false, AddProperty, prop);
        }
	}

	private string[] GetPropertyString(BitAnimator.RecordSlot.PropertyType type, string name)
	{
		string[] prop = null;
		switch (type) {
		case BitAnimator.RecordSlot.PropertyType.Float:
		case BitAnimator.RecordSlot.PropertyType.Range:
			prop = new string[1];
			prop [0] = name;
			break;
		case BitAnimator.RecordSlot.PropertyType.Vector:
		case BitAnimator.RecordSlot.PropertyType.Quaternion:
			prop = new string[4];
			prop [0] = name + ".x";
			prop [1] = name + ".y";
			prop [2] = name + ".z";
			prop [3] = name + ".w";
			break;
		case BitAnimator.RecordSlot.PropertyType.Color:
			prop = new string[4];
			prop [0] = name + ".r";
			prop [1] = name + ".g";
			prop [2] = name + ".b";
			prop [3] = name + ".a";
			break;
		case BitAnimator.RecordSlot.PropertyType.Vector3:
			prop = new string[3];
			prop [0] = name + ".x";
			prop [1] = name + ".y";
			prop [2] = name + ".z";
			break;
		}
		return prop;
	}

	private void RenderAdvancedSettings()
	{
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (interfaceMode == InterfaceMode.Simple)
        {
            float q = quality.floatValue;
            int idQuality;
            float[] qualityVariants = new float[] { 0, 0.3f, 0.5f, 0.7f, 1.0f };
            for (idQuality = qualityVariants.Length - 1; idQuality > 0; idQuality--)
                if (q >= qualityVariants[idQuality])
                    break;
            GUIContent[] options = new GUIContent[] { new GUIContent("Maximum compression"), new GUIContent("Balance"), new GUIContent("Normal"), new GUIContent("High quality"), new GUIContent("Lossless") };
            EditorGUI.BeginChangeCheck();
            idQuality = EditorGUILayout.Popup(new GUIContent("Animation quality", "0.0 - Maximum compression\n0.5 - normal quality\n1.0 - lossless"), idQuality, options);
            if (EditorGUI.EndChangeCheck())
                quality.floatValue = idQuality;
            string[] displayedPresets = presets.Select(p => p.name).ToArray();
            int selectedPreset = Array.IndexOf(displayedPresets, presetName.stringValue);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            selectedPreset = EditorGUILayout.Popup("Preset", selectedPreset, displayedPresets);
            if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus", "Add preset"), EditorStyles.miniButtonLeft, GUILayout.MaxWidth(24), GUILayout.MaxHeight(16)))
            {
            }
            if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus", "Remove preset"), EditorStyles.miniButtonRight, GUILayout.MaxWidth(24), GUILayout.MaxHeight(16)))
            {
            }
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {

            }
            EditorGUILayout.PropertyField(FFTWindowLog, new GUIContent("Sample size", "(FFT Window size) Higher values give more accuracy but also more input-lag"));
        }
        else
        {
            EditorGUI.indentLevel += 1;
            advancedSettingsToggle = EditorGUILayout.Foldout(advancedSettingsToggle, "Advanced Settings", toggleOnLabelClick: true);
            if (advancedSettingsToggle)
            {
                EditorGUILayout.PropertyField(quality, new GUIContent("Animation quality", "0.0 - Maximum compression\n0.5 - normal quality\n1.0 - lossless"));
                EditorGUILayout.PropertyField(FFTWindowLog, new GUIContent("Sample size", "(FFT Window size) Higher values give more accuracy but also more input-lag"));
                EditorGUILayout.PropertyField(multisamples, new GUIContent("Multisamples", "More samples - higher precision"));
                EditorGUILayout.PropertyField(calculateLoudness, new GUIContent("Calculate loudness", "Calculate volume adjusted for human perception"));
                EditorGUILayout.PropertyField(energyCorrection, new GUIContent("Calculate energy", "Normilize spectrum by wave frequency energy (E = frequency*amplitude)"));
                if (interfaceMode >= InterfaceMode.Expert)
                {
                    //EditorGUILayout.PropertyField(calculateVelocity, new GUIContent("Calculate velocity", "Take only change of volume"));
                    //EditorGUILayout.PropertyField(normalizeSpectrum, new GUIContent("Normalize", "Scale volume to given range"));
                    EditorGUILayout.PropertyField(multiply, new GUIContent("Multiplier", "Force scale values"));
                    EditorGUILayout.PropertyField(deconvolution, new GUIContent("Deconvolution", "[EXPERIMENTAL] Reconstruct detail values"));

                    EditorGUILayout.PropertyField(filter, new GUIContent("Filter", "Which a window function apply to audio to reduce noise"));

                    if (DSPLib.DSP.Window.IsParametrizedWindow((DSPLib.DSP.Window.Type)filter.intValue))
                        EditorGUILayout.PropertyField(windowParam, new GUIContent("Filter parameter", "Side-lobe level in decibels"));
                }
                int windowSize = 1 << FFTWindowLog.intValue;
                EditorGUILayout.LabelField(String.Format("Window size = {0} samples, {1:F3} sec/window", windowSize, (float)windowSize / audio.frequency));
                EditorGUILayout.LabelField(String.Format("Samples per second = {0:F3}", (float)audio.frequency / windowSize * multisamples.intValue));
            }
            EditorGUI.indentLevel -= 1;
        }
        EditorGUILayout.EndVertical();
        if (EditorGUI.EndChangeCheck())
        {
            updateAnimation = true;
            recalculateSpectrum.boolValue = true;
            if (bitAnimator.core != null)
            {
                serializedObject.ApplyModifiedProperties();
                bitAnimator.Setup();
            }
        }
    }

    void AddProperty(object obj)
    {
        BitAnimator.RecordSlot slot = obj as BitAnimator.RecordSlot;
        recordSlots.InsertArrayElementAtIndex(recordSlots.arraySize);
        SerializedProperty serializedProp = recordSlots.GetArrayElementAtIndex(recordSlots.arraySize - 1);
        SerializedProperty name = serializedProp.FindPropertyRelative("name");
        SerializedProperty property = serializedProp.FindPropertyRelative("property");
        SerializedProperty type = serializedProp.FindPropertyRelative("type");
        SerializedProperty typeSet = serializedProp.FindPropertyRelative("typeSet");
        SerializedProperty description = serializedProp.FindPropertyRelative("description");
        SerializedProperty startFreq = serializedProp.FindPropertyRelative("startFreq");
        SerializedProperty endFreq = serializedProp.FindPropertyRelative("endFreq");
        SerializedProperty minValue = serializedProp.FindPropertyRelative("minValue");
        SerializedProperty maxValue = serializedProp.FindPropertyRelative("maxValue");
        SerializedProperty rangeMin = serializedProp.FindPropertyRelative("rangeMin");
        SerializedProperty rangeMax = serializedProp.FindPropertyRelative("rangeMax");
        //SerializedProperty colors = serializedProp.FindPropertyRelative("colors");
        SerializedProperty remap = serializedProp.FindPropertyRelative("remap");
        SerializedProperty ampSmoothness = serializedProp.FindPropertyRelative("ampSmoothness");
        SerializedProperty damping = serializedProp.FindPropertyRelative("damping");
        SerializedProperty channelMask = serializedProp.FindPropertyRelative("channelMask");
        SerializedProperty accumulate = serializedProp.FindPropertyRelative("accumulate");
        SerializedProperty loops = serializedProp.FindPropertyRelative("loops");
        SerializedProperty modificators = serializedProp.FindPropertyRelative("modificators");
        if (name.stringValue != slot.name)
        {
            type.intValue = (int)slot.type;
            typeSet.intValue = (int)slot.typeSet;
            name.stringValue = slot.name;
            property.ClearArray();
            foreach (string s in slot.property)
            {
                property.InsertArrayElementAtIndex(property.arraySize);
                property.GetArrayElementAtIndex(property.arraySize - 1).stringValue = s;
            }
            description.stringValue = slot.description;
            startFreq.intValue = slot.startFreq;
            endFreq.intValue = slot.endFreq;
            rangeMin.floatValue = slot.rangeMin;
            rangeMax.floatValue = slot.rangeMax;
            minValue.vector4Value = new Vector4(slot.rangeMin, 0, 0, 0);
            maxValue.vector4Value = new Vector4(slot.rangeMax, 0, 0, 0);
            remap.animationCurveValue = new AnimationCurve(slot.remap.keys);
            ampSmoothness.floatValue = slot.ampSmoothness;
            damping.floatValue = slot.damping;
            channelMask.intValue = slot.channelMask;
            accumulate.boolValue = slot.accumulate;
            loops.intValue = slot.loops;
            modificators.ClearArray();
            //  TODO: gradient not serialized 
            /*colors.objectReferenceValue = new Gradient(); 

            
            ((BitAnimator)target).recordSlots [recordSlots.arraySize - 1].colors = new Gradient();*/
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void RenderAddShaderProperties()
	{
        EditorGUILayout.Space();
        if (GUILayout.Button(new GUIContent("Add Property", "Add new (shader, particle system, blend shape, object transform) property")))
            menu.ShowAsContext();
        EditorGUILayout.Space();
    }

	private void RenderShaderProperties()
	{
        if(expanded.Length != recordSlots.arraySize)
        {
            bool[] newVisible = new bool[recordSlots.arraySize];
            for (int i = 0; i < expanded.Length && i < newVisible.Length; i++)
                newVisible[i] = expanded[i];
            for (int i = expanded.Length; i < newVisible.Length; i++)
                newVisible[i] = false;
            expanded = newVisible;
        }
        EditorGUILayout.Space();
        Color oldBackground = GUI.backgroundColor;
        GUIStyle style = new GUIStyle(EditorStyles.helpBox);
        EditorGUI.BeginChangeCheck();
        for (int i = 0; i < recordSlots.arraySize; ++i) 
		{
            if (BitAnimatorWindow.selectedSlot == i && BitAnimatorWindow.target == bitAnimator)
                GUI.backgroundColor = Color.Lerp(GUI.backgroundColor, Color.blue + 0.5f*Color.green, 0.3f);
            else
                GUI.backgroundColor = oldBackground;
            EditorGUILayout.BeginVertical(style);
            Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(16));
            EditorGUILayout.Space();
            int baseWidth = (int)((rect.width - 40) / 4);

            Rect foldoutRect = new Rect(rect.x + 15, rect.y, 20, rect.height);
            Rect popupRect = new Rect(rect.x + 20, rect.y, baseWidth * 3, rect.height);
            Rect removeRect = new Rect(rect.x + 35 + baseWidth * 3, rect.y, baseWidth, rect.height);

            bool wasCollapsed = !expanded[i];
            expanded[i] = EditorGUI.Foldout(foldoutRect, expanded[i], GUIContent.none);

            SerializedProperty serializedProp = recordSlots.GetArrayElementAtIndex(i);
			SerializedProperty name = serializedProp.FindPropertyRelative("name");
            if (expanded[i] && wasCollapsed)
            {
                BitAnimatorWindow.target = bitAnimator;
                BitAnimatorWindow.selectedSlot = i;
                BitAnimatorWindow.ResetView();
            }
            int current = -1;
            for (int b = 0; b < availableVariables.Count; ++b)
                if (name.stringValue == availableVariables[b].name)
                {
                    current = b;
                    break;
                }
			EditorGUILayout.Space ();
			int selected = EditorGUI.Popup (popupRect, String.Empty, current, availableProperties);
			if (GUI.Button (removeRect, "Remove"))
			{
				recordSlots.DeleteArrayElementAtIndex (i);
                BitAnimatorWindow.selectedSlot = Math.Min(BitAnimatorWindow.selectedSlot, recordSlots.arraySize - 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                continue;
			}
			EditorGUILayout.EndHorizontal();
            if (selected >= 0)
			{
				EditorGUI.indentLevel += 1;
				SerializedProperty type = serializedProp.FindPropertyRelative("type");
				SerializedProperty typeSet = serializedProp.FindPropertyRelative("typeSet");
				SerializedProperty property = serializedProp.FindPropertyRelative("property");
				SerializedProperty description = serializedProp.FindPropertyRelative("description");
				SerializedProperty minValue = serializedProp.FindPropertyRelative("minValue");
				SerializedProperty maxValue = serializedProp.FindPropertyRelative("maxValue");
                //SerializedProperty minRotation = serializedProp.FindPropertyRelative("minRotation");
                //SerializedProperty maxRotation = serializedProp.FindPropertyRelative("maxRotation");
                SerializedProperty startFreq = serializedProp.FindPropertyRelative("startFreq");
				SerializedProperty endFreq = serializedProp.FindPropertyRelative("endFreq");
				SerializedProperty rangeMin = serializedProp.FindPropertyRelative ("rangeMin");
				SerializedProperty rangeMax = serializedProp.FindPropertyRelative ("rangeMax");
				SerializedProperty colors = serializedProp.FindPropertyRelative("colors");
				SerializedProperty remap = serializedProp.FindPropertyRelative("remap");
                SerializedProperty ampSmoothness = serializedProp.FindPropertyRelative("ampSmoothness");
                SerializedProperty damping = serializedProp.FindPropertyRelative("damping");
                //SerializedProperty channelMask = serializedProp.FindPropertyRelative("channelMask");
                SerializedProperty accumulate = serializedProp.FindPropertyRelative("accumulate");
                SerializedProperty loops = serializedProp.FindPropertyRelative("loops");
                SerializedProperty modificators = serializedProp.FindPropertyRelative("modificators");

                if (selected != current)
				{
					type.intValue = (int)availableVariables [selected].type;
					typeSet.intValue = (int)availableVariables [selected].typeSet;
					property.ClearArray();
					foreach (string s in availableVariables [selected].property) 
					{
						property.InsertArrayElementAtIndex (property.arraySize);
						property.GetArrayElementAtIndex(property.arraySize - 1).stringValue = s;
					}
					name.stringValue = availableVariables [selected].name;
					description.stringValue = availableVariables [selected].description;
                    rangeMin.floatValue = availableVariables[selected].rangeMin;
                    rangeMax.floatValue = availableVariables[selected].rangeMax;
                    /*startFreq.intValue = availableVariables [next].startFreq;
                    endFreq.intValue = availableVariables [next].endFreq;
                    minValue.vector4Value = new Vector4 (availableVariables [next].rangeMin, 0, 0, 0);
                    maxValue.vector4Value = new Vector4 (availableVariables [next].rangeMax, 0, 0, 0);
                    remap.animationCurveValue = AnimationCurve.Linear (0, 0, 1.0f, 1.0f);
                    //colors = availableVariables [next].colors;
                    ampSmoothness.floatValue = availableVariables[next].ampSmoothness;
                    damping.floatValue = availableVariables[next].damping;
                    channelMask.intValue = availableVariables[next].channelMask;
                    accumulate.boolValue = availableVariables[next].accumulate;
                    loops.intValue = availableVariables[next].loops;*/
                }
                if (!expanded[i])
                {
                    EditorGUI.indentLevel -= 1;
                    EditorGUILayout.EndVertical();
                    continue;
                }
                switch ((BitAnimator.RecordSlot.PropertyType)type.intValue) 
				{
				case BitAnimator.RecordSlot.PropertyType.Float:
					minValue.vector4Value = new Vector4(EditorGUILayout.FloatField ("Min value", minValue.vector4Value.x), 0, 0, 0);
					maxValue.vector4Value = new Vector4(EditorGUILayout.FloatField ("Max value", maxValue.vector4Value.x), 0, 0, 0);
					break;
				case BitAnimator.RecordSlot.PropertyType.Range:
					minValue.vector4Value = new Vector4(EditorGUILayout.FloatField ("Min value", minValue.vector4Value.x), 0, 0, 0);
					maxValue.vector4Value = new Vector4(EditorGUILayout.FloatField ("Max value", maxValue.vector4Value.x), 0, 0, 0);
					break;
				case BitAnimator.RecordSlot.PropertyType.Vector:
					minValue.vector4Value = EditorGUILayout.Vector4Field ("Min value", minValue.vector4Value);
					maxValue.vector4Value = EditorGUILayout.Vector4Field ("Max value", maxValue.vector4Value);
					break;
				case BitAnimator.RecordSlot.PropertyType.Color:
					EditorGUILayout.PropertyField (colors);
					break;
				case BitAnimator.RecordSlot.PropertyType.TexEnv:
					EditorGUILayout.HelpBox("Textures haven't animation parameters", MessageType.Warning);
					break;
				case BitAnimator.RecordSlot.PropertyType.Vector3:
					minValue.vector4Value = EditorGUILayout.Vector3Field ("Min value", minValue.vector4Value);
					maxValue.vector4Value = EditorGUILayout.Vector3Field ("Max value", maxValue.vector4Value);
					break;
				case BitAnimator.RecordSlot.PropertyType.Quaternion:
                    //Значения задаются углами Эйлера. Потом эти углы будут конвертированы в кватернионы во время записи анимации
                    minValue.vector4Value = EditorGUILayout.Vector3Field("Min rotation", minValue.vector4Value);
                    maxValue.vector4Value = EditorGUILayout.Vector3Field("Max rotation", maxValue.vector4Value);
                    break;
				}

				float maxFreq = audio.frequency / 2.0f;
                float newStartFreq = BitAnimator.FromLogFrequency(startFreq.intValue / maxFreq);
                float newEndFreq = BitAnimator.FromLogFrequency(endFreq.intValue / maxFreq);
                if (interfaceMode == InterfaceMode.Simple)
                {
                    //NOTE: просто не знаю как лучше найти соответствие между полосой частот и пресетом, поэтому сейчас оставлю так
                    float avg = maxFreq * BitAnimator.ToLogFrequency((newStartFreq + newEndFreq) / 2.0f);
                    int band = -1;
                    for(int b = 0; b < FrequencyBand.bandEnd.Length; b++)
                    {
                        if (avg <= FrequencyBand.bandEnd[b])
                        {
                            band = b;
                            break;
                        }
                    }
                    EditorGUI.BeginChangeCheck();
                    band = EditorGUILayout.Popup("Frequency preset", band, FrequencyBand.name);
                    if (EditorGUI.EndChangeCheck())
                    {
                        startFreq.intValue = FrequencyBand.bandStart[band];
                        endFreq.intValue = FrequencyBand.bandEnd[band];
                    }
                }
                else
                {
                    //FIXME: bug with slider when moving both values
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.MinMaxSlider(new GUIContent("Frequencies (Hz)", "Frequency band-pass filter"), ref newStartFreq, ref newEndFreq, 0, 1);
                    if (EditorGUI.EndChangeCheck())
                    {
                        startFreq.intValue = Mathf.FloorToInt(maxFreq * BitAnimator.ToLogFrequency(newStartFreq));
                        endFreq.intValue = Mathf.FloorToInt(maxFreq * BitAnimator.ToLogFrequency(newEndFreq));
                    }
                    startFreq.intValue = EditorGUILayout.IntField(new GUIContent("Frequency start", "Frequency band-pass filter"), startFreq.intValue);
                    endFreq.intValue = EditorGUILayout.IntField(new GUIContent("Frequency end", "Frequency band-pass filter"), endFreq.intValue);
                }
                if (interfaceMode >= InterfaceMode.Normal)
                {
                    damping.floatValue = EditorGUILayout.Slider(new GUIContent("Damping", "How long peaks fading to minimum"), damping.floatValue, 0, 1);
                    ampSmoothness.floatValue = EditorGUILayout.FloatField(new GUIContent("Smoothness", "Smooths the animation curve"), ampSmoothness.floatValue);
                }
                if (interfaceMode >= InterfaceMode.Expert)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(accumulate, new GUIContent("Accumulate", "Sum peaks values over time"));
                    EditorGUILayout.PropertyField(loops, new GUIContent("Lerp repeats", "5 - loop 5 times\n1 - no looping (default)"));
                    EditorGUILayout.EndHorizontal();
                    /*int mask = channelMask.intValue;
                    int result = 0;
                    result |= EditorGUI.Toggle(xRect, channelMask.intValue & 1);
                    result |= EditorGUI.Toggle(yRect, channelMask.intValue & 2);
                    result |= EditorGUI.Toggle(zRect, channelMask.intValue & 4);
                    result |= EditorGUI.Toggle(wRect, channelMask.intValue & 8);*/

                    EditorGUILayout.PropertyField(remap);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Add mod"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        BitAnimatorWindow.selectedSlot = i;
                        modMenu.ShowAsContext();
                        
                        serializedObject.Update();
                    }
                    if (bitAnimator.recordSlots[i].modificators != null && GUILayout.Button("Clear mods"))
                    {
                        modificators.ClearArray();
                    }
                    EditorGUILayout.EndHorizontal();
                    if (modificators != null)
                    {
                        //int modsCount = modificators.arraySize;
                        Rect modsRect = EditorGUILayout.BeginVertical();
                        serializedObject.ApplyModifiedProperties();
                        EditorGUI.BeginChangeCheck();
                        for (int m = 0; m < modificators.arraySize; m++)
                        {
                            SerializedProperty modificator = modificators.GetArrayElementAtIndex(m);
                            Modificator mod = (Modificator)modificator.objectReferenceValue;
                            mod.DrawProperty();
                        }
                        if (EditorGUI.EndChangeCheck())
                            serializedObject.Update();
                        EditorGUILayout.EndVertical();
                    }
                }
                EditorGUILayout.Space();
                EditorGUI.indentLevel -= 1;
			}
            EditorGUILayout.EndVertical();
        }
        if (EditorGUI.EndChangeCheck())
        {
            updateAnimation = true;
            BitAnimatorWindow.ResetView();
        }
        GUI.backgroundColor = oldBackground;
    }
}

[CustomPropertyDrawer(typeof(TimeRange))]
public class TimeRangeDrawer : PropertyDrawer
{
    void OnEnable()
    {
        Debug.Log("TimeRangeDrawer enabled");
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty start = property.FindPropertyRelative("start");
        SerializedProperty end = property.FindPropertyRelative("end");
        SerializedProperty fadeIn = property.FindPropertyRelative("fadeIn");
        SerializedProperty fadeOut = property.FindPropertyRelative("fadeOut");
        float[] bounds = new float[] { start.floatValue, fadeIn.floatValue, end.floatValue, fadeOut.floatValue };
        GUIContent[] subLabels = new GUIContent[] { new GUIContent("start"), new GUIContent("fadeIn"), new GUIContent("end"), new GUIContent("fadeOut") };
        EditorGUI.MultiFloatField(position, subLabels, bounds);
        start.floatValue = bounds[0];
        fadeIn.floatValue = bounds[1];
        end.floatValue = bounds[2];
        fadeOut.floatValue = bounds[3];
    }
}
#endif