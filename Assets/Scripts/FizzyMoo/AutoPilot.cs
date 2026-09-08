using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// A scripted player used to record the trailer (and as an attract mode).
    /// It plans the same way a good human does: read the order, work out how much
    /// pressure that bottle needs, eat exactly enough fruit of the right flavour,
    /// walk in, and release on the line. It deliberately fumbles one early order so
    /// the demo shows off the blowout without me having to fake it in the edit.
    /// </summary>
    public class AutoPilot : MonoBehaviour
    {
        public bool Enabled;

        enum Mode { Wait, Gather, Approach, Pour, Bleed }
        Mode _mode = Mode.Wait;

        CowController _cow;
        SodaStand _stand;
        Fruit[] _fruit;
        Fruit _target;
        float _t, _startT, _jitterT;
        Vector2 _jitter;
        bool _started;
        int _ordersSeen = -1;
        bool _showboat;            // this order is the intentional blowout
        bool _reckless;            // this order ignores wrong fruit on the way -> a SLOPPY pour

        public void Bind(CowController cow, SodaStand stand, Fruit[] fruit)
        {
            _cow = cow; _stand = stand; _fruit = fruit;
        }

        public void Think(float dt, out Vector2 move, out bool vent, out bool start)
        {
            move = Vector2.zero; vent = false; start = false;
            _t += dt;

            // Let the title card breathe for a beat, then hit start.
            if (!_started)
            {
                if (_t > 2.2f) { start = true; _started = true; _startT = _t; }
                return;
            }

            var cust = _stand.Customer;
            bool haveOrder = cust != null && cust.Active;
            if (!haveOrder) { _mode = _cow.Pressure > 4f ? Mode.Bleed : Mode.Wait; }

            Vector3 me = _cow.transform.position;
            Vector3 standPos = _stand.transform.position;
            float standDist = Vector3.Distance(new Vector3(me.x, 0f, me.z), new Vector3(standPos.x, 0f, standPos.z));

            float need = haveOrder ? cust.WantFill * SodaStand.BottleCapacity : 0f;
            float p = _cow.Pressure;

            if (haveOrder)
            {
                // New order? decide whether to showboat.
                int id = Mathf.RoundToInt(cust.PatienceLeft * 1000f);
                if (_lastPatience < cust.PatienceLeft) { _ordersSeen++; _showboat = _ordersSeen == 1 || _ordersSeen == 12; _reckless = _ordersSeen == 6; }
                _lastPatience = cust.PatienceLeft;

                if (_mode == Mode.Wait || _mode == Mode.Bleed)
                    _mode = p < need ? Mode.Gather : Mode.Approach;

                // Safety: never sit on a hair trigger far from the stand.
                if (p > 86f && _mode == Mode.Gather && !_showboat) _mode = Mode.Approach;
                if (_mode == Mode.Gather && p >= need) _mode = Mode.Approach;
                if (_mode == Mode.Approach && standDist < SodaStand.ServeRadius - 0.4f) _mode = Mode.Pour;
            }

            switch (_mode)
            {
                case Mode.Wait:
                    // amble near the stand
                    move = Steer(me, standPos + new Vector3(Mathf.Sin(_t * 0.7f) * 4.5f, 0f, -5.5f));
                    break;

                case Mode.Gather:
                {
                    // On the showboat order, keep eating well past what the bottle needs.
                    float goal = _showboat ? 99f : need;
                    if (_target == null || !_target.Available || _target.Flavor != cust.Want)
                        _target = PickFruit(me, cust.Want);
                    if (_target != null)
                        move = Steer(me, _target.transform.position, cust.Want);
                    else
                        move = Steer(me, standPos + new Vector3(0f, 0f, -6f), cust.Want);
                    if (p >= goal) _mode = Mode.Approach;
                    break;
                }

                case Mode.Approach:
                    move = Steer(me, standPos + new Vector3(0f, 0f, -SodaStand.ServeRadius * 0.45f));
                    break;

                case Mode.Pour:
                {
                    move = Vector2.zero;
                    float fill = _stand.Live.Fill;
                    // Release a hair before the line: one frame of venting is ~0.011 fill.
                    float stopAt = cust.WantFill - 0.006f;
                    vent = fill < stopAt;
                    if (!vent) _mode = _cow.Pressure > 3f ? Mode.Bleed : Mode.Wait;
                    break;
                }

                case Mode.Bleed:
                    // Dump leftover charge well away from the tap so it is not wasted into a bottle.
                    move = Steer(me, standPos + new Vector3(6.5f, 0f, -9f));
                    vent = standDist > SodaStand.ServeRadius + 1.6f && _cow.Pressure > 2f;
                    if (_cow.Pressure <= 2f) _mode = Mode.Wait;
                    break;
            }

            // A little wander noise so the path does not look robotic on camera.
            _jitterT -= dt;
            if (_jitterT <= 0f) { _jitterT = Random.Range(0.4f, 1.0f); _jitter = Random.insideUnitCircle * 0.16f; }
            if (move.sqrMagnitude > 0.01f) move = Vector2.ClampMagnitude(move + _jitter, 1f);
        }

        float _lastPatience = -1f;

        /// <summary>
        /// Steer toward a point while pushing away from fruit we must not eat.
        /// The cow eats anything she touches, so the route matters as much as the
        /// destination - a straight line to the right berry often runs you through
        /// three wrong ones and blows the whole bottle.
        /// </summary>
        Vector2 Steer(Vector3 from, Vector3 to, Flavor? edible = null)
        {
            var d = to - from; d.y = 0f;
            Vector2 dir = d.sqrMagnitude < 0.16f ? Vector2.zero : new Vector2(d.x, d.z).normalized;

            Vector2 avoid = Vector2.zero;
            const float R = 3.0f;
            if (!_reckless) foreach (var b in _fruit)
            {
                if (b == null || !b.Available) continue;
                if (edible.HasValue && b.Flavor == edible.Value) continue;   // safe to hit
                var o = b.transform.position - from; o.y = 0f;
                float dist = o.magnitude;
                if (dist > R || dist < 0.01f) continue;
                avoid -= new Vector2(o.x, o.z).normalized * ((1f - dist / R) * 1.7f);
            }
            return Vector2.ClampMagnitude(dir + avoid, 1f);
        }

        Fruit PickFruit(Vector3 me, Flavor want)
        {
            Fruit best = null; float bestD = float.MaxValue;
            foreach (var b in _fruit)
            {
                if (b == null || !b.Available || b.Flavor != want) continue;
                float d = (b.transform.position - me).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }
    }
}
