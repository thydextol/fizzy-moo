using UnityEngine;

namespace FizzyMoo
{
    public enum Flavor { Cola = 0, Berry = 1, Lime = 2 }

    /// <summary>Central colour language for the whole game.</summary>
    public static class Palette
    {
        public static readonly Color Cola  = new Color(0.45f, 0.22f, 0.08f);
        public static readonly Color Berry = new Color(0.82f, 0.17f, 0.50f);
        public static readonly Color Lime  = new Color(0.58f, 0.87f, 0.19f);

        public static readonly Color Grass     = new Color(0.34f, 0.60f, 0.27f);
        public static readonly Color GrassDark = new Color(0.26f, 0.48f, 0.21f);
        public static readonly Color CowWhite  = new Color(0.96f, 0.95f, 0.92f);
        public static readonly Color CowBlack  = new Color(0.14f, 0.13f, 0.15f);
        public static readonly Color Pink      = new Color(0.98f, 0.70f, 0.75f);
        public static readonly Color Wood      = new Color(0.52f, 0.34f, 0.19f);
        public static readonly Color Metal     = new Color(0.75f, 0.77f, 0.80f);
        public static readonly Color Danger    = new Color(1.00f, 0.28f, 0.21f);
        public static readonly Color Gold      = new Color(1.00f, 0.82f, 0.25f);
        public static readonly Color Ink       = new Color(0.10f, 0.09f, 0.13f);
        public static readonly Color Cream     = new Color(1.00f, 0.98f, 0.93f);

        public static Color Of(Flavor f) => f == Flavor.Cola ? Cola : f == Flavor.Berry ? Berry : Lime;
        public static string Name(Flavor f) => f == Flavor.Cola ? "COLA" : f == Flavor.Berry ? "BERRY" : "LIME";

        /// <summary>Blend the three flavour channels into the colour of the milk in the udder.</summary>
        public static Color Mix(Vector3 m)
        {
            float s = m.x + m.y + m.z;
            if (s < 0.0001f) return Cream;
            return (Cola * m.x + Berry * m.y + Lime * m.z) / s;
        }
    }

    /// <summary>Tiny factory helpers so the whole world can be authored in C#.</summary>
    public static class Mk
    {
        static Shader _std;
        static Shader Std => _std != null ? _std : (_std = Shader.Find("Standard"));

        public static Material Mat(Color c, float smooth = 0.25f, float metal = 0f, Color? emis = null)
        {
            var m = new Material(Std);
            m.color = c;
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emis.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emis.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        /// <summary>Transparent variant (for glass bottles, fizz domes).</summary>
        public static Material Glass(Color c)
        {
            var m = new Material(Std);
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            m.color = c;
            m.SetFloat("_Glossiness", 0.85f);
            return m;
        }

        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale,
                                      Material mat, string name = null, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(t);
            if (name != null) go.name = name;
            var col = go.GetComponent<Collider>();
            if (!collider && col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Empty(string name, Transform parent = null, Vector3 pos = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go;
        }

        /// <summary>Deterministic-ish jitter used for scattering props.</summary>
        public static Vector3 OnRing(float minR, float maxR, System.Random rng)
        {
            double a = rng.NextDouble() * System.Math.PI * 2.0;
            float r = Mathf.Lerp(minR, maxR, (float)rng.NextDouble());
            return new Vector3(Mathf.Cos((float)a) * r, 0f, Mathf.Sin((float)a) * r);
        }
    }

    public static class Ease
    {
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InCubic(float t) => t * t * t;
        public static float Pulse(float t, float freq) => Mathf.Sin(t * freq * Mathf.PI * 2f) * 0.5f + 0.5f;
    }
}
