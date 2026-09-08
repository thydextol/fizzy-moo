using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// Builds Bessie out of Unity primitives at runtime and animates her.
    /// No imported art: body/head/legs/horns/udder are all capsules, spheres and
    /// cylinders, which gives a readable low-poly look and keeps the repo asset-free.
    /// The udder is the key readability trick - it swells and takes on the colour of
    /// the flavour mix, so the player reads pressure without looking at the HUD.
    /// </summary>
    public class CowRig : MonoBehaviour
    {
        public Transform Body, Head, Udder, Tail, Snout;
        Transform[] _legs = new Transform[4];
        Vector3[] _legHome = new Vector3[4];
        Renderer _udderR;
        Material _udderMat;
        Vector3 _bodyHome, _headHome, _udderHome;
        float _walkPhase, _jiggle, _jiggleVel;
        public float PressureNorm;          // 0..1, driven by CowController
        public Vector3 FlavorMix = Vector3.zero;

        public static CowRig Build(Transform parent)
        {
            var root = Mk.Empty("CowRig", parent);
            var rig = root.AddComponent<CowRig>();
            rig.Construct();
            return rig;
        }

        void Construct()
        {
            var white = Mk.Mat(Palette.CowWhite, 0.15f);
            var black = Mk.Mat(Palette.CowBlack, 0.20f);
            var pink  = Mk.Mat(Palette.Pink, 0.35f);
            var horn  = Mk.Mat(new Color(0.90f, 0.86f, 0.74f), 0.30f);
            _udderMat = Mk.Mat(Palette.Cream, 0.55f);

            // --- torso: a capsule laid on its side ------------------------
            Body = Mk.Empty("Body", transform, new Vector3(0f, 0.86f, 0f)).transform;
            var torso = Mk.Prim(PrimitiveType.Capsule, Body, Vector3.zero, new Vector3(0.78f, 0.72f, 0.78f), white, "Torso");
            torso.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Holstein spots - flattened spheres pressed onto the torso.
            var spots = new[] {
                (new Vector3( 0.30f,  0.16f,  0.22f), 0.34f),
                (new Vector3(-0.28f,  0.10f, -0.20f), 0.40f),
                (new Vector3( 0.10f, -0.18f, -0.45f), 0.28f),
                (new Vector3(-0.20f,  0.22f,  0.46f), 0.24f),
            };
            foreach (var (p, s) in spots)
            {
                var sp = Mk.Prim(PrimitiveType.Sphere, Body, p, new Vector3(s, s * 0.55f, s), black, "Spot");
                sp.transform.localRotation = Quaternion.LookRotation(p.normalized);
            }

            // --- head ------------------------------------------------------
            Head = Mk.Empty("Head", Body, new Vector3(0f, 0.30f, 0.72f)).transform;
            Mk.Prim(PrimitiveType.Cube, Head, Vector3.zero, new Vector3(0.42f, 0.38f, 0.46f), white, "Skull");
            Snout = Mk.Prim(PrimitiveType.Cube, Head, new Vector3(0f, -0.09f, 0.26f), new Vector3(0.30f, 0.22f, 0.24f), pink, "Snout").transform;
            // nostrils
            Mk.Prim(PrimitiveType.Sphere, Snout, new Vector3( 0.25f, 0.10f, 0.46f), new Vector3(0.22f, 0.22f, 0.12f), black);
            Mk.Prim(PrimitiveType.Sphere, Snout, new Vector3(-0.25f, 0.10f, 0.46f), new Vector3(0.22f, 0.22f, 0.12f), black);
            // eyes
            Mk.Prim(PrimitiveType.Sphere, Head, new Vector3( 0.15f, 0.10f, 0.22f), new Vector3(0.10f, 0.12f, 0.10f), black, "EyeR");
            Mk.Prim(PrimitiveType.Sphere, Head, new Vector3(-0.15f, 0.10f, 0.22f), new Vector3(0.10f, 0.12f, 0.10f), black, "EyeL");
            // ears
            var earL = Mk.Prim(PrimitiveType.Sphere, Head, new Vector3(-0.26f, 0.10f, -0.02f), new Vector3(0.20f, 0.10f, 0.12f), white, "EarL");
            var earR = Mk.Prim(PrimitiveType.Sphere, Head, new Vector3( 0.26f, 0.10f, -0.02f), new Vector3(0.20f, 0.10f, 0.12f), white, "EarR");
            earL.transform.localRotation = Quaternion.Euler(0f, 0f,  28f);
            earR.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
            // horns
            foreach (float sx in new[] { -1f, 1f })
            {
                var h = Mk.Prim(PrimitiveType.Capsule, Head, new Vector3(0.14f * sx, 0.22f, -0.04f),
                                new Vector3(0.07f, 0.10f, 0.07f), horn, "Horn");
                h.transform.localRotation = Quaternion.Euler(0f, 0f, 32f * sx);
            }

            // --- legs ------------------------------------------------------
            var legPos = new[] {
                new Vector3( 0.26f, -0.44f,  0.40f),
                new Vector3(-0.26f, -0.44f,  0.40f),
                new Vector3( 0.26f, -0.44f, -0.40f),
                new Vector3(-0.26f, -0.44f, -0.40f),
            };
            for (int i = 0; i < 4; i++)
            {
                var leg = Mk.Empty("Leg" + i, Body, legPos[i]).transform;
                Mk.Prim(PrimitiveType.Cylinder, leg, new Vector3(0f, -0.20f, 0f), new Vector3(0.14f, 0.24f, 0.14f), white, "Shin");
                Mk.Prim(PrimitiveType.Cylinder, leg, new Vector3(0f, -0.42f, 0f), new Vector3(0.16f, 0.05f, 0.16f), black, "Hoof");
                _legs[i] = leg;
                _legHome[i] = legPos[i];
            }

            // --- udder: the pressure gauge you can see -----------------------
            Udder = Mk.Empty("Udder", Body, new Vector3(0f, -0.42f, -0.10f)).transform;
            var bag = Mk.Prim(PrimitiveType.Sphere, Udder, Vector3.zero, new Vector3(0.40f, 0.34f, 0.44f), _udderMat, "Bag");
            _udderR = bag.GetComponent<Renderer>();
            foreach (var t in new[] { new Vector3(0.13f, -0.55f, 0.16f), new Vector3(-0.13f, -0.55f, 0.16f),
                                      new Vector3(0.13f, -0.55f, -0.16f), new Vector3(-0.13f, -0.55f, -0.16f) })
                Mk.Prim(PrimitiveType.Capsule, Udder, t, new Vector3(0.16f, 0.22f, 0.16f), pink, "Teat");

            // --- tail --------------------------------------------------------
            Tail = Mk.Empty("Tail", Body, new Vector3(0f, 0.22f, -0.74f)).transform;
            Mk.Prim(PrimitiveType.Cylinder, Tail, new Vector3(0f, -0.22f, 0f), new Vector3(0.05f, 0.24f, 0.05f), white, "TailBone");
            Mk.Prim(PrimitiveType.Sphere, Tail, new Vector3(0f, -0.48f, 0f), new Vector3(0.13f, 0.16f, 0.13f), black, "Tuft");

            _bodyHome = Body.localPosition;
            _headHome = Head.localPosition;
            _udderHome = Udder.localScale;
        }

        /// <summary>Kick the jelly-physics wobble (used on eat, blowout, hard landings).</summary>
        public void Wobble(float force) => _jiggleVel += force;

        /// <param name="speed01">normalised ground speed, drives the gait</param>
        public void Tick(float speed01, float dt)
        {
            // Spring-damper on a scalar drives a squash/stretch wobble on the whole body.
            _jiggleVel += -_jiggle * 90f * dt;
            _jiggleVel *= Mathf.Exp(-6f * dt);
            _jiggle += _jiggleVel * dt;

            float p = PressureNorm;

            // Gait: legs swing in diagonal pairs, body bobs at twice the step rate.
            _walkPhase += dt * Mathf.Lerp(0f, 11f, speed01);
            for (int i = 0; i < 4; i++)
            {
                float ph = _walkPhase + ((i == 0 || i == 3) ? 0f : Mathf.PI);
                float swing = Mathf.Sin(ph) * 0.22f * speed01;
                float lift = Mathf.Max(0f, Mathf.Cos(ph)) * 0.10f * speed01;
                _legs[i].localPosition = _legHome[i] + new Vector3(0f, lift, swing);
                _legs[i].localRotation = Quaternion.Euler(swing * -55f, 0f, 0f);
            }

            // Body: bob from the gait, squash from the wobble spring, and a
            // pressure tremor that gets violent as she approaches a blowout.
            float bob = Mathf.Sin(_walkPhase * 2f) * 0.045f * speed01;
            float tremor = p > 0.55f ? (p - 0.55f) / 0.45f : 0f;
            float shake = Mathf.Sin(Time.time * Mathf.Lerp(18f, 46f, tremor)) * 0.035f * tremor;

            Body.localPosition = _bodyHome + new Vector3(shake * 0.6f, bob + _jiggle * 0.10f, 0f);
            float swell = 1f + p * 0.16f;                    // whole cow inflates a little
            Body.localScale = new Vector3(swell + _jiggle * 0.09f,
                                          swell - _jiggle * 0.12f,
                                          swell + _jiggle * 0.09f);

            // Head bobs while walking and lifts when she is about to pop.
            Head.localPosition = _headHome + new Vector3(0f, Mathf.Sin(_walkPhase * 2f + 0.6f) * 0.03f * speed01 + p * 0.05f, 0f);
            Head.localRotation = Quaternion.Euler(Mathf.Lerp(6f, -16f, p), 0f, shake * 20f);

            // Udder: the readable pressure gauge. Grows up to ~2.1x and glows.
            float u = 1f + p * 1.10f + _jiggle * 0.22f;
            Udder.localScale = Vector3.Scale(_udderHome, new Vector3(u, u * (1f + p * 0.25f), u));
            var c = Palette.Mix(FlavorMix);
            _udderMat.color = Color.Lerp(Palette.Cream, c, Mathf.Clamp01(FlavorMix.magnitude * 1.6f));
            _udderMat.SetColor("_EmissionColor", c * Mathf.Lerp(0f, 1.4f, Mathf.Max(0f, p - 0.5f) * 2f));
            if (p > 0.5f) _udderMat.EnableKeyword("_EMISSION");

            // Tail swishes constantly, faster under pressure.
            Tail.localRotation = Quaternion.Euler(28f, Mathf.Sin(Time.time * Mathf.Lerp(2.2f, 9f, p)) * 26f, 0f);
        }

        public void SetVisible(bool v)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = v;
        }
    }
}
