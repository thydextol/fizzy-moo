using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// A piece of flavour fruit. One distinct silhouette per real SKU - a key lime,
    /// an orange, a pineapple - so the player identifies flavour by shape as well as
    /// colour. Eaten on contact, then respawns elsewhere after a short delay.
    /// </summary>
    public class Fruit : MonoBehaviour
    {
        public Flavor Flavor;
        public bool Available { get; private set; } = true;

        Transform _visual;
        Material _mat;
        ParticleSystem _sparkle;
        float _phase, _respawnT;
        static System.Random _rng = new System.Random(1234);

        public const float FieldRadius = 13.5f;

        public static Fruit Spawn(Transform parent, Flavor f, Vector3 pos)
        {
            var go = Mk.Empty("Fruit_" + f, parent, pos);
            var b = go.AddComponent<Fruit>();
            b.Flavor = f;
            b.Construct();
            return b;
        }

        void Construct()
        {
            BuildVisual();

            var trig = gameObject.AddComponent<SphereCollider>();
            trig.isTrigger = true;
            trig.radius = 1.15f;
            trig.center = new Vector3(0f, 0.5f, 0f);

            var c = Palette.Of(Flavor);
            _sparkle = Mk.Empty("Sparkle", transform, new Vector3(0f, 0.55f, 0f)).AddComponent<ParticleSystem>();
            var m = _sparkle.main;
            m.startColor = c; m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
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

        void BuildVisual()
        {
            if (_visual != null) Destroy(_visual.gameObject);
            _visual = Mk.Empty("Visual", transform, new Vector3(0f, 0.55f, 0f)).transform;

            var c = Palette.Of(Flavor);
            _mat = Mk.Mat(c, 0.45f, 0f, c * 0.35f);
            var leafMat = Mk.Mat(new Color(0.26f, 0.46f, 0.22f), 0.3f);
            var stemMat = Mk.Mat(new Color(0.34f, 0.26f, 0.16f), 0.2f);

            switch (Flavor)
            {
                case Flavor.KeyLime:
                    // small, slightly oblong, with a nub at each end
                    Mk.Prim(PrimitiveType.Sphere, _visual, Vector3.zero, new Vector3(0.46f, 0.40f, 0.46f), _mat, "Lime", outline: true);
                    Mk.Prim(PrimitiveType.Sphere, _visual, new Vector3(0f, 0.21f, 0f), Vector3.one * 0.10f, stemMat, "Nub");
                    var lf = Mk.Prim(PrimitiveType.Sphere, _visual, new Vector3(0.16f, 0.24f, 0f), new Vector3(0.26f, 0.05f, 0.14f), leafMat, "Leaf");
                    lf.transform.localRotation = Quaternion.Euler(0f, 0f, 24f);
                    break;

                case Flavor.OrangeCream:
                    // rounder and bigger than the lime, dimpled top
                    Mk.Prim(PrimitiveType.Sphere, _visual, Vector3.zero, new Vector3(0.56f, 0.54f, 0.56f), _mat, "Orange", outline: true);
                    Mk.Prim(PrimitiveType.Cylinder, _visual, new Vector3(0f, 0.27f, 0f), new Vector3(0.07f, 0.05f, 0.07f), stemMat, "Stem");
                    foreach (float a in new[] { 0f, 120f, 240f })
                    {
                        var l = Mk.Prim(PrimitiveType.Sphere, _visual, Quaternion.Euler(0f, a, 0f) * new Vector3(0.15f, 0.28f, 0f),
                                        new Vector3(0.22f, 0.045f, 0.13f), leafMat, "Leaf");
                        l.transform.localRotation = Quaternion.Euler(0f, a, 18f);
                    }
                    break;

                default: // PinaColada - a pineapple
                    Mk.Prim(PrimitiveType.Sphere, _visual, Vector3.zero, new Vector3(0.48f, 0.62f, 0.48f), _mat, "Pineapple", outline: true);
                    // crown of fronds
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f;
                        var fr = Mk.Prim(PrimitiveType.Capsule, _visual,
                                         Quaternion.Euler(0f, a, 0f) * new Vector3(0.10f, 0.44f, 0f),
                                         new Vector3(0.07f, 0.15f, 0.07f), leafMat, "Frond");
                        fr.transform.localRotation = Quaternion.Euler(0f, a, 22f);
                    }
                    break;
            }
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
            _visual.localRotation = Quaternion.Euler(0f, _phase * 55f, Mathf.Sin(_phase * 1.7f) * 8f);
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
            var p = Mk.OnRing(4f, FieldRadius, _rng);
            transform.position = new Vector3(p.x, 0f, p.z);
            Flavor = (Flavor)_rng.Next(0, 3);
            BuildVisual();                       // silhouette must match the new flavour
            var c = Palette.Of(Flavor);
            var m = _sparkle.main; m.startColor = c;
            var em = _sparkle.emission; em.rateOverTime = 7f;
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
