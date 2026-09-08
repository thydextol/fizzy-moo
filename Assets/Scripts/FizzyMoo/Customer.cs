using UnityEngine;
using UnityEngine.UI;

namespace FizzyMoo
{
    /// <summary>
    /// A thirsty villager. Holds one order (flavour + fill level), shows it three
    /// ways so it reads from anywhere in the meadow: the real can they want held
    /// overhead, a billboarded order card (flavour name + a mini glass with the
    /// target line), and a shirt in the flavour colour. Every customer is restyled
    /// from a seed so the queue does not look like clones.
    /// </summary>
    public class Customer : MonoBehaviour
    {
        public Flavor Want;
        public float WantFill;          // 0..1 target fill
        public float Patience = 26f;
        public float PatienceLeft;
        public bool Active;
        const float Size = 1.22f;       // a bit over life-size so they read from across the meadow

        Transform _body, _head, _armL, _armR, _patienceRing, _hair, _hat, _card;
        Material _shirtMat, _ringMat, _skinMat, _hairMat;
        Can _reference;
        Text _cardName, _cardPct, _cardReact;
        Image _cardBg, _glassFill, _glassLine;
        Camera _cam;
        float _phase, _reactT;
        static System.Random _rng = new System.Random(99);

        static readonly Color[] Skins = {
            new Color(0.96f, 0.82f, 0.68f), new Color(0.85f, 0.64f, 0.46f),
            new Color(0.62f, 0.42f, 0.28f), new Color(0.40f, 0.26f, 0.18f) };
        static readonly Color[] Hairs = {
            new Color(0.12f, 0.10f, 0.10f), new Color(0.35f, 0.22f, 0.12f), new Color(0.80f, 0.62f, 0.30f),
            new Color(0.62f, 0.20f, 0.14f), new Color(0.85f, 0.85f, 0.88f) };

        public static Customer Build(Transform parent, Vector3 pos)
        {
            var go = Mk.Empty("Customer", parent, pos);
            var c = go.AddComponent<Customer>();
            c.Construct();
            return c;
        }

