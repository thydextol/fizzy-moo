using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// A thirsty villager. Holds one order (flavour + fill level), shows it as a
    /// physical reference bottle, and runs out of patience if ignored.
    /// </summary>
    public class Customer : MonoBehaviour
    {
        public Flavor Want;
        public float WantFill;          // 0..1 target fill
        public float Patience = 26f;
        public float PatienceLeft;
        public bool Active;
        const float Size = 1.22f;   // a bit over life-size so they read from across the meadow

        Transform _body, _head, _armL, _armR, _patienceRing;
        Material _shirtMat, _ringMat;
        Can _reference;
        float _phase;
        static System.Random _rng = new System.Random(99);

        public static Customer Build(Transform parent, Vector3 pos)
        {
            var go = Mk.Empty("Customer", parent, pos);
            var c = go.AddComponent<Customer>();
            c.Construct();
            return c;
        }

        void Construct()
        {
            _shirtMat = Mk.Mat(Palette.KeyLime, 0.2f);
            var skin = Mk.Mat(new Color(0.93f, 0.78f, 0.63f), 0.25f);

            _body = Mk.Empty("Body", transform, new Vector3(0f, 0f, 0f)).transform;
            Mk.Prim(PrimitiveType.Capsule, _body, new Vector3(0f, 0.52f, 0f), new Vector3(0.42f, 0.34f, 0.42f), _shirtMat, "Torso");
            _head = Mk.Prim(PrimitiveType.Sphere, _body, new Vector3(0f, 1.02f, 0f), Vector3.one * 0.36f, skin, "Head").transform;
            Mk.Prim(PrimitiveType.Sphere, _head, new Vector3( 0.20f, 0.06f, 0.40f), new Vector3(0.16f, 0.20f, 0.10f), Mk.Mat(Palette.Ink), "EyeR");
            Mk.Prim(PrimitiveType.Sphere, _head, new Vector3(-0.20f, 0.06f, 0.40f), new Vector3(0.16f, 0.20f, 0.10f), Mk.Mat(Palette.Ink), "EyeL");
            Mk.Prim(PrimitiveType.Sphere, _head, new Vector3(0f, 0.34f, -0.06f), new Vector3(0.94f, 0.52f, 0.94f), Mk.Mat(new Color(0.28f, 0.20f, 0.15f)), "Hair");
            _armL = Mk.Prim(PrimitiveType.Capsule, _body, new Vector3(-0.26f, 0.56f, 0f), new Vector3(0.12f, 0.20f, 0.12f), skin, "ArmL").transform;
            _armR = Mk.Prim(PrimitiveType.Capsule, _body, new Vector3( 0.26f, 0.56f, 0f), new Vector3(0.12f, 0.20f, 0.12f), skin, "ArmR").transform;
            Mk.Prim(PrimitiveType.Cylinder, _body, new Vector3(-0.12f, 0.14f, 0f), new Vector3(0.15f, 0.18f, 0.15f), Mk.Mat(new Color(0.25f, 0.28f, 0.40f)), "LegL");
            Mk.Prim(PrimitiveType.Cylinder, _body, new Vector3( 0.12f, 0.14f, 0f), new Vector3(0.15f, 0.18f, 0.15f), Mk.Mat(new Color(0.25f, 0.28f, 0.40f)), "LegR");

            // The order is shown as the actual product: the customer holds up a
            // real Fizzy Moo can in the flavour they want. Doubles as constant,
            // diegetic product placement without a single UI overlay.
            var holder = Mk.Empty("OrderHolder", transform, new Vector3(0f, 1.72f, 0f)).transform;
            _reference = Can.Build(holder, Vector3.zero, Flavor.KeyLime, 0.66f);
            _reference.Bob = 0.05f;

            // Patience ring - a flat disc that shrinks as time runs out.
            _ringMat = Mk.Mat(Palette.Gold, 0.4f, 0f, Palette.Gold * 0.8f);
            _patienceRing = Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.02f, 0f),
                                    new Vector3(1.5f, 0.02f, 1.5f), _ringMat, "PatienceRing").transform;
        }

        public void NewOrder(int difficulty)
        {
            Want = (Flavor)_rng.Next(0, 3);
            // Later orders sit in tighter, more awkward bands.
            float lo = 0.30f, hi = 0.92f;
            WantFill = Mathf.Lerp(lo, hi, (float)_rng.NextDouble());
            Patience = Mathf.Max(13f, 27f - difficulty * 1.6f);
            PatienceLeft = Patience;
            Active = true;

            var c = Palette.Of(Want);
            _shirtMat.color = c;
            _reference.Set(Want, 0.66f);
            _reference.Bob = 0.05f;
            transform.localScale = Vector3.zero;
            StartCoroutine(PopIn());
        }

        System.Collections.IEnumerator PopIn()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime * 2.6f)
            {
                transform.localScale = Vector3.one * (Size * Ease.OutBack(Mathf.Clamp01(t)));
                yield return null;
            }
            transform.localScale = Vector3.one * Size;
        }

        public void React(bool happy, float quality)
        {
            StopAllCoroutines();
            StartCoroutine(ReactRoutine(happy, quality));
        }

        System.Collections.IEnumerator ReactRoutine(bool happy, float quality)
        {
            Active = false;
            float dur = 0.9f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                if (happy)
                {
                    // jump for joy, arms up
                    float j = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3f)) * (1f - u) * 0.55f * (0.5f + quality);
                    _body.localPosition = new Vector3(0f, j, 0f);
                    _armL.localRotation = Quaternion.Euler(0f, 0f,  150f * Mathf.Min(1f, u * 4f));
                    _armR.localRotation = Quaternion.Euler(0f, 0f, -150f * Mathf.Min(1f, u * 4f));
                }
                else
                {
                    // slump and shake head
                    _body.localPosition = new Vector3(Mathf.Sin(u * 34f) * 0.05f * (1f - u), -0.12f * Mathf.Min(1f, u * 5f), 0f);
                }
                yield return null;
            }
            _body.localPosition = Vector3.zero;
            _armL.localRotation = Quaternion.identity;
            _armR.localRotation = Quaternion.identity;
            // shrink away
            for (float t = 0f; t < 1f; t += Time.deltaTime * 3.2f)
            {
                transform.localScale = Vector3.one * (Size * (1f - Ease.OutCubic(Mathf.Clamp01(t))));
                yield return null;
            }
            transform.localScale = Vector3.zero;
        }

        void Update()
        {
            _phase += Time.deltaTime;
            if (!Active) return;

            PatienceLeft -= Time.deltaTime;
            float p = Mathf.Clamp01(PatienceLeft / Patience);
            _patienceRing.localScale = new Vector3(1.5f * p, 0.02f, 1.5f * p);
            _ringMat.color = Color.Lerp(Palette.Danger, Palette.Gold, p);
            _ringMat.SetColor("_EmissionColor", _ringMat.color * (p < 0.3f ? Ease.Pulse(_phase, 4f) * 1.4f : 0.6f));

            // Idle: shift weight, and fidget faster as patience drains.
            float fidget = Mathf.Lerp(1.2f, 5.0f, 1f - p);
            _body.localPosition = new Vector3(Mathf.Sin(_phase * fidget) * 0.03f, Mathf.Abs(Mathf.Sin(_phase * fidget * 1.6f)) * 0.03f, 0f);
            _head.localRotation = Quaternion.Euler(0f, Mathf.Sin(_phase * fidget * 0.7f) * 14f, 0f);
        }

        public void Hide() { transform.localScale = Vector3.zero; Active = false; }
    }
}
