using UnityEngine;

namespace FizzyMoo
{
    public enum Phase { Title, Playing, GameOver }

    /// <summary>Round rules: timer, scoring, streaks, difficulty ramp, input routing.</summary>
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

        float _startDelay;

        void Start()
        {
            Stand.Bind(Cow);
            Stand.OnServed += OnServed;
            Stand.OnTimeout += OnTimeout;
            Cow.OnBlowout += OnBlowout;
            Hud.ShowTitle(true);
            Hud.ShowGameOver(false);
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
                Hud.Popup(quality > 0.2f ? "SLOPPY" : "SPILLED!", Palette.Danger);
                Cam.Shake(0.2f);
            }
            NextOrder();
        }

        void OnTimeout()
        {
            if (Phase != Phase.Playing) return;
            Combo = 0;
            Hud.Popup("TOO SLOW", Palette.Danger);
            NextOrder();
        }

        void OnBlowout()
        {
            if (Phase != Phase.Playing) return;
            Blowouts++;
            Combo = 0;
            Hud.Popup("BLOWOUT!", Palette.Danger);
            Hud.Flash(Palette.Danger, 1f);
            Cam.Shake(0.9f);
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

            switch (Phase)
            {
                case Phase.Title:
                    Cow.SetInput(Vector2.zero, false);
                    if (start) Begin();
                    break;

                case Phase.Playing:
                    Cow.SetInput(move, vent);
                    TimeLeft -= dt;
                    if (TimeLeft <= 0f) { TimeLeft = 0f; End(); }
                    break;

                case Phase.GameOver:
                    Cow.SetInput(Vector2.zero, false);
                    if (Input.GetKeyDown(KeyCode.R)) Restart();
                    break;
            }

            // --- hud ----------------------------------------------------------
            Hud.Tick(Cow.PressureNorm, Cow.FlavorMix, Score, Combo, TimeLeft, Served, dt);

            if (Phase == Phase.Playing)
            {
                if (Cow.State == CowState.Launched) Hud.SetHint("");
                else if (Cow.PressureNorm > 0.85f)  Hud.SetHint("SHE'S GONNA BLOW - VENT NOW!");
                else if (Stand.CowInZone)           Hud.SetHint("HOLD  SPACE  TO POUR - MATCH THE LINE");
                else if (Cow.PressureNorm < 0.12f)  Hud.SetHint("EAT FRUIT TO BUILD PRESSURE");
                else                                Hud.SetHint("GET TO THE STAND");
            }
            else Hud.SetHint("");
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
        }

        void End()
        {
            Phase = Phase.GameOver;
            Stand.ResetStand();
            string grade = Score >= 1400 ? "MASTER BREWER" : Score >= 900 ? "HEAD OF DAIRY"
                         : Score >= 500 ? "APPRENTICE"   : "INTERN";
            Hud.ShowGameOver(true, "TIME!",
                $"{Score} POINTS   -   {grade}\n\n" +
                $"{Served} served     {Perfect} perfect     {Blowouts} blowouts\n\n" +
                "Press  R  to run it back\n\n" + Brand.Tagline);
            Sfx.I?.Play(ProcAudio.Moo, 0.9f, 0.85f);
        }

        void Restart() => Begin();
    }
}
