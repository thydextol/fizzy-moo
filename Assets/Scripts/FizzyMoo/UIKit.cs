using UnityEngine;
using UnityEngine.UI;

namespace FizzyMoo
{
    /// <summary>Procedurally generated sprites + uGUI helpers, so the UI ships with no art files.</summary>
    public static class UIKit
    {
        static Font _font;
        public static Font Font
        {
            get
            {
                if (_font == null)
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Helvetica Neue", "Helvetica", "Arial", "SF Pro Text" }, 48);
                return _font;
            }
        }

        static Sprite _box, _circle, _ring;

        /// <summary>1x1 white sprite - tinted per use.</summary>
        public static Sprite Box
        {
            get
            {
                if (_box == null)
                {
                    var t = new Texture2D(4, 4);
                    var px = new Color[16];
                    for (int i = 0; i < 16; i++) px[i] = Color.white;
                    t.SetPixels(px); t.Apply();
                    _box = Sprite.Create(t, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
                }
                return _box;
            }
        }

        public static Sprite Circle => _circle ??= Radial(128, 0f, 1f, soft: true);
        public static Sprite Ring   => _ring   ??= Radial(256, 0.74f, 1f, soft: true);

        /// <summary>Antialiased disc/annulus generated in code.</summary>
        public static Sprite Radial(int size, float inner01, float outer01, bool soft)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float ro = outer01 * c, ri = inner01 * c;
            float aa = soft ? 1.25f : 0.01f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01((ro - d) / aa) * (ri <= 0f ? 1f : Mathf.Clamp01((d - ri) / aa));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), Vector2.one * 0.5f);
        }

        public static RectTransform Rect(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static Image Img(string name, Transform parent, Color c, Sprite sp = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>();
            im.sprite = sp ?? Box;
            im.color = c;
            im.raycastTarget = false;
            return im;
        }

        public static Text Label(string name, Transform parent, string txt, int size, Color c,
                                 TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font; t.fontSize = size; t.text = txt; t.color = c;
            t.alignment = anchor; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Cheap drop shadow: a second copy of the label offset behind it.</summary>
        public static Text LabelShadowed(string name, Transform parent, string txt, int size, Color c,
                                         TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var sh = Label(name + "_sh", parent, txt, size, new Color(0f, 0f, 0f, 0.45f), anchor);
            var rt = sh.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(3, -4); rt.offsetMax = new Vector2(3, -4);
            var main = Label(name, parent, txt, size, c, anchor);
            var mrt = main.rectTransform;
            mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one; mrt.offsetMin = Vector2.zero; mrt.offsetMax = Vector2.zero;
            main.gameObject.AddComponent<ShadowLink>().Shadow = sh;
            return main;
        }
    }

    /// <summary>Keeps a shadow label's text in sync with its owner.</summary>
    public class ShadowLink : MonoBehaviour
    {
        public Text Shadow;
        Text _self;
        void Awake() => _self = GetComponent<Text>();
        void LateUpdate() { if (Shadow != null && _self != null && Shadow.text != _self.text) Shadow.text = _self.text; }
    }
}
