using UnityEngine;

namespace FizzyMoo
{
    /// <summary>The three real Fizzy Moo SKUs.</summary>
    public enum Flavor { KeyLime = 0, OrangeCream = 1, PinaColada = 2 }

    public static class Palette
    {
        static Color Hex(uint v) => new Color(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f);

        // --- true brand colours, sampled from the production can artwork ------
        public static readonly Color BrandKeyLime     = Hex(0xD9DC7C);
        public static readonly Color BrandOrangeCream = Hex(0xF8981A);
        public static readonly Color BrandPinaColada  = Hex(0xFCEFA1);
        public static readonly Color Peach            = Hex(0xFEC375);   // mascot muzzle
        public static readonly Color Ink              = Hex(0x191B1B);   // keyline black

        // --- gameplay-legible variants ----------------------------------------
        // On the real cans Key Lime and Pina Colada sit only a few degrees apart
        // in hue - perfect on a shelf, unreadable across a meadow. These push the
        // two apart for at-a-glance identification while staying in the family.
        public static readonly Color KeyLime     = Hex(0xA9C63C);
        public static readonly Color OrangeCream = Hex(0xF8981A);
        public static readonly Color PinaColada  = Hex(0xFFDE72);

        public static readonly Color Grass     = new Color(0.34f, 0.60f, 0.27f);
        public static readonly Color GrassDark = new Color(0.26f, 0.48f, 0.21f);
        public static readonly Color CowWhite  = new Color(0.97f, 0.96f, 0.94f);
        public static readonly Color CowBlack  = Hex(0x191B1B);
        public static readonly Color Wood      = new Color(0.52f, 0.34f, 0.19f);
        public static readonly Color Metal     = new Color(0.78f, 0.80f, 0.83f);
        public static readonly Color Danger    = Hex(0xE8402C);
        public static readonly Color Gold      = Hex(0xFFC93F);
        public static readonly Color Cream     = new Color(1.00f, 0.99f, 0.95f);

        public static Color Of(Flavor f) =>
            f == Flavor.KeyLime ? KeyLime : f == Flavor.OrangeCream ? OrangeCream : PinaColada;

        public static Color BrandOf(Flavor f) =>
            f == Flavor.KeyLime ? BrandKeyLime : f == Flavor.OrangeCream ? BrandOrangeCream : BrandPinaColada;

        public static string Name(Flavor f) =>
            f == Flavor.KeyLime ? "KEY LIME" : f == Flavor.OrangeCream ? "ORANGE CREAM" : "PINA COLADA";

        public static string Short(Flavor f) =>
            f == Flavor.KeyLime ? "LIME" : f == Flavor.OrangeCream ? "ORANGE" : "PINA";

        // The three thresholds the score actually uses, named once so nothing drifts.
        public const float PurityFail  = 0.34f;   // Judge()'s flavour gate
        public const float PurityPour  = 0.45f;   // below this even a perfect release scores under 0.5 - the tap stays shut
        public const float PurityClean = 0.80f;   // Judge()'s "perfect" gate
        public static readonly Color Mud = Hex(0x6E6157);

        /// <summary>Dominant flavour of a mix and how pure it is. The one implementation.</summary>
        public static void Dom(Vector3 m, out Flavor f, out float purity)
        {
            float sum = m.x + m.y + m.z;
            if (sum < 0.0001f) { f = Flavor.KeyLime; purity = 0f; return; }
            if (m.x >= m.y && m.x >= m.z) { f = Flavor.KeyLime;     purity = m.x / sum; }
            else if (m.y >= m.z)          { f = Flavor.OrangeCream; purity = m.y / sum; }
            else                          { f = Flavor.PinaColada;  purity = m.z / sum; }
        }

        /// <summary>
        /// What is ACTUALLY in the keg: the dominant hue, dragged toward mud as purity falls.
        /// The ramp mirrors Judge()'s flavour score, so the colour is honest about the score -
        /// a contaminated tank reads dirty instead of averaging toward gold, the game's own
        /// colour for "correct".
        /// </summary>
        public static Color Tank(Vector3 m)
        {
            float s = m.x + m.y + m.z;
            if (s < 0.0001f) return Cream;
            Dom(m, out var f, out float p);
            float t = 0.15f + 0.85f * Mathf.InverseLerp(PurityFail, 1f, p);
            return Color.Lerp(Mud, Of(f), t);
        }
    }

