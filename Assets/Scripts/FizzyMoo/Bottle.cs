using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// A glass bottle whose liquid column is driven by a 0..1 fill value.
    /// Used twice: once as the customer's *reference* bottle (the order) and once
    /// as the *live* bottle the player is filling. Putting the order in world space
    /// as a physical object - rather than as text on the HUD - means the player
    /// reads the goal by looking at the thing they are pouring into.
    /// </summary>
    public class Bottle : MonoBehaviour
    {
        Transform _liquidPivot, _liquid, _foam;
        Material _liquidMat, _glassMat;
        float _fill;
        public float Height = 0.92f;
        public float Radius = 0.20f;

        public static Bottle Build(Transform parent, Vector3 pos, float scale = 1f, bool ghost = false)
        {
            var go = Mk.Empty("Bottle", parent, pos);
            go.transform.localScale = Vector3.one * scale;
            var b = go.AddComponent<Bottle>();
            b.Construct(ghost);
            return b;
        }

        void Construct(bool ghost)
        {
            _glassMat = Mk.Glass(new Color(0.85f, 0.93f, 0.95f, ghost ? 0.16f : 0.26f));
            _liquidMat = Mk.Mat(Palette.Cream, 0.6f);

            // Body + neck + cap, all cylinders.
            Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, Height * 0.5f, 0f),
                    new Vector3(Radius * 2f, Height * 0.5f, Radius * 2f), _glassMat, "Glass");
            Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, Height + 0.10f, 0f),
                    new Vector3(Radius * 1.0f, 0.10f, Radius * 1.0f), _glassMat, "Neck");
            Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, Height + 0.22f, 0f),
                    new Vector3(Radius * 1.15f, 0.035f, Radius * 1.15f),
                    Mk.Mat(ghost ? new Color(0.7f, 0.7f, 0.7f) : Palette.Metal, 0.8f, 0.9f), "Cap");

            // Liquid column: pivot sits on the inside floor, we scale it upward.
            _liquidPivot = Mk.Empty("LiquidPivot", transform, new Vector3(0f, 0.02f, 0f)).transform;
            _liquid = Mk.Prim(PrimitiveType.Cylinder, _liquidPivot, new Vector3(0f, 0.5f, 0f),
                              new Vector3(Radius * 1.82f, 0.5f, Radius * 1.82f), _liquidMat, "Liquid").transform;
            _foam = Mk.Prim(PrimitiveType.Cylinder, _liquidPivot, new Vector3(0f, 1.0f, 0f),
                            new Vector3(Radius * 1.86f, 0.022f, Radius * 1.86f),
                            Mk.Mat(new Color(1f, 1f, 1f, 1f), 0.4f), "Foam").transform;
            SetFill(0f);
        }

        public void SetColor(Color c)
        {
            _liquidMat.color = c;
            _liquidMat.SetColor("_EmissionColor", c * 0.35f);
            _liquidMat.EnableKeyword("_EMISSION");
        }

        public float Fill => _fill;

        public void SetFill(float f01)
        {
            _fill = Mathf.Clamp01(f01);
            float h = _fill * (Height - 0.04f);
            bool any = h > 0.001f;
            _liquidPivot.gameObject.SetActive(any);
            if (!any) return;
            _liquid.localScale = new Vector3(Radius * 1.82f, h * 0.5f, Radius * 1.82f);
            _liquid.localPosition = new Vector3(0f, h * 0.5f, 0f);
            _foam.localPosition = new Vector3(0f, h, 0f);
        }

        /// <summary>Draw a thin ring at a given fill height - used to mark the target line.</summary>
        public void AddTargetBand(float f01, Color c)
        {
            float h = f01 * (Height - 0.04f) + 0.02f;
            var band = Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, h, 0f),
                               new Vector3(Radius * 2.16f, 0.012f, Radius * 2.16f),
                               Mk.Mat(c, 0.5f, 0f, c * 1.5f), "TargetBand");
            band.name = "TargetBand";
        }

        public void ClearBands()
        {
            foreach (Transform t in transform)
                if (t.name == "TargetBand") Destroy(t.gameObject);
        }
    }
}
