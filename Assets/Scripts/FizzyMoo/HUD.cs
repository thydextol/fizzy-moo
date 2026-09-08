using UnityEngine;
using UnityEngine.UI;

namespace FizzyMoo
{
    /// <summary>
    /// Screen HUD, built entirely in code from generated sprites.
    /// The pressure gauge is the piece that matters: it is the player's fuse timer,
    /// so it flashes, pulses and screams before a blowout rather than just filling.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        Canvas _canvas;
        Image _gaugeFill, _gaugeGlow, _vignette, _flash;
        Text _psi, _score, _combo, _timer, _served, _order, _hint, _bigTitle, _bigSub;
        RectTransform _gaugeRoot, _panelTitle, _panelOver, _mixRoot, _glassRoot;
        Image _orderPill, _glassFillImg, _glassLineImg, _hintPill, _needTick;
        Text _glassPct, _tank;
        RectTransform _needPivot;
        Image[] _mixBars = new Image[3];
        Text[] _pops = new Text[6];
        float[] _popT = new float[6];
        Vector2[] _popHome = new Vector2[6];
        int _popNext;
        float _phase, _scorePunch, _flashT;
        Color _flashColor = Color.white;

        public static HUD Build(Transform parent)
        {
            var go = Mk.Empty("HUD", parent);
            var h = go.AddComponent<HUD>();
            h.Construct();
            return h;
        }

        void Construct()
        {
            var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            _canvas = cgo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)cgo.transform;

            // --- danger vignette (fades in with pressure) --------------------
            _vignette = UIKit.Img("Vignette", root, new Color(1f, 0.15f, 0.1f, 0f), UIKit.Radial(256, 0.55f, 1f, true));
            Stretch(_vignette.rectTransform, -260f);

            // --- full-screen flash (blowout / perfect) -----------------------
            _flash = UIKit.Img("Flash", root, new Color(1f, 1f, 1f, 0f));
            Stretch(_flash.rectTransform, 0f);

            // --- score (top-left) ---------------------------------------------
            var sBox = UIKit.Rect("ScoreBox", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                                  new Vector2(48f, -40f), new Vector2(460f, 92f));
            _score = UIKit.LabelShadowed("Score", sBox, "0", 78, Palette.Cream, TextAnchor.MiddleLeft);
            var cBox = UIKit.Rect("ComboBox", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                                  new Vector2(52f, -126f), new Vector2(420f, 46f));
            _combo = UIKit.LabelShadowed("Combo", cBox, "", 38, Palette.Gold, TextAnchor.MiddleLeft);

            // --- timer (top-centre) --------------------------------------------
            var tBox = UIKit.Rect("TimerBox", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                  new Vector2(0f, -44f), new Vector2(420f, 96f));
            _timer = UIKit.LabelShadowed("Timer", tBox, "2:00", 76, Palette.Cream);

            // --- served (top-right) --------------------------------------------
            var vBox = UIKit.Rect("ServedBox", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                                  new Vector2(-48f, -46f), new Vector2(460f, 60f));
            _served = UIKit.LabelShadowed("Served", vBox, "0 SERVED", 40, Palette.Cream, TextAnchor.MiddleRight);
            var oBox = UIKit.Rect("OrderBox", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                                  new Vector2(-48f, -108f), new Vector2(760f, 56f));
            _orderPill = UIKit.Img("OrderPill", oBox, new Color(0f, 0f, 0f, 0.45f));
            _orderPill.rectTransform.anchorMin = _orderPill.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _orderPill.rectTransform.pivot = new Vector2(1f, 0.5f);
            _orderPill.rectTransform.anchoredPosition = new Vector2(14f, 0f);
            _orderPill.rectTransform.sizeDelta = new Vector2(0f, 60f);
            _order = UIKit.LabelShadowed("Order", oBox, "", 42, Palette.Cream, TextAnchor.MiddleRight);

