using UnityEngine;

namespace FizzyMoo
{
    public enum CowState { Grazing, Venting, Launched }

    /// <summary>
    /// Bessie's simulation: locomotion, the carbonation model, and the blowout.
    ///
    /// The carbonation model is the heart of the game. Eating a fizz-berry adds an
    /// immediate pressure spike AND raises the ongoing fermentation rate, so pressure
    /// keeps climbing on its own afterwards. That converts a simple "collect items"
    /// loop into a risk/reward timing game: more fruit means a richer, more valuable
    /// flavour but a shorter fuse before she blows.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CowController : MonoBehaviour
    {
        // --- tuning ---------------------------------------------------------
        public const float MaxPressure   = 100f;
        const float MoveAccel            = 46f;
        const float MaxSpeed             = 6.4f;
        const float TurnLerp             = 15f;
        const float PressurePerBerry     = 13f;
        const float FermentPerBerry      = 1.05f;   // added to the per-second climb
        const float FermentDecay         = 0.16f;   // fermentation calms down slowly
        const float VentRate             = 34f;     // pressure released per second
        const float StunTime             = 2.6f;
        const float SpeedPressurePenalty = 0.34f;   // full udder = slower cow

        // --- state ----------------------------------------------------------
        public CowState State { get; private set; } = CowState.Grazing;
        public float Pressure { get; private set; }
        public Vector3 FlavorMix { get; private set; }      // (cola, berry, lime) accumulator
        public int FruitEaten { get; private set; }
        public float PressureNorm => Mathf.Clamp01(Pressure / MaxPressure);
        public bool CanAct => State != CowState.Launched;

        float _ferment, _stunT;
        Rigidbody _rb;
        CowRig _rig;
        ParticleSystem _fizzJet, _burst, _warnPuff;
        Vector2 _input;

        /// <summary>Set by the stand; the vent jet arcs toward it when she is in range.</summary>
        public Transform PourTarget;
        float _lastYaw;

        public System.Action OnBlowout;
        public System.Action<Flavor> OnAte;

        /// <summary>Dominant flavour, and how pure the mix is (1 = single flavour).</summary>
        public void DominantFlavor(out Flavor flavor, out float purity)
        {
            var m = FlavorMix;
            float sum = m.x + m.y + m.z;
            if (sum < 0.0001f) { flavor = Flavor.KeyLime; purity = 0f; return; }
            if (m.x >= m.y && m.x >= m.z) { flavor = Flavor.KeyLime;  purity = m.x / sum; }
            else if (m.y >= m.z)          { flavor = Flavor.OrangeCream; purity = m.y / sum; }
            else                          { flavor = Flavor.PinaColada;  purity = m.z / sum; }
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 4f;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.linearDamping = 3.2f;
            _rig = CowRig.Build(transform);
            BuildFx();
        }

        void BuildFx()
        {
            // Jet of fizz that sprays from under the udder while venting.
            _fizzJet = MakePs("FizzJet", new Vector3(0f, 0.34f, -0.10f), Quaternion.Euler(90f, 0f, 0f));
            var m = _fizzJet.main;
            m.startSpeed = new ParticleSystem.MinMaxCurve(3.2f, 6.5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.17f);
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            m.gravityModifier = 0.9f;
            var sh = _fizzJet.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 17f; sh.radius = 0.10f;
            var em = _fizzJet.emission; em.rateOverTime = 0f;

            // Warning wisps that hiss out as she nears the limit.
            _warnPuff = MakePs("WarnPuff", new Vector3(0f, 1.30f, 0.45f), Quaternion.Euler(-70f, 0f, 0f));
            var wm = _warnPuff.main;
            wm.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
            wm.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.26f);
            wm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            wm.startColor = new Color(1f, 1f, 1f, 0.5f);
            wm.gravityModifier = -0.15f;
            var wem = _warnPuff.emission; wem.rateOverTime = 0f;

            // One-shot explosion of foam on a blowout.
            _burst = MakePs("Burst", new Vector3(0f, 0.5f, 0f), Quaternion.identity);
            var bm = _burst.main;
            bm.startSpeed = new ParticleSystem.MinMaxCurve(6f, 15f);
            bm.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.34f);
            bm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            bm.gravityModifier = 1.1f;
            var bsh = _burst.shape; bsh.shapeType = ParticleSystemShapeType.Sphere; bsh.radius = 0.35f;
            var bem = _burst.emission; bem.rateOverTime = 0f;
        }

        ParticleSystem MakePs(string name, Vector3 pos, Quaternion rot)
        {
            var go = Mk.Empty(name, transform, pos);
            go.transform.localRotation = rot;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = Mk.ParticleMat(Color.white);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            Mk.FadeOut(ps);
            ps.Stop();
            return ps;
        }

        // --- input ------------------------------------------------------------
        public void SetInput(Vector2 move, bool vent)
        {
            _input = Vector2.ClampMagnitude(move, 1f);
            _wantVent = vent;
        }
        bool _wantVent;

        void Update()
        {
            float dt = Time.deltaTime;

            if (State == CowState.Launched)
            {
                _stunT -= dt;
                // Once she is falling and close to the ground, right her so she lands
                // on her feet. Tumbling is funny; lying on her side reads as a bug.
                if (_stunT < StunTime - 0.9f && transform.position.y < 2.4f)
                {
                    var upright = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, upright, 1f - Mathf.Exp(-7f * dt));
                    _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, Vector3.zero, 1f - Mathf.Exp(-6f * dt));
                }
                if (_stunT <= 0f) Recover();
                _rig.Tick(0f, dt);
                return;
            }

            // Fermentation: the slow burn that forces the player to keep moving.
            _ferment = Mathf.Max(0f, _ferment - FermentDecay * dt);
            Pressure += _ferment * dt;

            // Venting: only meaningful when the stand accepts the pour, but the
            // cow always physically releases pressure so the player can bleed off
            // a dangerous charge anywhere on the map (at the cost of wasting it).
            bool venting = _wantVent && Pressure > 0.5f;
            State = venting ? CowState.Venting : CowState.Grazing;
            if (venting)
            {
                float rate = VentRate * dt;
                float used = Mathf.Min(Pressure, rate);
                Pressure -= used;
                LastVentAmount = used;
            }
            else LastVentAmount = 0f;

            if (Pressure >= MaxPressure) { Blowout(); return; }

            DriveFx(venting, dt);
            _rig.PressureNorm = PressureNorm;
            _rig.FlavorMix = FlavorMix;

            var v = _rb.linearVelocity; v.y = 0f;

            // Lean into turns: yaw rate -> body roll on the rig.
            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(_lastYaw, yaw) / Mathf.Max(dt, 0.0001f);
            _lastYaw = yaw;
            _rig.Lean = Mathf.Lerp(_rig.Lean, Mathf.Clamp(yawRate / 260f, -1f, 1f), 1f - Mathf.Exp(-9f * dt));

            _rig.Tick(Mathf.Clamp01(v.magnitude / MaxSpeed), dt);
        }

        /// <summary>Pressure released this frame - the stand converts this into bottle fill.</summary>
        public float LastVentAmount { get; private set; }

        void DriveFx(bool venting, float dt)
        {
            var col = Palette.Mix(FlavorMix);

            var jm = _fizzJet.main; jm.startColor = col;
            var jem = _fizzJet.emission; jem.rateOverTime = venting ? 260f : 0f;

            // Arc the stream into the glass when it is within reach; otherwise it
            // just sprays at the ground. Sells the pour far better than a jet that
            // fires straight down three metres from the thing it is filling.
            var jt = _fizzJet.transform;
            bool aimed = PourTarget != null && (PourTarget.position - jt.position).magnitude < 6.5f;
            if (aimed)
            {
                var to = PourTarget.position - jt.position;
                jt.rotation = Quaternion.LookRotation((to.normalized + Vector3.up * 0.55f).normalized);
                jm.startSpeed = new ParticleSystem.MinMaxCurve(to.magnitude * 1.9f, to.magnitude * 2.3f);
            }
            else
            {
                jt.localRotation = Quaternion.Euler(90f, 0f, 0f);
                jm.startSpeed = new ParticleSystem.MinMaxCurve(3.2f, 6.5f);
            }
            if (venting && !_fizzJet.isPlaying) _fizzJet.Play();
            if (!venting && _fizzJet.isPlaying) _fizzJet.Stop();

            float warn = Mathf.Clamp01((PressureNorm - 0.62f) / 0.38f);
            var wem = _warnPuff.emission; wem.rateOverTime = warn * 42f;
            if (warn > 0f && !_warnPuff.isPlaying) _warnPuff.Play();
            if (warn <= 0f && _warnPuff.isPlaying) _warnPuff.Stop();

            if (Sfx.I != null)
                Sfx.I.SetFizz(venting ? 1f : warn * 0.45f, venting ? 1.0f : Mathf.Lerp(1.4f, 2.0f, warn));
        }

        void FixedUpdate()
        {
            if (State == CowState.Launched) return;

            // Heavier udder = slower cow. Another lever pushing the player to pour.
            float speedMul = 1f - PressureNorm * SpeedPressurePenalty;
            var wish = new Vector3(_input.x, 0f, _input.y);
            _rb.AddForce(wish * (MoveAccel * speedMul), ForceMode.Acceleration);

            var flat = _rb.linearVelocity; flat.y = 0f;
            float cap = MaxSpeed * speedMul;
            if (flat.magnitude > cap)
            {
                flat = flat.normalized * cap;
                _rb.linearVelocity = new Vector3(flat.x, _rb.linearVelocity.y, flat.z);
            }
            if (flat.sqrMagnitude > 0.06f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(flat.normalized), 1f - Mathf.Exp(-TurnLerp * Time.fixedDeltaTime));
        }

        // --- events -------------------------------------------------------------
        public void Eat(Flavor f)
        {
            if (!CanAct) return;
            Pressure = Mathf.Min(MaxPressure, Pressure + PressurePerBerry);
            _ferment += FermentPerBerry;
            FruitEaten++;
            var m = FlavorMix;
            if (f == Flavor.KeyLime) m.x += 1f; else if (f == Flavor.OrangeCream) m.y += 1f; else m.z += 1f;
            FlavorMix = m;
            _rig.Wobble(5.5f);
            _rig.Chomp();
            Sfx.I?.Play(ProcAudio.Chomp, 0.7f, Random.Range(0.9f, 1.15f));
            OnAte?.Invoke(f);
        }

        /// <summary>Called by the stand once a bottle is served - the udder empties out.</summary>
        public void ClearFlavor()
        {
            FlavorMix = Vector3.zero;
            FruitEaten = 0;
        }

        void Blowout()
        {
            State = CowState.Launched;
            _stunT = StunTime;
            Pressure = 0f;
            _ferment = 0f;
            FlavorMix = Vector3.zero;

            _rb.constraints = RigidbodyConstraints.None;
            _rb.AddForce(new Vector3(Random.Range(-3f, 3f), 17f, Random.Range(-3f, 3f)), ForceMode.VelocityChange);
            _rb.AddTorque(Random.insideUnitSphere * 26f, ForceMode.VelocityChange);

            _burst.Emit(190);
            _fizzJet.Stop(); _warnPuff.Stop();
            _rig.Wobble(14f);
            Sfx.I?.Play(ProcAudio.Launch, 0.85f);
            Sfx.I?.Play(ProcAudio.Moo, 0.9f, 1.25f);
            OnBlowout?.Invoke();
        }

        void Recover()
        {
            State = CowState.Grazing;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            var p = transform.position;
            transform.position = new Vector3(Mathf.Clamp(p.x, -22f, 22f), 1.2f, Mathf.Clamp(p.z, -22f, 22f));
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rig.Wobble(6f);
        }

        public void ResetAll()
        {
            Pressure = 0f; _ferment = 0f; FlavorMix = Vector3.zero; FruitEaten = 0;
            State = CowState.Grazing;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(new Vector3(0f, 1.2f, -4f), Quaternion.identity);
        }
    }
}
