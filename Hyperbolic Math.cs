using UnityEngine;

namespace UnityEngine {
	public static class HyperbolicMath{
		public static float Sinh(float x) {
			return (Mathf.Exp(x) - Mathf.Exp(-x))*0.5f;
		}
		public static float Cosh(float x) {
			return (Mathf.Exp(x) + Mathf.Exp(-x))*0.5f;
		}
	}
}