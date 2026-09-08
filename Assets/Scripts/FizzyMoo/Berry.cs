using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// A fizz-berry. Bobs, spins, and glows in its flavour colour. Eaten on contact,
    /// then respawns somewhere else in the meadow after a short delay so the field
    /// never runs dry but never feels static either.
    /// </summary>
    public class Berry : MonoBehaviour
    {
        public Flavor Flavor;
        public bool Available { get; private set; } = true;

        Transform _visual;
        Material _mat;
        ParticleSystem _sparkle;
        float _phase, _respawnT;
        static System.Random _rng = new System.Random(1234);

        public const float FieldRadius = 13.5f;

        public static Berry Spawn(Transform parent, Flavor f, Vector3 pos)
        {
            var go = Mk.Empty("Berry_" + f, parent, pos);
            var b = go.AddComponent<Berry>();
            b.Flavor = f;
            b.Construct();
            return b;
        }

        void Construct()
        {
            var c = Palette.Of(Flavor);
            _mat = Mk.Mat(c, 0.75f, 0.1f, c * 1.6f);

            _visual = Mk.Empty("Visual", transform, new Vector3(0f, 0.55f, 0f)).transform;
            // A cluster of three spheres reads better than one ball at distance.
            Mk.Prim(PrimitiveType.Sphere, _visual, new Vector3( 0.00f,  0.10f, 0f), Vector3.one * 0.48f, _mat);
            Mk.Prim(PrimitiveType.Sphere, _visual, new Vector3( 0.22f, -0.10f, 0.07f), Vector3.one * 0.36f, _mat);
            Mk.Prim(PrimitiveType.Sphere, _visual, new Vector3(-0.21f, -0.11f, -0.06f), Vector3.one * 0.34f, _mat);
            // little stem
            Mk.Prim(PrimitiveType.Cylinder, _visual, new Vector3(0f, 0.28f, 0f), new Vector3(0.035f, 0.10f, 0.035f),
                    Mk.Mat(new Color(0.30f, 0.45f, 0.20f)));

            var trig = gameObject.AddComponent<SphereCollider>();
            trig.isTrigger = true;
            trig.radius = 1.15f;
            trig.center = new Vector3(0f, 0.5f, 0f);

            _sparkle = Mk.Empty("Sparkle", transform, new Vector3(0f, 0.55f, 0f)).AddComponent<ParticleSystem>();
            var m = _sparkle.main;
            m.startColor = c; m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            m.gravityModifier = -0.25f; m.maxParticles = 60;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = _sparkle.emission; em.rateOverTime = 7f;
            var sh = _sparkle.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.22f;
            _sparkle.GetComponent<ParticleSystemRenderer>().material = Mk.ParticleMat(Color.white);
            Mk.FadeOut(_sparkle);

            _phase = (float)_rng.NextDouble() * 6.28f;
        }

        void Update()
        {
            if (!Available)
            {
                _respawnT -= Time.deltaTime;
                if (_respawnT <= 0f) Respawn();
                return;
            }
            _phase += Time.deltaTime;
            _visual.localPosition = new Vector3(0f, 0.55f + Mathf.Sin(_phase * 2.1f) * 0.11f, 0f);
            _visual.localRotation = Quaternion.Euler(0f, _phase * 62f, Mathf.Sin(_phase * 1.7f) * 9f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!Available) return;
            var cow = other.GetComponentInParent<CowController>();
            if (cow == null || !cow.CanAct) return;
            cow.Eat(Flavor);
            Consume();
        }

        void Consume()
        {
            Available = false;
            _respawnT = Random.Range(2.2f, 4.0f);
            _visual.gameObject.SetActive(false);
            var em = _sparkle.emission; em.rateOverTime = 0f;
            _sparkle.Emit(22);
        }

        void Respawn()
        {
            // Re-roll position and flavour so the field composition keeps shifting.
            var p = Mk.OnRing(4f, FieldRadius, _rng);
            transform.position = new Vector3(p.x, 0f, p.z);
            Flavor = (Flavor)_rng.Next(0, 3);
            var c = Palette.Of(Flavor);
            _mat.color = c;
            _mat.SetColor("_EmissionColor", c * 1.6f);
            var m = _sparkle.main; m.startColor = c;
            var em = _sparkle.emission; em.rateOverTime = 7f;
            _visual.gameObject.SetActive(true);
            _visual.localScale = Vector3.zero;
            Available = true;
            StartCoroutine(PopIn());
        }

        System.Collections.IEnumerator PopIn()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime * 3.4f)
            {
                _visual.localScale = Vector3.one * Ease.OutBack(Mathf.Clamp01(t));
                yield return null;
            }
            _visual.localScale = Vector3.one;
        }
    }
}
