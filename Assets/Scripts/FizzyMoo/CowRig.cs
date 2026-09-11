using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// Bessie, assembled from Unity primitives at runtime - no imported art.
    ///
    /// The trick to making primitives not look like primitives is overlapping soft
    /// masses rather than one shape per body part: the torso is three intersecting
    /// spheres (chest, barrel, rump), the legs taper thigh -> shin -> hoof, and the
    /// face is built from a squashed skull sphere with a separate muzzle. Everything
    /// is slightly oversized and rounded, which reads as "stylised" instead of "crude".
    ///
    /// The udder is the key readability device: it swells to ~2x and takes on the
    /// colour of the flavour mix, so the player can read pressure and flavour off
    /// the cow herself without checking the HUD.
    /// </summary>
    public class CowRig : MonoBehaviour
    {
        public Transform Body, Head, Udder, Tail, Muzzle;
        Transform[] _legs = new Transform[4];
        Vector3[] _legHome = new Vector3[4];
        Transform _eyeL, _eyeR, _neck;
        Material _udderMat;
        Vector3 _bodyHome, _headHome, _udderHome, _eyeHome;
        float _walkPhase, _jiggle, _jiggleVel, _blinkT, _blink = 1f;

        public float PressureNorm;
        public float Lean;                  // -1..1, driven by yaw rate
        float _chompT;
        Vector3 _muzzleHome;
        public Vector3 FlavorMix = Vector3.zero;

        public static CowRig Build(Transform parent)
        {
            var root = Mk.Empty("CowRig", parent);
            root.transform.localScale = Vector3.one * 1.3f;   // reads better on camera
            var rig = root.AddComponent<CowRig>();
            rig.Construct();
            return rig;
        }

        void Construct()
        {
            var white = Mk.Mat(Palette.CowWhite, 0.18f);
            var black = Mk.Mat(Palette.CowBlack, 0.24f);
            var peach = Mk.Mat(Palette.Peach, 0.40f);
            var horn  = Mk.Mat(new Color(0.92f, 0.88f, 0.76f), 0.34f);
            
            
            _udderMat = Mk.Mat(Palette.Cream, 0.55f);

            // --- torso: three overlapping masses, not one capsule ---------------
            Body = Mk.Empty("Body", transform, new Vector3(0f, 0.86f, 0f)).transform;
            Mk.Prim(PrimitiveType.Sphere, Body, new Vector3(0f, 0.00f,  0.00f), new Vector3(0.95f, 0.84f, 1.28f), white, "Barrel", outline: true);
            Mk.Prim(PrimitiveType.Sphere, Body, new Vector3(0f, -0.01f, 0.44f), new Vector3(0.88f, 0.80f, 0.74f), white, "Chest", outline: true);
            Mk.Prim(PrimitiveType.Sphere, Body, new Vector3(0f, 0.03f, -0.44f), new Vector3(0.90f, 0.86f, 0.76f), white, "Rump", outline: true);

            // Holstein spots, pressed flat against the surface they sit on.
            AddSpot(new Vector3( 0.85f,  0.30f,  0.35f), 0.44f, black);
            AddSpot(new Vector3(-0.80f,  0.15f, -0.45f), 0.50f, black);
            AddSpot(new Vector3( 0.10f,  0.95f, -0.30f), 0.46f, black);
            AddSpot(new Vector3(-0.35f,  0.80f,  0.55f), 0.32f, black);
            AddSpot(new Vector3( 0.55f, -0.55f, -0.70f), 0.30f, black);

            // --- neck -----------------------------------------------------------
            // Thick and long enough to bury one end in the chest and the other in the skull.
            _neck = Mk.Prim(PrimitiveType.Capsule, Body, new Vector3(0f, 0.21f, 0.54f),
                            new Vector3(0.46f, 0.32f, 0.46f), white, "Neck", outline: true).transform;
            _neck.localRotation = Quaternion.Euler(54f, 0f, 0f);

            // --- head -------------------------------------------------------------
            Head = Mk.Empty("Head", Body, new Vector3(0f, 0.44f, 0.78f)).transform;
            Mk.Prim(PrimitiveType.Sphere, Head, Vector3.zero, new Vector3(0.50f, 0.47f, 0.54f), white, "Skull", outline: true);
            Mk.Prim(PrimitiveType.Sphere, Head, new Vector3(0f, 0.20f, -0.06f), new Vector3(0.46f, 0.30f, 0.46f), black, "Forelock");

            Muzzle = Mk.Prim(PrimitiveType.Sphere, Head, new Vector3(0f, -0.15f, 0.26f),
                             new Vector3(0.36f, 0.30f, 0.34f), peach, "Muzzle", outline: true).transform;
            Mk.Prim(PrimitiveType.Sphere, Muzzle, new Vector3( 0.26f, 0.18f, 0.34f), new Vector3(0.22f, 0.26f, 0.16f), black, "NostrilR");
            Mk.Prim(PrimitiveType.Sphere, Muzzle, new Vector3(-0.26f, 0.18f, 0.34f), new Vector3(0.22f, 0.26f, 0.16f), black, "NostrilL");
            Mk.Prim(PrimitiveType.Sphere, Muzzle, new Vector3(0f, -0.30f, 0.30f), new Vector3(0.52f, 0.10f, 0.20f), black, "Mouth");

            _eyeR = MakeEye(new Vector3( 0.21f, 0.11f, 0.32f), black);
            _eyeL = MakeEye(new Vector3(-0.21f, 0.11f, 0.32f), black);
            _eyeHome = _eyeR.localScale;

            var earR = Mk.Prim(PrimitiveType.Sphere, Head, new Vector3( 0.27f, 0.07f, -0.05f), new Vector3(0.30f, 0.14f, 0.18f), black, "EarR", outline: true);
            var earL = Mk.Prim(PrimitiveType.Sphere, Head, new Vector3(-0.27f, 0.07f, -0.05f), new Vector3(0.30f, 0.14f, 0.18f), black, "EarL", outline: true);
            earR.transform.localRotation = Quaternion.Euler(0f, -18f, -34f);
            earL.transform.localRotation = Quaternion.Euler(0f,  18f,  34f);

            foreach (float sx in new[] { -1f, 1f })
            {
                var h = Mk.Prim(PrimitiveType.Sphere, Head, new Vector3(0.17f * sx, 0.26f, 0.02f),
                                new Vector3(0.14f, 0.17f, 0.14f), horn, "Horn", outline: true);
                h.transform.localRotation = Quaternion.Euler(0f, 0f, 26f * sx);
            }

            // --- legs: thigh -> shin -> hoof ----------------------------------
            // Leg roots sit INSIDE the barrel (y = -0.09, well above the hide at
            // y = -0.17 for these x/z), so the thigh emerges from the body instead
            // of floating below it. Each segment overlaps the next by ~0.06.
            var legPos = new[] {
                new Vector3( 0.30f, -0.09f,  0.42f),
                new Vector3(-0.30f, -0.09f,  0.42f),
                new Vector3( 0.30f, -0.09f, -0.42f),
                new Vector3(-0.30f, -0.09f, -0.42f),
            };
            for (int i = 0; i < 4; i++)
            {
                var leg = Mk.Empty("Leg" + i, Body, legPos[i]).transform;
                Mk.Prim(PrimitiveType.Capsule, leg, new Vector3(0f, -0.20f, 0f), new Vector3(0.23f, 0.22f, 0.23f), white, "Thigh", outline: true);
                Mk.Prim(PrimitiveType.Capsule, leg, new Vector3(0f, -0.52f, 0f), new Vector3(0.16f, 0.16f, 0.16f), white, "Shin", outline: true);
                Mk.Prim(PrimitiveType.Cylinder, leg, new Vector3(0f, -0.72f, 0f), new Vector3(0.185f, 0.06f, 0.185f), black, "Hoof", outline: true);
                _legs[i] = leg;
                _legHome[i] = legPos[i];
            }

            // --- udder: the visible pressure gauge ----------------------------------
            Udder = Mk.Empty("Udder", Body, new Vector3(0f, -0.28f, -0.12f)).transform;
            Mk.Prim(PrimitiveType.Sphere, Udder, Vector3.zero, new Vector3(0.44f, 0.38f, 0.48f), _udderMat, "Bag", outline: true);
            foreach (var t in new[] { new Vector3( 0.14f, -0.52f,  0.16f), new Vector3(-0.14f, -0.52f,  0.16f),
                                      new Vector3( 0.14f, -0.52f, -0.16f), new Vector3(-0.14f, -0.52f, -0.16f) })
                Mk.Prim(PrimitiveType.Capsule, Udder, t, new Vector3(0.15f, 0.20f, 0.15f), peach, "Teat");

            // --- tail -----------------------------------------------------------------
            Tail = Mk.Empty("Tail", Body, new Vector3(0f, 0.28f, -0.66f)).transform;
            Mk.Prim(PrimitiveType.Capsule, Tail, new Vector3(0f, -0.24f, 0f), new Vector3(0.06f, 0.22f, 0.06f), white, "TailBone");
            Mk.Prim(PrimitiveType.Sphere, Tail, new Vector3(0f, -0.50f, 0f), new Vector3(0.15f, 0.19f, 0.15f), black, "Tuft");

            _muzzleHome = Muzzle.localScale;
            _bodyHome = Body.localPosition;
            _headHome = Head.localPosition;
            _udderHome = Udder.localScale;
            _blinkT = Random.Range(1.5f, 4f);
        }

        // Radii of the barrel ellipsoid, used to seat things on the hide.
        static readonly Vector3 BarrelR = new Vector3(0.475f, 0.42f, 0.64f);

        /// <summary>Distance from the body centre to the barrel surface along a direction.</summary>
        static float SurfaceDist(Vector3 n) =>
            1f / Mathf.Sqrt((n.x / BarrelR.x) * (n.x / BarrelR.x) +
                            (n.y / BarrelR.y) * (n.y / BarrelR.y) +
                            (n.z / BarrelR.z) * (n.z / BarrelR.z));

        /// <summary>
        /// Seat a Holstein patch on the hide. Placing it at a fixed radius leaves it
        /// hovering off the body, so we solve for the actual ellipsoid surface along
        /// the patch direction and sink the disc slightly below it - the sphere then
        /// pokes through as a flat patch that follows the curve.
        /// </summary>
        void AddSpot(Vector3 dir, float size, Material black)
        {
            var n = dir.normalized;
            float t = SurfaceDist(n);
            var sp = Mk.Prim(PrimitiveType.Sphere, Body, n * (t * 0.86f), Vector3.one, black, "Spot");
            sp.transform.localRotation = Quaternion.LookRotation(n);
            sp.transform.localScale = new Vector3(size, size * 0.86f, size * 0.46f);
        }

        Transform MakeEye(Vector3 pos, Material black)
        {
            // The mascot's eyes are flat black ovals - adding a sclera and a
            // specular glint made her look like a different character entirely.
            var eye = Mk.Empty("Eye", Head, pos).transform;
            Mk.Prim(PrimitiveType.Sphere, eye, Vector3.zero, new Vector3(0.17f, 0.21f, 0.13f), black, "Pupil");
            return eye;
        }

        public void Wobble(float force) => _jiggleVel += force;

        /// <summary>Head-dip and muzzle pop when she eats something.</summary>
        public void Chomp() => _chompT = 1f;

        public void Tick(float speed01, float dt)
        {
            _jiggleVel += -_jiggle * 90f * dt;
            _jiggleVel *= Mathf.Exp(-6f * dt);
            _jiggle += _jiggleVel * dt;

            float p = PressureNorm;

            // Gait: diagonal pairs, body bobbing at twice the step rate.
            _walkPhase += dt * Mathf.Lerp(0f, 11f, speed01);
            for (int i = 0; i < 4; i++)
            {
                float ph = _walkPhase + ((i == 0 || i == 3) ? 0f : Mathf.PI);
                float swing = Mathf.Sin(ph) * 0.22f * speed01;
                float lift = Mathf.Max(0f, Mathf.Cos(ph)) * 0.11f * speed01;
                _legs[i].localPosition = _legHome[i] + new Vector3(0f, lift, swing);
                _legs[i].localRotation = Quaternion.Euler(swing * -58f, 0f, 0f);
            }

            float bob = Mathf.Sin(_walkPhase * 2f) * 0.045f * speed01;
            float tremor = p > 0.55f ? (p - 0.55f) / 0.45f : 0f;
            float shake = Mathf.Sin(Time.time * Mathf.Lerp(18f, 46f, tremor)) * 0.035f * tremor;

            Body.localPosition = _bodyHome + new Vector3(shake * 0.6f, bob + _jiggle * 0.10f, 0f);
            Body.localRotation = Quaternion.Euler(0f, 0f, -Lean * 7f);
            float swell = 1f + p * 0.15f;
            Body.localScale = new Vector3(swell + _jiggle * 0.09f, swell - _jiggle * 0.12f, swell + _jiggle * 0.09f);

            _chompT = Mathf.Max(0f, _chompT - dt * 3.6f);
            float dip = Mathf.Sin(Mathf.Clamp01(_chompT) * Mathf.PI);
            Head.localPosition = _headHome + new Vector3(0f, Mathf.Sin(_walkPhase * 2f + 0.6f) * 0.03f * speed01 + p * 0.05f - dip * 0.13f, dip * 0.05f);
            Head.localRotation = Quaternion.Euler(Mathf.Lerp(4f, -18f, p) + dip * 34f, Mathf.Sin(Time.time * 0.8f) * 3f * (1f - speed01), shake * 20f);
            Muzzle.localScale = _muzzleHome * (1f + dip * 0.35f);

            // Blink - cheap, but it is what stops her reading as an object.
            _blinkT -= dt;
            if (_blinkT <= 0f) { _blinkT = Random.Range(1.8f, 5.5f); _blink = 0f; }
            _blink = Mathf.Min(1f, _blink + dt * 9f);
            float lid = Mathf.Lerp(0.08f, 1f, Mathf.Clamp01(_blink));
            var es = new Vector3(_eyeHome.x, _eyeHome.y * lid, _eyeHome.z);
            _eyeL.localScale = es; _eyeR.localScale = es;

            // Udder: swells and glows with the flavour in the tank.
            float u = 1f + p * 1.05f + _jiggle * 0.22f;
            Udder.localScale = Vector3.Scale(_udderHome, new Vector3(u, u * (1f + p * 0.25f), u));
            var c = Palette.Tank(FlavorMix);
            _udderMat.color = Color.Lerp(Palette.Cream, c, Mathf.Clamp01(FlavorMix.magnitude * 1.6f));
            _udderMat.SetColor("_EmissionColor", c * Mathf.Lerp(0f, 1.4f, Mathf.Max(0f, p - 0.5f) * 2f));
            if (p > 0.5f) _udderMat.EnableKeyword("_EMISSION");

            Tail.localRotation = Quaternion.Euler(24f, Mathf.Sin(Time.time * Mathf.Lerp(2.2f, 9f, p)) * 26f, 0f);
        }

        public void SetVisible(bool v)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = v;
        }
    }
}
