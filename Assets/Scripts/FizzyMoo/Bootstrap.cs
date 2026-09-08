using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// The only component that needs to exist in the saved scene. Everything else -
    /// terrain, props, lighting, cow, stand, UI, audio - is constructed here at
    /// runtime. Keeping the scene file essentially empty means the whole game is
    /// reviewable as source code and there are no binary merge conflicts.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        public bool DemoMode;      // autopilot drives the cow
        public bool RecordMode;    // dump a PNG sequence for the trailer

        const float Half = 16f;    // half-size of the fenced meadow
        System.Random _rng = new System.Random(20260908);

        void Awake()
        {
            Application.targetFrameRate = 60;
            ParseArgs();

            var sfxGo = Mk.Empty("Audio", transform);
            sfxGo.AddComponent<Sfx>();

            BuildEnvironment();
            var cam = BuildCamera();
            var cow = BuildCow();
            Fruit.Avoid = cow.transform;
            var stand = SodaStand.Build(transform, new Vector3(0f, 0f, 8.5f));
            stand.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            ScatterFruit(20);

            var hud = HUD.Build(transform);

            var rig = CameraRig.Build(cam);
            rig.Target = cow.transform;
            rig.Cow = cow;

            var gm = gameObject.AddComponent<GameManager>();
            gm.Cow = cow; gm.Stand = stand; gm.Hud = hud; gm.Cam = rig;

            if (DemoMode)
            {
                var auto = gameObject.AddComponent<AutoPilot>();
                auto.Bind(cow, stand, FindObjectsByType<Fruit>());
                auto.Enabled = true;
                gm.Auto = auto;
            }
            if (RecordMode) gameObject.AddComponent<FrameRecorder>();
        }

        void ParseArgs()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
            {
                if (a == "--demo") DemoMode = true;
                if (a == "--record") { RecordMode = true; DemoMode = true; }
            }
        }

        // -------------------------------------------------------------------------

        void BuildEnvironment()
        {
            // --- sky + atmosphere ---------------------------------------------
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.58f, 0.60f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.30f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.71f, 0.83f, 0.93f);
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 135f;

            var sun = Mk.Empty("Sun", transform).AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.87f);
            sun.intensity = 1.32f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.62f;
            sun.transform.rotation = Quaternion.Euler(46f, 38f, 0f);

            // A cool fill from the opposite side keeps shadows from going muddy.
            var fill = Mk.Empty("Fill", transform).AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.62f, 0.72f, 0.92f);
            fill.intensity = 0.32f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(28f, 214f, 0f);

            // --- ground --------------------------------------------------------
            var grass = Mk.Mat(Palette.Grass, 0.06f);
            // Mesh extends far past the fence so the player never sees the edge of the world.
            var ground = Mk.Prim(PrimitiveType.Cube, transform, new Vector3(0f, -0.5f, 0f),
                                 new Vector3(260f, 1f, 260f), grass, "Ground", collider: true);
            ground.isStatic = true;

            // Mown patches: flat discs of slightly different green break up the plane.
            var dark = Mk.Mat(Palette.GrassDark, 0.05f);
            for (int i = 0; i < 26; i++)
            {
                var p = Mk.OnRing(2f, Half - 1.5f, _rng);
                float s = Mathf.Lerp(2.5f, 8f, (float)_rng.NextDouble());
                Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(p.x, 0.005f, p.z),
                        new Vector3(s, 0.005f, s * Mathf.Lerp(0.6f, 1f, (float)_rng.NextDouble())), dark, "Patch");
            }

            // --- fence ----------------------------------------------------------
            var wood = Mk.Mat(new Color(0.62f, 0.47f, 0.32f), 0.15f);
            for (float x = -Half; x <= Half; x += 3f)
            {
                MakeFencePost(new Vector3(x, 0f,  Half), 0f, wood);
                MakeFencePost(new Vector3(x, 0f, -Half), 0f, wood);
            }
            for (float z = -Half + 3f; z < Half; z += 3f)
            {
                MakeFencePost(new Vector3( Half, 0f, z), 90f, wood);
                MakeFencePost(new Vector3(-Half, 0f, z), 90f, wood);
            }

            // Invisible walls so the cow cannot wander out of the meadow.
            foreach (var (pos, scale) in new[] {
                (new Vector3(0f, 2f,  Half + 0.6f), new Vector3(Half * 2f + 4f, 6f, 0.6f)),
                (new Vector3(0f, 2f, -Half - 0.6f), new Vector3(Half * 2f + 4f, 6f, 0.6f)),
                (new Vector3( Half + 0.6f, 2f, 0f), new Vector3(0.6f, 6f, Half * 2f + 4f)),
                (new Vector3(-Half - 0.6f, 2f, 0f), new Vector3(0.6f, 6f, Half * 2f + 4f)) })
            {
                var w = Mk.Prim(PrimitiveType.Cube, transform, pos, scale, null, "Wall", collider: true);
                w.GetComponent<Renderer>().enabled = false;
            }

            // --- scenery ------------------------------------------------------------
            for (int i = 0; i < 16; i++)
            {
                var p = Mk.OnRing(Half + 4f, Half + 30f, _rng);
                MakeTree(new Vector3(p.x, 0f, p.z), Mathf.Lerp(0.8f, 1.6f, (float)_rng.NextDouble()));
            }
            for (int i = 0; i < 9; i++)
            {
                var p = Mk.OnRing(5f, Half - 2f, _rng);
                float s = Mathf.Lerp(0.35f, 0.9f, (float)_rng.NextDouble());
                var rock = Mk.Prim(PrimitiveType.Sphere, transform, new Vector3(p.x, s * 0.28f, p.z),
                                   new Vector3(s, s * 0.62f, s * 0.85f), Mk.Mat(new Color(0.56f, 0.56f, 0.58f), 0.1f), "Rock");
                rock.transform.localRotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
            }
            // distant hills
            var hillMat = Mk.Mat(new Color(0.44f, 0.60f, 0.44f), 0.02f);
            for (int i = 0; i < 10; i++)
            {
                var p = Mk.OnRing(72f, 108f, _rng);
                float s = Mathf.Lerp(22f, 50f, (float)_rng.NextDouble());
                Mk.Prim(PrimitiveType.Sphere, transform, new Vector3(p.x, -s * 0.30f, p.z),
                        new Vector3(s, s * 0.55f, s), hillMat, "Hill");
            }
            // clouds
            var cloudMat = Mk.Mat(new Color(1f, 1f, 1f), 0.0f);
            for (int i = 0; i < 14; i++)
            {
                var p = Mk.OnRing(18f, 85f, _rng);
                var c = Mk.Empty("Cloud", transform, new Vector3(p.x, Mathf.Lerp(24f, 40f, (float)_rng.NextDouble()), p.z));
                int puffs = 3 + _rng.Next(3);
                for (int j = 0; j < puffs; j++)
                {
                    float s = Mathf.Lerp(4f, 9f, (float)_rng.NextDouble());
                    Mk.Prim(PrimitiveType.Sphere, c.transform,
                            new Vector3((j - puffs * 0.5f) * 3.1f, (float)_rng.NextDouble() * 1.4f, (float)_rng.NextDouble() * 2f),
                            new Vector3(s, s * 0.55f, s * 0.8f), cloudMat, "Puff");
                }
                c.AddComponent<Drift>().Speed = Mathf.Lerp(0.25f, 0.8f, (float)_rng.NextDouble());
            }
        }

        void MakeFencePost(Vector3 pos, float yaw, Material wood)
        {
            var post = Mk.Empty("Fence", transform, pos);
            post.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Mk.Prim(PrimitiveType.Cube, post.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.16f, 1.25f, 0.16f), wood);
            Mk.Prim(PrimitiveType.Cube, post.transform, new Vector3(1.5f, 0.92f, 0f), new Vector3(3f, 0.11f, 0.08f), wood);
            Mk.Prim(PrimitiveType.Cube, post.transform, new Vector3(1.5f, 0.52f, 0f), new Vector3(3f, 0.11f, 0.08f), wood);
        }

        void MakeTree(Vector3 pos, float scale)
        {
            var t = Mk.Empty("Tree", transform, pos);
            t.transform.localScale = Vector3.one * scale;
            t.transform.localRotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
            Mk.Prim(PrimitiveType.Cylinder, t.transform, new Vector3(0f, 1.5f, 0f), new Vector3(0.42f, 1.5f, 0.42f),
                    Mk.Mat(new Color(0.40f, 0.29f, 0.19f), 0.1f), "Trunk");
            var leaf = Mk.Mat(new Color(0.24f, 0.48f, 0.24f), 0.05f);
            var leaf2 = Mk.Mat(new Color(0.30f, 0.56f, 0.27f), 0.05f);
            Mk.Prim(PrimitiveType.Sphere, t.transform, new Vector3(0f, 3.5f, 0f), new Vector3(3.2f, 2.8f, 3.2f), leaf, "Canopy");
            Mk.Prim(PrimitiveType.Sphere, t.transform, new Vector3(0.9f, 4.3f, 0.4f), new Vector3(2.0f, 1.8f, 2.0f), leaf2, "Canopy2");
            Mk.Prim(PrimitiveType.Sphere, t.transform, new Vector3(-0.8f, 4.0f, -0.5f), new Vector3(1.8f, 1.6f, 1.8f), leaf2, "Canopy3");
        }

        Camera BuildCamera()
        {
            var go = Mk.Empty("MainCamera", transform, new Vector3(0f, 6f, -10f));
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.55f, 0.76f, 0.94f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 58f;
            cam.farClipPlane = 320f;
            go.AddComponent<AudioListener>();
            return cam;
        }

        CowController BuildCow()
        {
            var go = Mk.Empty("Bessie", transform, new Vector3(0f, 1.4f, -4f));
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.78f; col.height = 2.10f; col.center = new Vector3(0f, 1.05f, 0f);
            var mat = new PhysicsMaterial("CowPhys") { dynamicFriction = 0.3f, staticFriction = 0.3f, bounciness = 0.15f };
            col.material = mat;
            go.AddComponent<Rigidbody>();
            return go.AddComponent<CowController>();
        }

        void ScatterFruit(int n)
        {
            var root = Mk.Empty("Berries", transform);
            for (int i = 0; i < n; i++)
            {
                var p = Fruit.PickSpot(_rng);
                Fruit.Spawn(root.transform, (Flavor)(i % 3), new Vector3(p.x, 0f, p.z));
            }
        }
    }

    /// <summary>Slow horizontal drift, used for clouds.</summary>
    public class Drift : MonoBehaviour
    {
        public float Speed = 0.5f;
        void Update()
        {
            transform.position += Vector3.right * (Speed * Time.deltaTime);
            if (transform.position.x > 130f)
                transform.position = new Vector3(-130f, transform.position.y, transform.position.z);
        }
    }
}
