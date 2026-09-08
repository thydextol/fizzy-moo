using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// Chase camera. Beyond plain smoothing it does two things that carry a lot of
    /// the game's feel: FOV widens as pressure builds (the world literally starts
    /// to bulge), and a trauma-based shake spikes on blowouts and hard events.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform Target;
        public CowController Cow;

        Camera _cam;
        Vector3 _vel;
        float _trauma, _seed;
        const float BaseFov = 58f;

        public static CameraRig Build(Camera cam)
        {
            var r = cam.gameObject.AddComponent<CameraRig>();
            r._cam = cam;
            r._seed = Random.value * 100f;
            return r;
        }

        public void Shake(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        void LateUpdate()
        {
            if (Target == null) return;
            float dt = Time.deltaTime;
            float p = Cow != null ? Cow.PressureNorm : 0f;
            bool airborne = Cow != null && Cow.State == CowState.Launched;

            // A three-quarter offset rather than dead-behind: it shows the cow's
            // spots and the swelling udder instead of just her rear end.
            float dist = airborne ? 8.5f : Mathf.Lerp(6.6f, 7.4f, p);
            float height = airborne ? 5.5f : Mathf.Lerp(3.4f, 3.9f, p);
            float side = airborne ? 3.0f : 2.7f;

            var desired = Target.position + new Vector3(side, height, -dist);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, airborne ? 0.22f : 0.30f);

            var look = Target.position + Vector3.up * (airborne ? 0.6f : 1.35f);
            var rot = Quaternion.LookRotation(look - transform.position);

            // Trauma decays quadratically -> punchy hit, quick settle.
            _trauma = Mathf.Max(0f, _trauma - dt * 1.6f);
            float s = _trauma * _trauma;
            // Constant low-level tremble once she is near the red line.
            float tension = Mathf.Max(0f, p - 0.7f) / 0.3f;
            s = Mathf.Max(s, tension * 0.12f);
            if (s > 0.0001f)
            {
                float t = Time.time * 26f;
                float nx = (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f;
                float ny = (Mathf.PerlinNoise(_seed + 11f, t) - 0.5f) * 2f;
                float nz = (Mathf.PerlinNoise(_seed + 23f, t) - 0.5f) * 2f;
                rot *= Quaternion.Euler(nx * 4.5f * s, ny * 4.5f * s, nz * 3.0f * s);
            }
            transform.rotation = rot;

            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView,
                BaseFov + p * 9f + (airborne ? 8f : 0f) + s * 6f, dt * 6f);
        }
    }
}
