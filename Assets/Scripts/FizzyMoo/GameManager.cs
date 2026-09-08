using UnityEngine;

namespace FizzyMoo
{
    public enum Phase { Title, Playing, GameOver }

    /// <summary>
    /// Round rules: timer, scoring, streaks, difficulty ramp, input routing - and
    /// the guidance layer. Rather than a tutorial screen, the game always states
    /// the one next action in plain words ("EAT 2 MORE LIMES", "TANK FULL - GO TO
    /// THE STAND", "HOLD SPACE - RELEASE ON THE LINE"), lights up the fruit it is
    /// talking about, and floats a beacon toward the stand when it is time to pour.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const float RoundTime = 120f;

        public Phase Phase { get; private set; } = Phase.Title;
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int Served { get; private set; }
        public int Perfect { get; private set; }
        public int Blowouts { get; private set; }
        public float TimeLeft { get; private set; } = RoundTime;

        public CowController Cow;
        public SodaStand Stand;
        public HUD Hud;
        public CameraRig Cam;
        public AutoPilot Auto;      // null unless demo mode is on

        Transform _beacon;
        Material _beaconMat;
        Transform[] _path = new Transform[8];   // marching dots on the grass, cow -> stand
        float _orderAge;            // seconds since the current order appeared
        float _teachT;              // seconds left on the post-blowout lesson
        bool _hurry;

        void Start()
        {
            Stand.Bind(Cow);
            Stand.OnServed += OnServed;
            Stand.OnTimeout += OnTimeout;
            Cow.OnBlowout += OnBlowout;
            Hud.ShowTitle(true);
            Hud.ShowGameOver(false);
            BuildBeacon();
        }

        /// <summary>A gold arrow that hovers over Bessie and points at the stand when the tank is ready.</summary>
        void BuildBeacon()
        {
            _beaconMat = Mk.Mat(Palette.Gold, 0.5f, 0f, Palette.Gold * 1.2f);
            // A flat chevron ">" lying almost horizontal, so it reads as an arrowhead from
            // the chase camera (a shafted 3D arrow read as a yellow slab from above).
            _beacon = Mk.Empty("Beacon", null).transform;
            var l = Mk.Prim(PrimitiveType.Cube, _beacon, new Vector3(-0.42f, 0f, -0.30f), new Vector3(0.30f, 0.12f, 1.35f), _beaconMat, "HeadL", outline: true);
            var r = Mk.Prim(PrimitiveType.Cube, _beacon, new Vector3( 0.42f, 0f, -0.30f), new Vector3(0.30f, 0.12f, 1.35f), _beaconMat, "HeadR", outline: true);
            l.transform.localRotation = Quaternion.Euler(0f,  36f, 0f);
            r.transform.localRotation = Quaternion.Euler(0f, -36f, 0f);
            _beacon.gameObject.SetActive(false);
            for (int i = 0; i < _path.Length; i++)
            {
                _path[i] = Mk.Prim(PrimitiveType.Cylinder, null, Vector3.zero, new Vector3(0.7f, 0.02f, 0.7f), _beaconMat, "PathDot").transform;
                _path[i].gameObject.SetActive(false);
            }
        }

        void OnServed(int points, float quality, bool perfect)
        {
            if (Phase != Phase.Playing) return;

            if (quality > 0.5f)
            {
                Combo++;
                float mult = 1f + Mathf.Min(Combo - 1, 8) * 0.25f;
                int gained = Mathf.RoundToInt(points * mult);
                Score += gained;
                Served++;
                if (perfect) Perfect++;
                Hud.PunchScore();
                Hud.Popup((perfect ? "PERFECT POUR!  +" : "+") + gained, perfect ? Palette.Gold : Palette.Cream);
                if (perfect) { Hud.Flash(Palette.Gold, 0.8f); Cam.Shake(0.25f); }
            }
            else
            {
                Combo = 0;
                Score += Mathf.RoundToInt(points * 0.3f);
                Hud.Popup(quality > 0.2f ? "SLOPPY - WRONG FRUIT?" : "SPILLED!", Palette.Danger);
                Cam.Shake(0.2f);
            }
            NextOrder();
        }

        void OnTimeout()
        {
            if (Phase != Phase.Playing) return;
            Combo = 0;
            Hud.Popup("TOO SLOW - THEY LEFT", Palette.Danger);
            NextOrder();
        }

        void OnBlowout()
        {
            if (Phase != Phase.Playing) return;
            Blowouts++;
            Combo = 0;
            Hud.Popup("BLOWOUT!  100 PSI", Palette.Danger);
            Hud.Flash(Palette.Danger, 1f);
            Cam.Shake(0.9f);
            _teachT = 5f;
        }

        void NextOrder()
        {
            // Difficulty scales with how many have been served, not with the clock,
            // so a struggling player is not punished twice.
            Invoke(nameof(SpawnOrder), 1.15f);
        }

