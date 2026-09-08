using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// The glass under the tap. A pint glass rather than a bottle: the mascot
    /// drinks from a glass with a straw on the packaging, and an open top lets
    /// the pour visibly land in it. The liquid column is driven by a 0..1 fill.
    /// </summary>
    public class Bottle : MonoBehaviour
    {
        Transform _liquidPivot, _liquid, _foam;
        Material _liquidMat, _glassMat;
        float _fill;
        public float Height = 0.92f;
        public float Radius = 0.22f;

        public static Bottle Build(Transform parent, Vector3 pos, float scale = 1f, bool ghost = false)
        {
            var go = Mk.Empty("Glass", parent, pos);
            go.transform.localScale = Vector3.one * scale;
            var b = go.AddComponent<Bottle>();
            b.Construct(ghost);
            return b;
        }

        void Construct(bool ghost)
        {
            _glassMat = Mk.Glass(new Color(0.86f, 0.94f, 0.97f, ghost ? 0.16f : 0.28f));
            _liquidMat = Mk.Mat(Palette.Cream, 0.6f);

            Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, Height * 0.5f, 0f),
                    new Vector3(Radius * 2f, Height * 0.5f, Radius * 2f), _glassMat, "Glass");
            Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, Height, 0f),
                    new Vector3(Radius * 2.12f, 0.018f, Radius * 2.12f), _glassMat, "Rim");
            Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.012f, 0f),
                    new Vector3(Radius * 2.05f, 0.012f, Radius * 2.05f),
                    Mk.Mat(new Color(0.80f, 0.88f, 0.92f), 0.7f), "Base");
            var straw = Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(Radius * 0.45f, Height * 0.62f, 0f),
                                new Vector3(0.035f, Height * 0.62f, 0.035f), Mk.Mat(Palette.Peach, 0.5f), "Straw");
            straw.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);

            _liquidPivot = Mk.Empty("LiquidPivot", transform, new Vector3(0f, 0.02f, 0f)).transform;
            _liquid = Mk.Prim(PrimitiveType.Cylinder, _liquidPivot, new Vector3(0f, 0.5f, 0f),
                              new Vector3(Radius * 1.86f, 0.5f, Radius * 1.86f), _liquidMat, "Liquid").transform;
            _foam = Mk.Prim(PrimitiveType.Cylinder, _liquidPivot, new Vector3(0f, 1.0f, 0f),
                            new Vector3(Radius * 1.90f, 0.022f, Radius * 1.90f),
                            Mk.Mat(Color.white, 0.4f), "Foam").transform;
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
            _liquid.localScale = new Vector3(Radius * 1.86f, h * 0.5f, Radius * 1.86f);
            _liquid.localPosition = new Vector3(0f, h * 0.5f, 0f);
            _foam.localPosition = new Vector3(0f, h, 0f);
        }

        /// <summary>Thin ring at a fill height - marks the customer's target line.</summary>
        public void AddTargetBand(float f01, Color c)
        {
            float h = f01 * (Height - 0.04f) + 0.02f;
            var band = Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, h, 0f),
                               new Vector3(Radius * 2.2f, 0.012f, Radius * 2.2f),
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
