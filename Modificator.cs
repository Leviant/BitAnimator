
// Copyright © 2019 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.0 (14.07.2019)

#if UNITY_EDITOR
using System;
using UnityEngine;

namespace AudioVisualization.Modificators
{
    [Serializable]
    public class Modificator : ScriptableObject
    {
        public bool useTempBuffer;
        enum ExecutionParam
        {
            ApplyFirst,
        }
        virtual public void Apply(ComputeProgram core, ComputeBuffer input, ComputeBuffer output) { }
        virtual public void DrawProperty() { }
    }
}
#endif