    public static class Mk
    {
        static Shader _std, _outline, _sprite;
        static Shader Std     => _std     != null ? _std     : (_std     = Shader.Find("Standard"));
        static Shader Outline => _outline != null ? _outline : (_outline = Shader.Find("FizzyMoo/Outline"));
        static Shader SpriteS => _sprite  != null ? _sprite  : (_sprite  = Shader.Find("Sprites/Default"));

        static Material _outlineMat;
        /// <summary>Shared black keyline material, applied as a second material slot.</summary>
        public static Material OutlineMat
        {
            get
            {
                if (_outlineMat == null && Outline != null)
                {
                    _outlineMat = new Material(Outline);
                    _outlineMat.SetColor("_OutlineColor", Palette.Ink);
                    _outlineMat.SetFloat("_OutlineWidth", 0.030f);
                }
                return _outlineMat;
            }
        }

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
            m.SetFloat("_Glossiness", 0.88f);
            return m;
        }

        public static Material Unlit(Texture tex, Color tint)
        {
            var m = new Material(SpriteS);
            m.mainTexture = tex;
            m.color = tint;
            return m;
        }

        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale,
                                      Material mat, string name = null, bool collider = false, bool outline = false)
        {
            var go = GameObject.CreatePrimitive(t);
            if (name != null) go.name = name;
            var col = go.GetComponent<Collider>();
            if (!collider && col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null)
            {
                // A second material slot on a single-submesh mesh draws it twice -
                // that is how the keyline gets rendered without extra GameObjects.
                if (outline && OutlineMat != null) r.sharedMaterials = new[] { mat, OutlineMat };
                else r.sharedMaterial = mat;
            }
            return go;
        }

        public static GameObject Empty(string name, Transform parent = null, Vector3 pos = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go;
        }

        static Texture2D _dot;
        /// <summary>Soft round sprite for particles - billboards render as hard squares without it.</summary>
        public static Texture2D Dot
        {
            get
            {
                if (_dot != null) return _dot;
                const int N = 64;
                _dot = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[N * N];
                float c = (N - 1) * 0.5f;
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
                _dot.SetPixels32(px); _dot.Apply();
                return _dot;
            }
        }

        public static Material ParticleMat(Color tint)
        {
            var m = new Material(SpriteS);
            m.mainTexture = Dot;
            m.color = tint;
            return m;
        }

        public static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.95f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        public static Vector3 OnRing(float minR, float maxR, System.Random rng)
        {
            double a = rng.NextDouble() * System.Math.PI * 2.0;
            float r = Mathf.Lerp(minR, maxR, (float)rng.NextDouble());
            return new Vector3(Mathf.Cos((float)a) * r, 0f, Mathf.Sin((float)a) * r);
        }
    }

    /// <summary>
    /// Loads the real Fizzy Moo brand assets shipped in Resources: the production
    /// can models exported from the company's Blender source, the label artwork,
    /// the wordmark, the mascot, and the brand typeface.
    /// </summary>
    public static class Brand
    {
        public const string Tagline = "TRY SOMETHING MOO.";

        static GameObject[] _cans = new GameObject[3];
        static Texture2D _wordmark, _sillyCow;
        static Font _font;

        public static GameObject CanPrefab(Flavor f)
        {
            int i = (int)f;
            if (_cans[i] == null)
            {
                string n = f == Flavor.KeyLime ? "Can_Key_Lime"
                         : f == Flavor.OrangeCream ? "Can_Orange_Cream" : "Can_Pina_Colada";
                _cans[i] = Resources.Load<GameObject>("Cans/" + n);
                if (_cans[i] == null) Debug.LogWarning("[Brand] missing can model: " + n);
            }
            return _cans[i];
        }

        public static Texture2D Wordmark => _wordmark != null ? _wordmark : (_wordmark = Resources.Load<Texture2D>("Brand/Wordmark"));
        public static Texture2D SillyCow => _sillyCow != null ? _sillyCow : (_sillyCow = Resources.Load<Texture2D>("Brand/SillyCow"));

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.Load<Font>("Brand/FizzyMoo");
                if (_font == null)
                    _font = UnityEngine.Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Arial" }, 48);
                return _font;
            }
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