            // --- pressure gauge (left, mid) --------------------------------------
            _gaugeRoot = UIKit.Rect("Gauge", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                                    new Vector2(200f, -40f), new Vector2(280f, 280f));
            var track = UIKit.Img("Track", _gaugeRoot, new Color(0f, 0f, 0f, 0.34f), UIKit.Ring);
            Stretch(track.rectTransform, 0f);
            // danger arc: last 20% of the dial, rotated into place
            var danger = UIKit.Img("DangerArc", _gaugeRoot, new Color(1f, 0.25f, 0.2f, 0.42f), UIKit.Ring);
            Stretch(danger.rectTransform, 0f);
            danger.type = Image.Type.Filled;
            danger.fillMethod = Image.FillMethod.Radial360;
            danger.fillOrigin = (int)Image.Origin360.Bottom;
            danger.fillClockwise = true;
            danger.fillAmount = 0.20f;
            danger.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -0.80f * 360f);

            _gaugeGlow = UIKit.Img("GaugeGlow", _gaugeRoot, new Color(1f, 1f, 1f, 0f), UIKit.Ring);
            Stretch(_gaugeGlow.rectTransform, 26f);

            _gaugeFill = UIKit.Img("GaugeFill", _gaugeRoot, Palette.PinaColada, UIKit.Ring);
            Stretch(_gaugeFill.rectTransform, 0f);
            _gaugeFill.type = Image.Type.Filled;
            _gaugeFill.fillMethod = Image.FillMethod.Radial360;
            _gaugeFill.fillOrigin = (int)Image.Origin360.Bottom;
            _gaugeFill.fillClockwise = true;
            _gaugeFill.fillAmount = 0f;

            // "how much is enough": a white tick on the ring at the current order's PSI
            _needPivot = UIKit.Rect("NeedPivot", _gaugeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    Vector2.zero, new Vector2(280f, 280f));
            _needTick = UIKit.Img("NeedTick", _needPivot, Color.white);
            _needTick.rectTransform.anchorMin = _needTick.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _needTick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _needTick.rectTransform.anchoredPosition = new Vector2(0f, -122f);
            _needTick.rectTransform.sizeDelta = new Vector2(12f, 44f);
            _needPivot.gameObject.SetActive(false);

            var pBox = UIKit.Rect("PsiBox", _gaugeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  new Vector2(0f, 6f), new Vector2(220f, 90f));
            _psi = UIKit.LabelShadowed("Psi", pBox, "0", 68, Palette.Cream);
            var lBox = UIKit.Rect("PsiLbl", _gaugeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  new Vector2(0f, -52f), new Vector2(220f, 40f));
            UIKit.LabelShadowed("PsiLabel", lBox, "PSI", 26, new Color(1f, 1f, 1f, 0.7f));

            // --- flavour mix bars (under the gauge) ---------------------------------
            var mixRoot = UIKit.Rect("Mix", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 1f),
                                     new Vector2(200f, -200f), new Vector2(260f, 110f));
            _mixRoot = mixRoot;
            var tkBox = UIKit.Rect("TankTitle", mixRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
                                   new Vector2(0f, 34f), new Vector2(220f, 30f));
            UIKit.LabelShadowed("TankTitle", tkBox, "IN THE TANK", 22, new Color(1f, 1f, 1f, 0.85f));
            var tkVal = UIKit.Rect("TankVal", mixRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                   new Vector2(0f, -200f), new Vector2(260f, 40f));
            _tank = UIKit.LabelShadowed("TankVal", tkVal, "EMPTY", 30, Palette.Cream);
            string[] nm = { "LIME", "ORANGE", "PINA" };
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 84f;
                var fc = Palette.Of((Flavor)i);
                var back = UIKit.Img("MixBack" + i, mixRoot, new Color(fc.r * 0.30f, fc.g * 0.30f, fc.b * 0.30f, 0.78f));
                back.rectTransform.anchorMin = back.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                back.rectTransform.pivot = new Vector2(0.5f, 0f);
                back.rectTransform.anchoredPosition = new Vector2(x, -74f);
                back.rectTransform.sizeDelta = new Vector2(60f, 80f);

                var fill = UIKit.Img("MixFill" + i, mixRoot, Palette.Of((Flavor)i));
                fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                fill.rectTransform.pivot = new Vector2(0.5f, 0f);
                fill.rectTransform.anchoredPosition = new Vector2(x, -74f);
                fill.rectTransform.sizeDelta = new Vector2(60f, 0f);
                _mixBars[i] = fill;

                var lab = UIKit.Rect("MixLbl" + i, mixRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                     new Vector2(x, -162f), new Vector2(96f, 32f));
                UIKit.LabelShadowed("L" + i, lab, nm[i], 24, fc);
            }

            // --- hint (bottom-centre) -------------------------------------------------
            var hBox = UIKit.Rect("HintBox", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                  new Vector2(0f, 74f), new Vector2(1560f, 76f));
            _hintPill = UIKit.Img("HintPill", hBox, new Color(0.04f, 0.05f, 0.09f, 0.45f));
            _hintPill.rectTransform.anchorMin = _hintPill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _hintPill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _hintPill.rectTransform.sizeDelta = new Vector2(0f, 76f);
            _hint = UIKit.LabelShadowed("Hint", hBox, "", 46, Palette.Cream);

            // --- glass meter: shown while in the pour zone, because the real glass and its
            //     target line are tiny from the chase camera --------------------------------
            _glassRoot = UIKit.Rect("GlassMeter", root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                                    new Vector2(-64f, -30f), new Vector2(96f, 380f));
            var gback = UIKit.Img("Back", _glassRoot, new Color(0f, 0f, 0f, 0.40f)); Stretch(gback.rectTransform, 0f);
            var ginner = UIKit.Img("Inner", _glassRoot, new Color(1f, 1f, 1f, 0.10f)); Stretch(ginner.rectTransform, -8f);
            _glassFillImg = UIKit.Img("Fill", _glassRoot, Palette.Cream);
            _glassFillImg.rectTransform.anchorMin = new Vector2(0f, 0f); _glassFillImg.rectTransform.anchorMax = new Vector2(1f, 0f);
            _glassFillImg.rectTransform.pivot = new Vector2(0.5f, 0f);
            _glassFillImg.rectTransform.anchoredPosition = new Vector2(0f, 8f); _glassFillImg.rectTransform.sizeDelta = new Vector2(-16f, 0f);
            _glassLineImg = UIKit.Img("Line", _glassRoot, Palette.Gold);
            _glassLineImg.rectTransform.anchorMin = new Vector2(0f, 0f); _glassLineImg.rectTransform.anchorMax = new Vector2(1f, 0f);
            _glassLineImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _glassLineImg.rectTransform.anchoredPosition = new Vector2(0f, 8f); _glassLineImg.rectTransform.sizeDelta = new Vector2(30f, 9f);
            var gpBox = UIKit.Rect("PctBox", _glassRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 0.5f),
                                   new Vector2(-24f, 8f), new Vector2(130f, 44f));
            _glassPct = UIKit.LabelShadowed("Pct", gpBox, "", 36, Palette.Gold, TextAnchor.MiddleRight);
            var gtBox = UIKit.Rect("TitleBox", _glassRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
                                   new Vector2(0f, 8f), new Vector2(200f, 34f));
            UIKit.LabelShadowed("GlassTitle", gtBox, "RELEASE AT THE LINE", 22, Palette.Cream);
            _glassRoot.gameObject.SetActive(false);

            // --- floating score popups -------------------------------------------------
            for (int i = 0; i < _pops.Length; i++)
            {
                var pb = UIKit.Rect("Pop" + i, root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    new Vector2(0f, 120f), new Vector2(760f, 90f));
                _popHome[i] = pb.anchoredPosition;
                _pops[i] = UIKit.LabelShadowed("PopT" + i, pb, "", 64, Palette.Gold);
                _pops[i].transform.parent.gameObject.SetActive(false);
            }

            // --- title / game-over panels ------------------------------------------------
            _panelTitle = BuildPanel(root, out _bigTitle, out _bigSub, "",
                                     "Eat the fruit the customer wants  -  pour at the stand  -  release on the line\nDon't hit 100 PSI.\n\nWASD to move      hold SPACE to pour      Press SPACE to start");
            BuildWordmark(_panelTitle);
            _panelOver = BuildPanel(root, out var ot, out var os, "TIME!", "");
            _overTitle = ot; _overSub = os;
            _panelOver.gameObject.SetActive(false);
        }

        Text _overTitle, _overSub;

        RectTransform BuildPanel(Transform root, out Text title, out Text sub, string t, string s)
        {
            var bg = UIKit.Img("Panel", root, new Color(0.04f, 0.05f, 0.09f, 0.78f));
            var p = bg.rectTransform;
            Stretch(p, 0f);
            var tb = UIKit.Rect("T", p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                new Vector2(0f, 120f), new Vector2(1500f, 200f));
            title = UIKit.LabelShadowed("Title", tb, t, 150, Palette.Cream);
            var sb = UIKit.Rect("S", p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                new Vector2(0f, -170f), new Vector2(1500f, 320f));
            sub = UIKit.LabelShadowed("Sub", sb, s, 44, new Color(1f, 1f, 1f, 0.9f));
            return p;
        }


        /// <summary>
        /// The wordmark artwork is solid black with an alpha channel, so tinting it
        /// light is not possible - it is laid on a cream card instead, which is also
        /// how the logo is locked up on the packaging.
        /// </summary>
        void BuildWordmark(RectTransform panel)
        {
            var tex = Brand.Wordmark;
            if (tex == null) { _bigTitle.text = "FIZZY MOO"; return; }
            _bigTitle.text = "";
            var sh = _bigTitle.GetComponent<ShadowLink>();
            if (sh != null && sh.Shadow != null) sh.Shadow.text = "";

            float aspect = tex.width / (float)tex.height;
            var card = UIKit.Img("LogoCard", panel, Palette.Cream);
            card.rectTransform.anchorMin = card.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            card.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            card.rectTransform.anchoredPosition = new Vector2(0f, 168f);
            card.rectTransform.sizeDelta = new Vector2(760f, 760f / aspect + 90f);

            var logo = UIKit.Img("LogoMark", card.transform, Color.white,
                                 Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f));
            logo.preserveAspect = true;
            logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            logo.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            logo.rectTransform.anchoredPosition = Vector2.zero;
            logo.rectTransform.sizeDelta = new Vector2(660f, 660f / aspect);

            var tagBox = UIKit.Rect("TagBox", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    new Vector2(0f, 6f), new Vector2(1200f, 60f));
            UIKit.LabelShadowed("Tagline", tagBox, Brand.Tagline, 46, Palette.Peach);
        }

        static void Stretch(RectTransform rt, float pad)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-pad, -pad); rt.offsetMax = new Vector2(pad, pad);
        }

        // --- public API ------------------------------------------------------------------

        public void ShowTitle(bool on) => _panelTitle.gameObject.SetActive(on);

        public void ShowGameOver(bool on, string title = "", string sub = "")
        {
            _panelOver.gameObject.SetActive(on);
            if (on) { _overTitle.text = title; _overSub.text = sub; }
            // the gauges have nothing to say on the results card
            if (_gaugeRoot != null) _gaugeRoot.gameObject.SetActive(!on);
            if (_mixRoot != null) _mixRoot.gameObject.SetActive(!on);
            if (on && _glassRoot != null) _glassRoot.gameObject.SetActive(false);
        }

        public void SetHint(string s)
        {
            if (_hint.text != s) _hint.text = s;
            bool any = s.Length > 0;
            if (_hintPill.gameObject.activeSelf != any) _hintPill.gameObject.SetActive(any);
            if (any) _hintPill.rectTransform.sizeDelta = new Vector2(_hint.preferredWidth + 56f, 76f);
        }

        /// <summary>Tick on the PSI ring at the pressure the current order needs.</summary>
        public void SetNeed(bool on, float need01)
        {
            if (_needPivot.gameObject.activeSelf != on) _needPivot.gameObject.SetActive(on);
            if (on) _needPivot.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Clamp01(need01) * 360f);
        }

        public void SetTank(string s, Color c) { if (_tank.text != s) _tank.text = s; _tank.color = c; }

        public void SetOrder(string s, Color c)
        {
            if (_order.text != s) _order.text = s;
            _order.color = c;
            bool any = s.Length > 0;
            if (_orderPill.gameObject.activeSelf != any) _orderPill.gameObject.SetActive(any);
            if (any) _orderPill.rectTransform.sizeDelta = new Vector2(_order.preferredWidth + 40f, 60f);
        }

        /// <summary>Vertical glass meter: current fill vs the customer's target line.</summary>
        public void SetGlass(bool on, float fill, float target, Color c)
        {
            if (_glassRoot.gameObject.activeSelf != on) _glassRoot.gameObject.SetActive(on);
            if (!on) return;
            const float H = 380f - 16f;
            _glassFillImg.color = c;
            _glassFillImg.rectTransform.sizeDelta = new Vector2(-16f, Mathf.Clamp01(fill) * H);
            _glassLineImg.rectTransform.anchoredPosition = new Vector2(0f, 8f + Mathf.Clamp01(target) * H);
            ((RectTransform)_glassPct.transform.parent).anchoredPosition = new Vector2(-24f, 8f + Mathf.Clamp01(target) * H);
            string t = Mathf.RoundToInt(target * 100f) + "%";
            if (_glassPct.text != t) _glassPct.text = t;
        }

        public void Flash(Color c, float strength)
        {
            _flashColor = c; _flashT = strength;
        }

        public void Popup(string text, Color c)
        {
            int i = _popNext; _popNext = (_popNext + 1) % _pops.Length;
            _pops[i].text = text;
            _pops[i].color = c;
            var sh = _pops[i].GetComponent<ShadowLink>();
            if (sh != null && sh.Shadow != null) sh.Shadow.text = text;
            _pops[i].transform.parent.gameObject.SetActive(true);
            _popT[i] = 1f;
        }

        public void PunchScore() => _scorePunch = 1f;

        public void Tick(float pressure01, Vector3 mix, int score, int combo, float timeLeft, int served, float dt)
        {
            _phase += dt;

            _gaugeFill.fillAmount = Mathf.Lerp(_gaugeFill.fillAmount, pressure01, dt * 14f);
            // Green -> amber -> red as the fuse burns down.
            var gc = pressure01 < 0.55f ? Color.Lerp(Palette.PinaColada, Palette.Gold, pressure01 / 0.55f)
                                        : Color.Lerp(Palette.Gold, Palette.Danger, (pressure01 - 0.55f) / 0.45f);
            _gaugeFill.color = gc;
            _psi.text = Mathf.RoundToInt(pressure01 * CowController.MaxPressure).ToString();

            float danger = Mathf.Clamp01((pressure01 - 0.68f) / 0.32f);
            float pulse = Ease.Pulse(_phase, Mathf.Lerp(2f, 9f, danger));
            _gaugeGlow.color = new Color(gc.r, gc.g, gc.b, danger * pulse * 0.55f);
            _gaugeRoot.localScale = Vector3.one * (1f + danger * pulse * 0.06f);
            _vignette.color = new Color(1f, 0.15f, 0.10f, danger * pulse * 0.42f);
            _psi.color = Color.Lerp(Palette.Cream, Palette.Danger, danger);

            float sum = Mathf.Max(1f, mix.x + mix.y + mix.z);
            for (int i = 0; i < 3; i++)
            {
                float v = (i == 0 ? mix.x : i == 1 ? mix.y : mix.z);
                float h = Mathf.Clamp01(v / Mathf.Max(3f, sum)) * 80f;
                var rt = _mixBars[i].rectTransform;
                rt.sizeDelta = new Vector2(60f, Mathf.Lerp(rt.sizeDelta.y, h, dt * 12f));
            }

            _score.text = score.ToString();
            // Show the real multiplier (capped at x3), not the raw streak count.
            float mult = 1f + Mathf.Min(combo - 1, 8) * 0.25f;
            _combo.text = combo >= 2 ? "x" + mult.ToString("0.##") + "  -  " + combo + " STREAK" : "";
            _served.text = served + (served == 1 ? " SERVED" : " SERVED");

            int m = Mathf.Max(0, Mathf.FloorToInt(timeLeft / 60f));
            int s = Mathf.Max(0, Mathf.FloorToInt(timeLeft % 60f));
            _timer.text = m + ":" + s.ToString("00");
            _timer.color = timeLeft <= 15f ? Color.Lerp(Palette.Danger, Palette.Cream, Ease.Pulse(_phase, 3f)) : Palette.Cream;

            _scorePunch = Mathf.Max(0f, _scorePunch - dt * 3.2f);
            _score.transform.parent.localScale = Vector3.one * (1f + Ease.OutCubic(_scorePunch) * 0.22f);

            _flashT = Mathf.Max(0f, _flashT - dt * 3.4f);
            _flash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flashT * 0.55f);

            for (int i = 0; i < _pops.Length; i++)
            {
                if (_popT[i] <= 0f) continue;
                _popT[i] -= dt * 0.85f;
                float u = 1f - Mathf.Clamp01(_popT[i]);
                var rt = (RectTransform)_pops[i].transform.parent;
                rt.anchoredPosition = _popHome[i] + new Vector2(0f, Ease.OutCubic(u) * 150f);
                rt.localScale = Vector3.one * (0.7f + Ease.OutBack(Mathf.Clamp01(u * 3f)) * 0.45f);
                var c = _pops[i].color; c.a = Mathf.Clamp01(_popT[i] * 1.6f);
                _pops[i].color = c;
                if (_popT[i] <= 0f) rt.gameObject.SetActive(false);
            }
        }
    }
}
