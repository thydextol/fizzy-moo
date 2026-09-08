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
        public bool Spin = true;
        public float SpinSpeed = 46f;
        public float Bob = 0f;

        Transform _spin;
        float _t;

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
            // A wrapper carries the normalisation so the model keeps its authored
            // transform - the FBX root holds the Z-up to Y-up conversion, and
            // resetting it laid the can on its side.
            var norm = Mk.Empty("Norm", _spin).transform;
            var inst = Instantiate(prefab, norm);

            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Destroy(inst); BuildProcedural(Flavor, targetHeight); return; }

            var b = HierarchyBounds(inst.transform);
            float h = b.size.y;
            float s = h > 0.0001f ? targetHeight / h : 1f;
            norm.localScale = Vector3.one * s;
            // seat the base on the anchor and centre it horizontally
            norm.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z) * s;

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

        /// <summary>
        /// Bounds of a hierarchy in its parent's space, composed purely from local TRS.
        /// Renderer.bounds and world matrices are useless here: the customer holding
        /// the can is scaled to ZERO between orders, which made the first version
        /// measure garbage and produce a barn-sized can lying on its side.
        /// </summary>
        static Bounds HierarchyBounds(Transform root)
        {
            var b = new Bounds(); bool first = true;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var m = Matrix4x4.identity;
                for (var t = mf.transform; t != null; t = t.parent)
                {
                    m = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * m;
                    if (t == root) break;
                }
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var p = m.MultiplyPoint3x4(corner);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
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

        void Update()
        {
            if (_spin == null) return;
            _t += Time.deltaTime;
            if (Spin) _spin.localRotation = Quaternion.Euler(0f, _t * SpinSpeed, 0f);
            if (Bob > 0f) _spin.localPosition = new Vector3(0f, Mathf.Sin(_t * 2.2f) * Bob, 0f);
        }
    }
}