        void Construct()
        {
            _shirtMat = Mk.Mat(Palette.KeyLime, 0.2f);
            _skinMat  = Mk.Mat(Skins[0], 0.25f);
            _hairMat  = Mk.Mat(Hairs[0], 0.2f);
            var ink = Mk.Mat(Palette.Ink);
            var pants = Mk.Mat(new Color(0.25f, 0.28f, 0.40f));

            _body = Mk.Empty("Body", transform, Vector3.zero).transform;
            Mk.Prim(PrimitiveType.Capsule, _body, new Vector3(0f, 0.52f, 0f), new Vector3(0.42f, 0.34f, 0.42f), _shirtMat, "Torso", outline: true);
            _head = Mk.Prim(PrimitiveType.Sphere, _body, new Vector3(0f, 1.02f, 0f), Vector3.one * 0.36f, _skinMat, "Head", outline: true).transform;
            Mk.Prim(PrimitiveType.Sphere, _head, new Vector3( 0.20f, 0.06f, 0.40f), new Vector3(0.16f, 0.20f, 0.10f), ink, "EyeR");
            Mk.Prim(PrimitiveType.Sphere, _head, new Vector3(-0.20f, 0.06f, 0.40f), new Vector3(0.16f, 0.20f, 0.10f), ink, "EyeL");
            _hair = Mk.Prim(PrimitiveType.Sphere, _head, new Vector3(0f, 0.34f, -0.06f), new Vector3(0.94f, 0.52f, 0.94f), _hairMat, "Hair").transform;
            // a flat cap, swapped in for some customers
            _hat = Mk.Empty("Hat", _head, new Vector3(0f, 0.40f, 0f)).transform;
            Mk.Prim(PrimitiveType.Cylinder, _hat, Vector3.zero, new Vector3(0.98f, 0.10f, 0.98f), _hairMat, "Crown");
            Mk.Prim(PrimitiveType.Cube, _hat, new Vector3(0f, -0.06f, 0.55f), new Vector3(0.8f, 0.06f, 0.45f), _hairMat, "Brim");
            _armL = Mk.Prim(PrimitiveType.Capsule, _body, new Vector3(-0.26f, 0.56f, 0f), new Vector3(0.12f, 0.20f, 0.12f), _skinMat, "ArmL").transform;
            _armR = Mk.Prim(PrimitiveType.Capsule, _body, new Vector3( 0.26f, 0.56f, 0f), new Vector3(0.12f, 0.20f, 0.12f), _skinMat, "ArmR").transform;
            Mk.Prim(PrimitiveType.Cylinder, _body, new Vector3(-0.12f, 0.14f, 0f), new Vector3(0.15f, 0.18f, 0.15f), pants, "LegL");
            Mk.Prim(PrimitiveType.Cylinder, _body, new Vector3( 0.12f, 0.14f, 0f), new Vector3(0.15f, 0.18f, 0.15f), pants, "LegR");

            // The order as the actual product: the real can, held up. Comically
            // oversized on purpose - the brand is "funny first, self-aware".
            var holder = Mk.Empty("OrderHolder", transform, new Vector3(0f, 1.78f, 0f)).transform;
            _reference = Can.Build(holder, Vector3.zero, Flavor.KeyLime, 0.95f);
            _reference.Bob = 0.05f;

            BuildCard();

            _ringMat = Mk.Mat(Palette.Gold, 0.4f, 0f, Palette.Gold * 0.8f);
            _patienceRing = Mk.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.02f, 0f),
                                    new Vector3(1.5f, 0.02f, 1.5f), _ringMat, "PatienceRing").transform;
        }

        /// <summary>Billboarded order card: flavour name + a mini glass with the target line.</summary>
        void BuildCard()
        {
            var go = new GameObject("OrderCard", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 3.05f, 0f);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(360f, 124f);
            rt.localScale = Vector3.one * 0.0062f;
            _card = go.transform;

            _cardBg = UIKit.Img("Bg", rt, Palette.Cream);
            _cardBg.rectTransform.anchorMin = Vector2.zero; _cardBg.rectTransform.anchorMax = Vector2.one;
            _cardBg.rectTransform.offsetMin = Vector2.zero; _cardBg.rectTransform.offsetMax = Vector2.zero;
            var edge = UIKit.Img("Edge", rt, Palette.Ink);
            edge.rectTransform.anchorMin = new Vector2(0f, 0f); edge.rectTransform.anchorMax = new Vector2(1f, 0f);
            edge.rectTransform.pivot = new Vector2(0.5f, 0f); edge.rectTransform.anchoredPosition = Vector2.zero;
            edge.rectTransform.sizeDelta = new Vector2(0f, 8f);

            var nameBox = UIKit.Rect("NameBox", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                     new Vector2(18f, 16f), new Vector2(250f, 60f));
            _cardName = UIKit.Label("Name", nameBox, "KEY LIME", 46, Palette.KeyLime, TextAnchor.MiddleLeft);
            var pctBox = UIKit.Rect("PctBox", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                    new Vector2(18f, -30f), new Vector2(250f, 40f));
            _cardPct = UIKit.Label("Pct", pctBox, "FILL TO 70%", 28, new Color(0.25f, 0.25f, 0.28f), TextAnchor.MiddleLeft);
            var reactBox = UIKit.Rect("ReactBox", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _cardReact = UIKit.Label("React", reactBox, "", 62, Palette.Gold);
            _cardReact.gameObject.SetActive(false);

            // mini glass at the right: outline, liquid to the target, and the white line
            var glass = UIKit.Img("Glass", rt, new Color(0.18f, 0.20f, 0.24f));
            glass.rectTransform.anchorMin = glass.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            glass.rectTransform.pivot = new Vector2(1f, 0.5f);
            glass.rectTransform.anchoredPosition = new Vector2(-18f, 0f);
            glass.rectTransform.sizeDelta = new Vector2(58f, 96f);
            var inner = UIKit.Img("Inner", glass.transform, Palette.Cream);
            inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
            inner.rectTransform.offsetMin = new Vector2(5f, 5f); inner.rectTransform.offsetMax = new Vector2(-5f, -5f);
            _glassFill = UIKit.Img("Fill", inner.transform, Palette.KeyLime);
            _glassFill.rectTransform.anchorMin = new Vector2(0f, 0f); _glassFill.rectTransform.anchorMax = new Vector2(1f, 0f);
            _glassFill.rectTransform.pivot = new Vector2(0.5f, 0f);
            _glassFill.rectTransform.anchoredPosition = Vector2.zero; _glassFill.rectTransform.sizeDelta = new Vector2(0f, 40f);
            _glassLine = UIKit.Img("Line", inner.transform, Palette.Ink);
            _glassLine.rectTransform.anchorMin = new Vector2(-0.25f, 0f); _glassLine.rectTransform.anchorMax = new Vector2(1.25f, 0f);
            _glassLine.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _glassLine.rectTransform.sizeDelta = new Vector2(0f, 5f);
        }

        void Restyle(int seed)
        {
            var r = new System.Random(seed);
            _skinMat.color = Skins[r.Next(Skins.Length)];
            _hairMat.color = Hairs[r.Next(Hairs.Length)];
            bool hat = r.NextDouble() < 0.35;
            _hair.gameObject.SetActive(!hat);
            _hat.gameObject.SetActive(hat);
            float h = Mathf.Lerp(0.90f, 1.14f, (float)r.NextDouble());
            _body.localScale = new Vector3(1f, h, 1f);
        }

        public void NewOrder(int difficulty)
        {
            Want = (Flavor)_rng.Next(0, 3);
            WantFill = Mathf.Lerp(0.30f, 0.92f, (float)_rng.NextDouble());
            Patience = Mathf.Max(13f, 27f - difficulty * 1.6f);
            PatienceLeft = Patience;
            Active = true;
            Restyle(_rng.Next());

            var c = Palette.Of(Want);
            _shirtMat.color = c;
            _reference.Set(Want, 0.95f);
            _reference.Bob = 0.05f;

            _cardName.text = Palette.Name(Want); _cardName.color = c;
            _cardPct.text = "FILL TO " + Mathf.RoundToInt(WantFill * 100f) + "%";
            _glassFill.color = c;
            float inner = 86f;
            _glassFill.rectTransform.sizeDelta = new Vector2(0f, WantFill * inner);
            _glassLine.rectTransform.anchoredPosition = new Vector2(0f, WantFill * inner);
            _cardReact.gameObject.SetActive(false);
            _cardName.gameObject.SetActive(true); _cardPct.gameObject.SetActive(true);
            _cardBg.color = Palette.Cream;

            transform.localScale = Vector3.zero;
            StartCoroutine(PopIn());
        }

        System.Collections.IEnumerator PopIn()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime * 2.6f)
            {
                transform.localScale = Vector3.one * (Size * Ease.OutBack(Mathf.Clamp01(t)));
                yield return null;
            }
            transform.localScale = Vector3.one * Size;
        }

        public void React(bool happy, float quality)
        {
            StopAllCoroutines();
            // The card becomes the reaction so it reads from across the field.
            _cardName.gameObject.SetActive(false); _cardPct.gameObject.SetActive(false);
            _cardReact.gameObject.SetActive(true);
            _cardReact.text = !happy ? (quality > 0.2f ? "MEH." : "NOPE.") : (quality > 0.9f ? "MOO-VELLOUS!" : "YUM!");
            _cardReact.color = happy ? (quality > 0.9f ? Palette.Gold : new Color(0.2f, 0.55f, 0.25f)) : Palette.Danger;
            _cardBg.color = happy ? Palette.Cream : new Color(1f, 0.86f, 0.82f);
            StartCoroutine(ReactRoutine(happy, quality));
        }

        System.Collections.IEnumerator ReactRoutine(bool happy, float quality)
        {
            Active = false;
            float dur = 1.0f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                if (happy)
                {
                    float j = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3f)) * (1f - u) * 0.55f * (0.5f + quality);
                    _body.localPosition = new Vector3(0f, j, 0f);
                    _armL.localRotation = Quaternion.Euler(0f, 0f,  150f * Mathf.Min(1f, u * 4f));
                    _armR.localRotation = Quaternion.Euler(0f, 0f, -150f * Mathf.Min(1f, u * 4f));
                }
                else
                {
                    _body.localPosition = new Vector3(Mathf.Sin(u * 34f) * 0.05f * (1f - u), -0.12f * Mathf.Min(1f, u * 5f), 0f);
                    _head.localRotation = Quaternion.Euler(0f, Mathf.Sin(u * 40f) * 22f * (1f - u), 0f);
                }
                yield return null;
            }
            _body.localPosition = Vector3.zero;
            _armL.localRotation = Quaternion.identity;
            _armR.localRotation = Quaternion.identity;
            for (float t = 0f; t < 1f; t += Time.deltaTime * 3.2f)
            {
                transform.localScale = Vector3.one * (Size * (1f - Ease.OutCubic(Mathf.Clamp01(t))));
                yield return null;
            }
            transform.localScale = Vector3.zero;
        }

        void Update()
        {
            _phase += Time.deltaTime;
            if (_cam == null) _cam = Camera.main;
            if (_card != null && _cam != null)
                _card.rotation = Quaternion.LookRotation(_card.position - _cam.transform.position);

            if (!Active) return;

            PatienceLeft -= Time.deltaTime;
            float p = Mathf.Clamp01(PatienceLeft / Patience);
            _patienceRing.localScale = new Vector3(1.5f * p, 0.02f, 1.5f * p);
            _ringMat.color = Color.Lerp(Palette.Danger, Palette.Gold, p);
            _ringMat.SetColor("_EmissionColor", _ringMat.color * (p < 0.3f ? Ease.Pulse(_phase, 4f) * 1.4f : 0.6f));
            // the card blushes red as patience runs out
            _cardBg.color = p < 0.3f ? Color.Lerp(Palette.Cream, new Color(1f, 0.80f, 0.76f), Ease.Pulse(_phase, 3f)) : Palette.Cream;

            float fidget = Mathf.Lerp(1.2f, 5.0f, 1f - p);
            _body.localPosition = new Vector3(Mathf.Sin(_phase * fidget) * 0.03f, Mathf.Abs(Mathf.Sin(_phase * fidget * 1.6f)) * 0.03f, 0f);
            _head.localRotation = Quaternion.Euler(0f, Mathf.Sin(_phase * fidget * 0.7f) * 14f, 0f);
        }

        public void Hide() { transform.localScale = Vector3.zero; Active = false; }
    }
}
