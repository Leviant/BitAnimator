
// Copyright © 2019 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.0 (14.07.2019)

#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;

namespace AudioVisualization.Modificators
{
    [Serializable]
    public class TimeRange: Modificator
    {
        public float start;
        public float end;
        public float fadeIn;
        public float fadeOut;
        public TimeRange() { useTempBuffer = false; }
        override public void Apply(ComputeProgram core, ComputeBuffer input, ComputeBuffer output)
        {
            core.ApplyCurveFilter(input, fadeIn, start, end, fadeOut);
        }
        override public void DrawProperty()
        {
            GUILayout.Label("Time range: start, rise, fading, end");
            GUILayout.BeginHorizontal();
            fadeIn = EditorGUILayout.FloatField(fadeIn);
            start = EditorGUILayout.FloatField(start);
            end = EditorGUILayout.FloatField(end);
            fadeOut = EditorGUILayout.FloatField(fadeOut);
            GUILayout.EndHorizontal();
        }
    }
}
#endif

