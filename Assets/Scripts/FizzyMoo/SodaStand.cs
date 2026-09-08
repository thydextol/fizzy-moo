using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// The Fizzy Moo stand. Owns the live bottle, the current customer, and the
    /// judging rule that turns a pour into points.
    ///
    /// Scoring blends two independent axes so the player has to plan the whole
    /// trip, not just the last second:
    ///   FILL    - did you release at the right moment? (motor skill)
    ///   FLAVOUR - did you eat the right fruit, and only those? (routing/planning)
    /// </summary>
    public class SodaStand : MonoBehaviour
    {
        public const float BottleCapacity = 52f;   // pressure units for a full bottle
        public const float ServeRadius    = 3.6f;

        public Customer Customer { get; private set; }
        public Bottle Live { get; private set; }
        public bool CowInZone { get; private set; }
        public bool Pouring { get; private set; }
        public float Charge { get; private set; }   // pressure units currently in the bottle

        Material _zoneMat, _tapMat;
        Transform _zoneDisc, _tapHandle;
        ParticleSystem _spill, _celebrate;
        CowController _cow;
        int _difficulty;
        float _phase, _lockout;

        /// <summary>(points, quality 0..1, perfect) - GameManager listens for this.</summary>
        public System.Action<int, float, bool> OnServed;
        public System.Action OnTimeout;

        public static SodaStand Build(Transform parent, Vector3 pos)
        {
            var go = Mk.Empty("SodaStand", parent, pos);
            var s = go.AddComponent<SodaStand>();
            s.Construct();
            return s;
        }

        void Construct()
        {
            var wood  = Mk.Mat(Palette.Wood, 0.2f);
            var wood2 = Mk.Mat(Palette.Wood * 0.78f, 0.2f);
            var metal = Mk.Mat(Palette.Metal, 0.85f, 0.9f);

            // counter
            Mk.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 0.55f, 0f), new Vector3(3.4f, 1.1f, 1.3f), wood, "Counter");
            Mk.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 1.16f, 0f), new Vector3(3.7f, 0.12f, 1.55f), wood2, "CounterTop");
            // posts + awning
            foreach (float sx in new[] { -1f, 1f })
                Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(1.6f * sx, 1.5f, -0.5f), new Vector3(0.12f, 1.5f, 0.12f), wood2, "Post");
            var awn = Mk.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 3.02f, 0.1f), new Vector3(4.0f, 0.10f, 2.0f),
                              Mk.Mat(Palette.Peach, 0.25f), "Awning");
            awn.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            // Cream valance along the leading edge instead of the stripe row, which
            // rendered as a set of white teeth hanging off the awning.
            Mk.Prim(PrimitiveType.Cube, awn.transform, new Vector3(0f, 0f, 0.47f), new Vector3(1.01f, 1.6f, 0.10f),
                    Mk.Mat(Palette.Cream, 0.25f), "Valance");

            // Sign. The stand is rotated 180 degrees, so the face the player sees is
            // stand-local +z; the wordmark quad is parented to the stand root (not to
            // the sign cube) so the board's non-uniform scale does not squash it.
            Mk.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 3.55f, -0.34f), new Vector3(3.4f, 0.95f, 0.12f),
                    Mk.Mat(Palette.Cream, 0.3f), "Sign");
            var mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(mark.GetComponent<Collider>());
            mark.name = "Wordmark";
            mark.transform.SetParent(transform, false);
            mark.transform.localPosition = new Vector3(0f, 3.55f, -0.27f);
            // Sprites/Default is Cull Off, so the quad renders from both sides and
            // the player-facing side (stand-local -z) showed the artwork mirrored.
            mark.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            mark.transform.localScale = new Vector3(2.75f, 0.78f, 1f);
            mark.GetComponent<Renderer>().sharedMaterial = Mk.Unlit(Brand.Wordmark, Color.white);

            // the product line, on display where customers queue
            for (int i = 0; i < 3; i++)
            {
                var can = Can.Build(transform, new Vector3(-0.95f + i * 0.95f, 1.22f, -0.34f), (Flavor)i, 0.42f);
                can.Spin = false;
                can.transform.localRotation = Quaternion.Euler(0f, 180f + (i - 1) * 14f, 0f);
            }

            // tap + live bottle
            Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 1.60f, -0.30f), new Vector3(0.10f, 0.45f, 0.10f), metal, "TapPost");
            _tapHandle = Mk.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 2.00f, -0.18f), new Vector3(0.10f, 0.10f, 0.36f), metal, "TapHandle").transform;
            _tapMat = Mk.Mat(Palette.Cream, 0.6f);
            Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 1.52f, 0.02f), new Vector3(0.07f, 0.14f, 0.07f), metal, "Spout");
            Live = Bottle.Build(transform, new Vector3(0f, 1.24f, 0.02f), 1.0f);

            // Pour zone: a glowing ring laid on the grass. A filled disc read as a
            // white blob and fought with the product for attention.
            var ringTex = UIKit.Radial(256, 0.82f, 1f, true).texture;
            _zoneMat = Mk.Unlit(ringTex, Palette.Gold);
            var zoneQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(zoneQuad.GetComponent<Collider>());
            zoneQuad.name = "PourZone";
            zoneQuad.transform.SetParent(transform, false);
            zoneQuad.transform.localPosition = new Vector3(0f, 0.02f, 1.9f);
            zoneQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            zoneQuad.transform.localScale = new Vector3(ServeRadius * 2.5f, ServeRadius * 2.5f, 1f);
            zoneQuad.GetComponent<Renderer>().sharedMaterial = _zoneMat;
            _zoneDisc = zoneQuad.transform;

            Customer = Customer.Build(transform, new Vector3(-2.55f, 0f, 0.15f));
            Customer.transform.localRotation = Quaternion.Euler(0f, 78f, 0f);
            Customer.Hide();

            _spill = MakePs("Spill", new Vector3(0f, 1.2f, 0.02f), 1.2f);
            _celebrate = MakePs("Celebrate", new Vector3(0f, 2.1f, 0.3f), 0.5f);
        }

        ParticleSystem MakePs(string n, Vector3 p, float size)
        {
            var ps = Mk.Empty(n, transform, p).AddComponent<ParticleSystem>();
            var m = ps.main;
            m.playOnAwake = false; m.maxParticles = 400;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f * size, 0.22f * size);
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            m.gravityModifier = 1.0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var em = ps.emission; em.rateOverTime = 0f;
            ps.GetComponent<ParticleSystemRenderer>().material = Mk.ParticleMat(Color.white);
            Mk.FadeOut(ps);
            ps.Stop();
            return ps;
        }

        public void Bind(CowController cow) => _cow = cow;

        public void NextCustomer(int difficulty)
        {
            _difficulty = difficulty;
            Charge = 0f;
            Live.SetFill(0f);
            Live.ClearBands();
            Customer.NewOrder(difficulty);
            Live.AddTargetBand(Customer.WantFill, Color.white);
            Live.SetColor(Palette.Of(Customer.Want));
            _lockout = 0f;
        }

        void Update()
        {
            _phase += Time.deltaTime;
            if (_lockout > 0f) _lockout -= Time.deltaTime;
            if (_cow == null) return;

            var flat = _cow.transform.position - transform.position;
            flat.y = 0f;
            CowInZone = flat.magnitude <= ServeRadius + 1.2f;

            // Zone marker glows when you are standing in it and ready to pour.
            var zc = CowInZone ? Palette.Gold : Palette.Cream;
            float za = CowInZone ? 0.55f + Ease.Pulse(_phase, 1.6f) * 0.40f : 0.30f;
            _zoneMat.color = new Color(zc.r, zc.g, zc.b, za);

            bool active = Customer != null && Customer.Active && _lockout <= 0f;

            if (active && CowInZone && _cow.LastVentAmount > 0f && _cow.CanAct)
            {
                Pouring = true;
                Charge += _cow.LastVentAmount;
                float fill = Charge / BottleCapacity;
                Live.SetColor(Palette.Mix(_cow.FlavorMix));
                Live.SetFill(fill);
                _tapHandle.localRotation = Quaternion.Euler(-38f, 0f, 0f);

                if (fill >= 1.0f) Judge(overflow: true);   // spilled everywhere
            }
            else
            {
                _tapHandle.localRotation = Quaternion.identity;
                if (Pouring)
                {
                    Pouring = false;
                    if (active && Charge > 0.5f) Judge(overflow: false);
                }
            }

            if (active && Customer.PatienceLeft <= 0f)
            {
                Customer.React(false, 0f);
                Sfx.I?.Play(ProcAudio.Buzz, 0.6f);
                _lockout = 1.1f;
                OnTimeout?.Invoke();
            }
        }

        void Judge(bool overflow)
        {
            Pouring = false;
            float fill = Mathf.Clamp01(Charge / BottleCapacity);
            float want = Customer.WantFill;

            _cow.DominantFlavor(out var dom, out var purity);
            bool flavorRight = dom == Customer.Want && purity > 0.34f;

            // Fill accuracy: full credit inside 3%, tapering to zero at 35% off.
            float fillErr = Mathf.Abs(fill - want);
            float fillScore = overflow ? 0f : Mathf.Clamp01(1f - Mathf.Max(0f, fillErr - 0.03f) / 0.32f);

            // Flavour: a clean single-flavour pour scores far better than a muddled one.
            float flavorScore = flavorRight ? Mathf.InverseLerp(0.34f, 1f, purity) * 0.85f + 0.15f : 0.10f;

            float quality = Mathf.Clamp01(fillScore * 0.62f + flavorScore * 0.38f);
            bool perfect = !overflow && fillErr < 0.045f && flavorRight && purity > 0.80f;
            int points = Mathf.RoundToInt(quality * 100f) + (perfect ? 60 : 0);

            if (overflow)
            {
                _spill.Emit(120);
                Sfx.I?.Play(ProcAudio.Buzz, 0.7f);
            }
            else if (perfect)
            {
                var cm = _celebrate.main; cm.startColor = Palette.Gold;
                _celebrate.Emit(90);
                Sfx.I?.Play(ProcAudio.Chime, 0.8f);
                Sfx.I?.Play(ProcAudio.Pop, 0.5f);
            }
            else if (quality > 0.5f)
            {
                Sfx.I?.Play(ProcAudio.Pop, 0.6f);
            }
            else
            {
                Sfx.I?.Play(ProcAudio.Buzz, 0.45f);
            }

            Customer.React(quality > 0.5f, quality);
            _cow.ClearFlavor();
            _lockout = 1.0f;
            OnServed?.Invoke(points, quality, perfect);
        }

        public void ResetStand()
        {
            Charge = 0f; Pouring = false; _lockout = 0f;
            Live.SetFill(0f); Live.ClearBands();
            Customer.Hide();
        }
    }
}
