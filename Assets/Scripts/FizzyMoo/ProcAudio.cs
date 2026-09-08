using System;
using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// Every sound in Fizzy Moo is synthesised from scratch into an AudioClip at
    /// load time - the project ships with zero audio files. Keeps the repo tiny
    /// and makes the whole game reproducible from source alone.
    /// </summary>
    public static class ProcAudio
    {
        const int SR = 44100;
        static System.Random _rng = new System.Random(7);

        public static AudioClip Moo, Fizz, Pop, Chime, Buzz, Launch, Chomp, Tick;

        public static void BuildAll()
        {
            if (Moo != null) return;
            Moo    = Build("moo",    1.05f, MooFn);
            Fizz   = Build("fizz",   1.00f, FizzFn, loop: true);
            Pop    = Build("pop",    0.16f, PopFn);
            Chime  = Build("chime",  0.70f, ChimeFn);
            Buzz   = Build("buzz",   0.38f, BuzzFn);
            Launch = Build("launch", 1.30f, LaunchFn);
            Chomp  = Build("chomp",  0.18f, ChompFn);
            Tick   = Build("tick",   0.06f, TickFn);
        }

        static AudioClip Build(string name, float dur, Func<float, float, float> fn, bool loop = false)
        {
            int n = Mathf.CeilToInt(dur * SR);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                data[i] = Mathf.Clamp(fn(t, dur), -1f, 1f);
            }
            // De-click: 4 ms fade at both ends (skipped at the seam for loops).
            int fade = Mathf.Min(SR / 250, n / 2);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] *= k;
                if (!loop) data[n - 1 - i] *= k;
            }
            var clip = AudioClip.Create(name, n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);

        // --- Voices -------------------------------------------------------

        /// <summary>A cow. Sawtooth fundamental that sags in pitch, with vibrato and a nasal formant.</summary>
        static float MooFn(float t, float d)
        {
            float u = t / d;
            float f0 = Mathf.Lerp(138f, 96f, Ease.OutCubic(u));           // pitch sags as breath runs out
            f0 *= 1f + 0.035f * Mathf.Sin(t * 2f * Mathf.PI * 5.5f);      // vibrato
            float ph = t * f0;
            float saw = 2f * (ph - Mathf.Floor(ph + 0.5f));
            float formant = 0.42f * Mathf.Sin(t * 2f * Mathf.PI * f0 * 3.02f);
            float body = 0.75f * saw + formant;
            // Slow attack, long release - reads as a lowing sound rather than a beep.
            float env = Mathf.Min(1f, u / 0.16f) * Mathf.Pow(1f - u, 1.5f);
            return body * env * 0.55f;
        }

        /// <summary>Carbonation. Band-limited noise with a shimmer of bubble bursts on top.</summary>
        static float _fzLp, _fzHp;
        static float FizzFn(float t, float d)
        {
            float n = Noise();
            _fzLp += (n - _fzLp) * 0.35f;              // one-pole lowpass
            _fzHp = _fzHp * 0.86f + (_fzLp - _fzHp);   // ...then highpass -> hiss band
            float hiss = _fzHp * 0.9f;
            // Sparse high sine "bubbles" so it sounds carbonated, not like static.
            float bubbles = 0f;
            if (_rng.NextDouble() < 0.010) bubbles = 0.5f;
            return hiss * 0.5f + bubbles * Mathf.Sin(t * 2f * Mathf.PI * 3200f) * 0.25f;
        }

        /// <summary>Cork out of a bottle: fast pitch drop plus a click transient.</summary>
        static float PopFn(float t, float d)
        {
            float u = t / d;
            float f = Mathf.Lerp(880f, 180f, Ease.OutCubic(u));
            float env = Mathf.Pow(1f - u, 3f);
            return (Mathf.Sin(t * 2f * Mathf.PI * f) * 0.8f + Noise() * 0.25f) * env;
        }

        /// <summary>Perfect-pour reward: a major triad arpeggio.</summary>
        static float ChimeFn(float t, float d)
        {
            float[] f = { 784f, 988f, 1319f };   // G5 B5 E6
            float s = 0f;
            for (int i = 0; i < f.Length; i++)
            {
                float on = i * 0.085f;
                if (t < on) continue;
                float lt = t - on;
                s += Mathf.Sin(lt * 2f * Mathf.PI * f[i]) * Mathf.Exp(-lt * 5.0f);
            }
            return s * 0.30f;
        }

        /// <summary>Wrong flavour / missed order.</summary>
        static float BuzzFn(float t, float d)
        {
            float u = t / d;
            float f = 150f * (1f - 0.35f * u);
            float sq = Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * f));
            return sq * Mathf.Pow(1f - u, 1.2f) * 0.30f;
        }

        /// <summary>Blowout. Rising jet-noise sweep under a screaming tone.</summary>
        static float LaunchFn(float t, float d)
        {
            float u = t / d;
            float f = Mathf.Lerp(120f, 1500f, Ease.InCubic(u));
            float tone = Mathf.Sin(t * 2f * Mathf.PI * f);
            float jet = Noise() * Mathf.Lerp(0.9f, 0.25f, u);
            float env = Mathf.Min(1f, u / 0.05f) * Mathf.Pow(1f - u, 0.7f);
            return (tone * 0.45f + jet * 0.55f) * env * 0.6f;
        }

        /// <summary>Eating a berry.</summary>
        static float ChompFn(float t, float d)
        {
            float u = t / d;
            float f = Mathf.Lerp(300f, 90f, u);
            return (Mathf.Sin(t * 2f * Mathf.PI * f) * 0.6f + Noise() * 0.4f) * Mathf.Pow(1f - u, 2f) * 0.5f;
        }

        static float TickFn(float t, float d)
        {
            float u = t / d;
            return Mathf.Sin(t * 2f * Mathf.PI * 1400f) * Mathf.Pow(1f - u, 4f) * 0.25f;
        }
    }

    /// <summary>Small pooled one-shot player so overlapping SFX never cut each other off.</summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx I;
        AudioSource[] _pool;
        int _next;
        AudioSource _fizzLoop;

        void Awake()
        {
            I = this;
            ProcAudio.BuildAll();
            _pool = new AudioSource[10];
            for (int i = 0; i < _pool.Length; i++)
            {
                var a = gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;
                _pool[i] = a;
            }
            _fizzLoop = gameObject.AddComponent<AudioSource>();
            _fizzLoop.clip = ProcAudio.Fizz;
            _fizzLoop.loop = true;
            _fizzLoop.playOnAwake = false;
            _fizzLoop.volume = 0f;
            _fizzLoop.Play();
        }

        public void Play(AudioClip c, float vol = 1f, float pitch = 1f)
        {
            if (c == null) return;
            var a = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            a.pitch = pitch;
            a.PlayOneShot(c, vol);
        }

        /// <summary>Continuous fizz bed - volume/pitch track how hard the cow is venting.</summary>
        public void SetFizz(float amount01, float pitch = 1f)
        {
            _fizzLoop.volume = Mathf.Lerp(_fizzLoop.volume, Mathf.Clamp01(amount01) * 0.5f, Time.deltaTime * 12f);
            _fizzLoop.pitch = pitch;
        }
    }
}
