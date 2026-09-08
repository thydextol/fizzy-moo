using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// The real product. Instantiates the 12oz sleek can model exported from Fizzy
    /// Moo's own Blender source, normalises it to a target height (the export scale
    /// is not authored for game units), and re-applies the production label artwork
    /// so the can in the player's hand is the can on the shelf.
    ///
    /// Falls back to a procedural sleek can if the model is missing, so the game
    /// still runs from a checkout without the brand assets.
    /// </summary>
    public class Can : MonoBehaviour
    {
        public Flavor Flavor { get; private set; }
        Transform _spin;

        public static Can Build(Transform parent, Vector3 pos, Flavor f, float targetHeight = 0.62f)
        {
            var go = Mk.Empty("Can", parent, pos);
            var c = go.AddComponent<Can>();
            c.Set(f, targetHeight);
            return c;
        }

        public void Set(Flavor f, float targetHeight = 0.62f)
        {
            Flavor = f;
            if (_spin != null) Destroy(_spin.gameObject);
            _spin = Mk.Empty("Spin", transform).transform;

            var prefab = Brand.CanPrefab(f);
            if (prefab != null) BuildFromModel(prefab, targetHeight);
            else BuildProcedural(f, targetHeight);
        }

        void BuildFromModel(GameObject prefab, float targetHeight)
        {
            var inst = Instantiate(prefab, _spin);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Destroy(inst); BuildProcedural(Flavor, targetHeight); return; }

            // Measure in the model's OWN local space. Renderer.bounds is world space,
            // and this can is parented under a customer that starts at scale zero
            // during its pop-in, which made the measurement degenerate and produced
            // a can the size of a barn.
            var local = LocalBounds(inst.transform);
            float h = local.size.y;
            float s = h > 0.0001f ? targetHeight / h : 1f;
            inst.transform.localScale = Vector3.one * s;
            // seat the base on the anchor and centre it horizontally
            inst.transform.localPosition = new Vector3(-local.center.x, -local.min.y, -local.center.z) * s;

            // FBX material import is unreliable headless; bind the label explicitly.
            var label = Resources.Load<Texture2D>("Brand/" + LabelName(Flavor));
            if (label != null)
            {
                var mat = Mk.Mat(Color.white, 0.62f, 0.30f);
                mat.mainTexture = label;
                foreach (var r in rends) r.sharedMaterial = mat;
            }
            else Debug.LogWarning("[Brand] missing label texture: " + LabelName(Flavor));

            foreach (var c in inst.GetComponentsInChildren<Collider>()) Destroy(c);
        }


        /// <summary>Bounds of a hierarchy expressed in the root's own local space.</summary>
        static Bounds LocalBounds(Transform root)
        {
            var b = new Bounds();
            bool first = true;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var c = m.MultiplyPoint3x4(mb.center);
                var ax = m.MultiplyVector(new Vector3(mb.extents.x, 0f, 0f));
                var ay = m.MultiplyVector(new Vector3(0f, mb.extents.y, 0f));
                var az = m.MultiplyVector(new Vector3(0f, 0f, mb.extents.z));
                var e = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x),
                                    Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y),
                                    Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
                var one = new Bounds(c, e * 2f);
                if (first) { b = one; first = false; } else b.Encapsulate(one);
            }
            return b;
        }

        static string LabelName(Flavor f) =>
            f == Flavor.KeyLime ? "Label_KeyLime" : f == Flavor.OrangeCream ? "Label_OrangeCream" : "Label_PinaColada";

        void BuildProcedural(Flavor f, float targetHeight)
        {
            var body = Mk.Mat(Palette.BrandOf(f), 0.55f, 0.4f);
            var lid  = Mk.Mat(Palette.Metal, 0.85f, 0.95f);
            float r = targetHeight * 0.30f;
            Mk.Prim(PrimitiveType.Cylinder, _spin, new Vector3(0f, targetHeight * 0.5f, 0f),
                    new Vector3(r * 2f, targetHeight * 0.46f, r * 2f), body, "Body");
            Mk.Prim(PrimitiveType.Cylinder, _spin, new Vector3(0f, targetHeight * 0.97f, 0f),
                    new Vector3(r * 1.7f, targetHeight * 0.03f, r * 1.7f), lid, "Lid");
            Mk.Prim(PrimitiveType.Cylinder, _spin, new Vector3(0f, targetHeight * 0.03f, 0f),
                    new Vector3(r * 1.7f, targetHeight * 0.03f, r * 1.7f), lid, "Base");
        }

        public bool Spin = true;
        public float Bob = 0f;
        float _t;

        void Update()
        {
            if (_spin == null) return;
            _t += Time.deltaTime;
            if (Spin) _spin.localRotation = Quaternion.Euler(0f, _t * 46f, 0f);
            if (Bob > 0f) _spin.localPosition = new Vector3(0f, Mathf.Sin(_t * 2.2f) * Bob, 0f);
        }
    }
}