        void SpawnOrder()
        {
            if (Phase != Phase.Playing) return;
            Stand.NextCustomer(Served);
            _orderAge = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // --- input --------------------------------------------------------
            Vector2 move = Vector2.zero;
            bool vent = false, start = false;

            if (Auto != null && Auto.Enabled)
            {
                Auto.Think(dt, out move, out vent, out start);
            }
            else
            {
                move = new Vector2(
                    (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) -
                    (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)  ? 1f : 0f),
                    (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)    ? 1f : 0f) -
                    (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)  ? 1f : 0f));
                vent = Input.GetKey(KeyCode.Space);
                start = Input.GetKeyDown(KeyCode.Space);
            }

            // Live-demo hotkeys: B = "watch the udder" (jump to 92 PSI), P = toggle the autopilot.
            if (Input.GetKeyDown(KeyCode.B) && Phase == Phase.Playing) Cow.Bloat(92f);
            if (Input.GetKeyDown(KeyCode.P)) ToggleAutopilot();

            switch (Phase)
            {
                case Phase.Title:
                    Cow.SetInput(Vector2.zero, false);
                    if (start) Begin();
                    break;

                case Phase.Playing:
                    Cow.SetInput(move, vent);
                    TimeLeft -= dt;
                    _orderAge += dt;
                    if (_teachT > 0f) _teachT -= dt;
                    if (TimeLeft <= 0f) { TimeLeft = 0f; End(); }
                    break;

                case Phase.GameOver:
                    Cow.SetInput(Vector2.zero, false);
                    if (Input.GetKeyDown(KeyCode.R) || (Auto != null && Auto.Enabled && start)) Restart();
                    break;
            }

            // --- hud + guidance -------------------------------------------------
            Hud.Tick(Cow.PressureNorm, Cow.FlavorMix, Score, Combo, TimeLeft, Served, dt);

            var cust = Stand.Customer;
            bool order = Phase == Phase.Playing && cust != null && cust.Active;
            Fruit.Wanted = order ? cust.Want : (Flavor?)null;

            // Last six seconds of patience: the order line turns red and says so, once with a tick.
            bool hurry = order && cust.PatienceLeft < 6f && !Stand.Pouring;
            if (hurry && !_hurry) Sfx.I?.Play(ProcAudio.Tick, 0.7f);
            _hurry = hurry;
            if (order)
                Hud.SetOrder((hurry ? "HURRY!   " : "ORDER   ") + Palette.Name(cust.Want) + "   " + Mathf.RoundToInt(cust.WantFill * 100f) + "%",
                             hurry ? Palette.Danger : Palette.Of(cust.Want));
            else Hud.SetOrder("", Palette.Cream);

            Hud.SetGlass(order && Stand.CowInZone && Cow.State != CowState.Launched,
                         Stand.Live.Fill, order ? cust.WantFill : 0f, order ? Palette.Of(cust.Want) : Palette.Cream);

            Hud.SetNeed(order, order ? cust.WantFill * SodaStand.BottleCapacity / CowController.MaxPressure : 0f);
            Cow.DominantFlavor(out var tankDom, out var tankPurity);
            Hud.SetTank(Cow.FruitEaten > 0 ? Palette.Short(tankDom) + "  " + Mathf.RoundToInt(tankPurity * 100f) + "%" : "EMPTY",
                        Cow.FruitEaten > 0 ? Palette.Of(tankDom) : Palette.Cream);

            bool showBeacon = false;
            if (Phase == Phase.Playing)
            {
                string h = Directive(cust, order, out showBeacon);
                if (hurry && Cow.State != CowState.Launched && _teachT <= 0f) h = "HURRY  -  " + h;
                Hud.SetHint(h);
            }
            else Hud.SetHint("");
            UpdateBeacon(showBeacon, dt);
        }

        /// <summary>The single next action, in words. This is the whole tutorial.</summary>
        string Directive(Customer cust, bool order, out bool beacon)
        {
            beacon = false;
            if (Cow.State == CowState.Launched) return "OOPS.  SHE'LL BE FINE.";
            if (_teachT > 0f) return "BLOWOUT!  FRUIT KEEPS FERMENTING  -  POUR BEFORE 100 PSI";
            if (Stand.Pouring) return "RELEASE ON THE LINE!";
            if (Cow.PressureNorm > 0.85f)
                return Stand.CowInZone ? "SHE'S GONNA BLOW  -  HOLD SPACE, POUR NOW!" : "SHE'S GONNA BLOW  -  HOLD SPACE TO VENT!";
            if (!order) return "NEXT CUSTOMER INCOMING...";

            float need = cust.WantFill * SodaStand.BottleCapacity;
            float have = Cow.Pressure;
            Cow.DominantFlavor(out var dom, out var purity);
            bool tankHasWrong = Cow.FruitEaten > 0 && (dom != cust.Want || purity < 0.6f);
            string fruit = cust.Want == Flavor.KeyLime ? "LIME" : cust.Want == Flavor.OrangeCream ? "ORANGE" : "PINEAPPLE";

            if (Stand.CowInZone)
            {
                if (tankHasWrong) return "WRONG FRUIT IN THE TANK  -  STEP OUT OF THE RING, HOLD SPACE TO DUMP IT";
                if (have < need - 1f) return "NOT ENOUGH PRESSURE  -  EAT MORE " + fruit + "S";
                return "HOLD  SPACE  TO POUR  -  RELEASE ON THE LINE";
            }
            if (tankHasWrong && have > need * 0.5f)
                return "WRONG FRUIT IN THE TANK  -  HOLD SPACE OUT HERE UNTIL IT'S EMPTY";

            int more = Mathf.CeilToInt(Mathf.Max(0f, need - have) / 13f);
            if (more > 0)
            {
                if (_orderAge < 3.5f && Served == 0)
                    return "THEY WANT " + Palette.Name(cust.Want) + "  -  EAT THE GLOWING " + fruit + "S";
                return "EAT " + more + " MORE " + fruit + (more == 1 ? "" : "S") + "  (" + Mathf.RoundToInt(have) + "/" + Mathf.RoundToInt(need) + " PSI)";
            }
            beacon = true;
            return "TANK FULL  -  FOLLOW THE DOTS TO THE STAND";
        }

        void UpdateBeacon(bool on, float dt)
        {
            if (_beacon == null) return;
            var flat = Stand.transform.position - Cow.transform.position; flat.y = 0f;
            on = on && flat.magnitude > 4.5f;
            if (_beacon.gameObject.activeSelf != on) _beacon.gameObject.SetActive(on);
            foreach (var d in _path) if (d.gameObject.activeSelf != on) d.gameObject.SetActive(on);
            if (!on) return;
            float bob = Mathf.Sin(Time.time * 4f) * 0.15f;
            _beacon.position = Cow.transform.position + Vector3.up * (3.6f + bob) + flat.normalized * 0.8f;
            // dotted path on the grass, marching toward the pour zone
            var a = Cow.transform.position; a.y = 0.03f;
            var b = Stand.transform.position - flat.normalized * 2.8f; b.y = 0.03f;
            float ph = (Time.time * 0.9f) % 1f;
            for (int i = 0; i < _path.Length; i++)
            {
                float t = (i + ph) / _path.Length;
                _path[i].position = Vector3.Lerp(a, b, t);
                float s = 0.55f + 0.35f * Mathf.Sin(t * Mathf.PI);
                _path[i].localScale = new Vector3(s, 0.02f, s);
            }
            _beacon.rotation = Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(28f, 0f, 0f);
            _beaconMat.SetColor("_EmissionColor", Palette.Gold * (0.9f + Ease.Pulse(Time.time, 2f) * 0.9f));
        }

        void ToggleAutopilot()
        {
            if (Auto == null)
            {
                Auto = gameObject.AddComponent<AutoPilot>();
                Auto.Bind(Cow, Stand, FindObjectsByType<Fruit>());
                Auto.Enabled = true;
                Hud.Popup("AUTOPILOT ON", Palette.Gold);
            }
            else
            {
                Auto.Enabled = !Auto.Enabled;
                Hud.Popup(Auto.Enabled ? "AUTOPILOT ON" : "AUTOPILOT OFF", Palette.Gold);
            }
        }

        void Begin()
        {
            Phase = Phase.Playing;
            Score = 0; Combo = 0; Served = 0; Perfect = 0; Blowouts = 0;
            TimeLeft = RoundTime;
            Cow.ResetAll();
            Stand.ResetStand();
            Hud.ShowTitle(false);
            Hud.ShowGameOver(false);
            Sfx.I?.Play(ProcAudio.Moo, 0.8f);
            Stand.NextCustomer(0);
            _orderAge = 0f; _teachT = 0f; _hurry = false;
        }

        void End()
        {
            Phase = Phase.GameOver;
            Stand.ResetStand();
            // She keeps fermenting otherwise and blows out under the results card.
            Cow.Calm();
            Fruit.Wanted = null;
            Combo = 0;
            string grade = Score >= 2200 ? "MASTER BREWER" : Score >= 1200 ? "HEAD OF DAIRY"
                         : Score >= 600 ? "APPRENTICE"   : "INTERN";
            string blow = Blowouts == 0 ? "Zero blowouts. Bessie is suspicious of you."
                        : Blowouts == 1 ? "1 blowout. She's fine. Probably."
                        : $"{Blowouts} blowouts. Bessie has filed a complaint.";
            Hud.ShowGameOver(true, "TIME!",
                $"{Score} POINTS   -   {grade}\n\n" +
                $"{Served} served     {Perfect} perfect\n{blow}\n\n" +
                "Press  R  to run it back\n\n" + Brand.Tagline);
            Sfx.I?.Play(ProcAudio.Moo, 0.9f, 0.85f);
        }

        void Restart() => Begin();
    }
}
