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
                // New order? (counted by the stand's sequence number, not by patience jumps)
                if (_stand.OrderSeq != _lastSeq)
                {
                    _lastSeq = _stand.OrderSeq; _ordersSeen++;
                    _showboat = _ordersSeen == 2 || _ordersSeen == 12;   // blowouts on the 3rd and 13th orders
                    _reckless = _ordersSeen == 6;                         // one sloppy pour on the 7th
                }

                if (_mode == Mode.Wait || _mode == Mode.Bleed)
                    _mode = p < need ? Mode.Gather : Mode.Approach;

                // Safety: never sit on a hair trigger far from the stand.
                if (p > 86f && _mode == Mode.Gather && !_showboat) _mode = Mode.Approach;
                if (_cow.State == CowState.Launched) _showboat = false;
                if (_mode == Mode.Gather && p >= need && !_showboat) _mode = Mode.Approach;
                // Lost the charge on the way (blowout, bleed)? Go back for more before walking up.
                if (_mode == Mode.Approach && p < need - 4f && standDist > 2.5f && !_showboat) _mode = Mode.Gather;
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
                    // Never pour flavourless cream: if the tank was dumped, go and eat first.
                    if (_cow.FruitEaten == 0 && !_stand.Pouring) { _mode = Mode.Gather; break; }
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

        int _lastSeq = -1;

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
            if (d.magnitude < 2.0f) return dir;      // commit: no dodging in the last two metres

            Vector2 avoid = Vector2.zero;
            const float R = 3.0f;
            if (!_reckless) foreach (var b in _fruit)
            {
                if (b == null || !b.Available) continue;
                if (edible.HasValue && b.Flavor == edible.Value) continue;   // safe to hit
                var o = b.transform.position - from; o.y = 0f;
                float dist = o.magnitude;
                if (dist > R || dist < 0.01f) continue;
                if (Vector2.Dot(new Vector2(o.x, o.z) / dist, dir) < 0f) continue;   // behind us - irrelevant
                avoid -= new Vector2(o.x, o.z).normalized * ((1f - dist / R) * 1.7f);
            }
            var res = dir + avoid;
            if (res.magnitude < 0.3f) return dir;      // pinned between fruit: just go
            return Vector2.ClampMagnitude(res, 1f);
        }

        /// <summary>True if the straight walk from a to b passes through the stand's footprint.</summary>
        bool PathCrossesStand(Vector3 a, Vector3 b)
        {
            var c = _stand.transform.position; a.y = b.y = c.y = 0f;
            var ab = b - a; float len = ab.magnitude; if (len < 0.01f) return false;
            float t = Mathf.Clamp01(Vector3.Dot(c - a, ab) / (len * len));
            return (a + ab * t - c).magnitude < 2.6f;
        }

        Fruit PickFruit(Vector3 me, Flavor want)
        {
            Fruit best = null; float bestD = float.MaxValue;
            foreach (var b in _fruit)
            {
                if (b == null || !b.Available || b.Flavor != want) continue;
                if (PathCrossesStand(me, b.transform.position)) continue;   // the counter is solid
                float d = (b.transform.position - me).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }
    }
}
