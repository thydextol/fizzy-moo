using System.IO;
using UnityEngine;

namespace FizzyMoo
{
    /// <summary>
    /// Deterministic offline capture. Setting Time.captureFramerate decouples the
    /// simulation from wall-clock time, so every frame is rendered and saved no
    /// matter how slow the encode is - the output is a perfectly smooth 60fps
    /// sequence even on a busy machine. JPEG rather than PNG keeps the sequence
    /// to ~1.5GB instead of ~11GB.
    /// </summary>
    public class FrameRecorder : MonoBehaviour
    {
        public int Fps = 60;
        public float Seconds = 78f;
        public int Width = 1920, Height = 1080;
        public int Quality = 92;

        string _dir;
        int _frame;
        Texture2D _tex;
        bool _done, _locked;
        float _settleT;

        void Start()
        {
            _dir = ReadArg("--outdir") ?? Path.Combine(Application.persistentDataPath, "frames");
            var s = ReadArg("--seconds"); if (s != null) float.TryParse(s, out Seconds);
            Directory.CreateDirectory(_dir);

            Screen.SetResolution(Width, Height, false);
            Time.captureFramerate = Fps;
            _tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            Debug.Log($"[FrameRecorder] -> {_dir}  {Seconds}s @ {Fps}fps");
            StartCoroutine(Capture());
        }

        static string ReadArg(string key)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
            return null;
        }

        System.Collections.IEnumerator Capture()
        {
            int total = Mathf.RoundToInt(Seconds * Fps);
            while (_frame < total)
            {
                yield return new WaitForEndOfFrame();

                // The window may never land on exactly the requested size (display
                // limits, Retina scaling). Give it a moment, then just record at
                // whatever the backbuffer actually is rather than spinning forever.
                if (!_locked)
                {
                    _settleT += Time.unscaledDeltaTime;
                    bool matches = Screen.width == Width && Screen.height == Height;
                    if (!matches && _settleT < 2f) { yield return null; continue; }
                    if (!matches)
                    {
                        Width = Screen.width; Height = Screen.height;
                        Destroy(_tex);
                        _tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                        Debug.LogWarning($"[FrameRecorder] falling back to {Width}x{Height}");
                    }
                    _locked = true;
                    Debug.Log($"[FrameRecorder] locked at {Width}x{Height}");
                }
                _tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                _tex.Apply(false);
                File.WriteAllBytes(Path.Combine(_dir, $"f_{_frame:D5}.jpg"), _tex.EncodeToJPG(Quality));
                _frame++;
                if (_frame % 120 == 0) Debug.Log($"[FrameRecorder] {_frame}/{total}");
            }
            _done = true;
            Debug.Log($"[FrameRecorder] DONE {_frame} frames");
            Application.Quit(0);
        }

        void OnApplicationQuit()
        {
            if (!_done) Debug.LogWarning($"[FrameRecorder] quit early at frame {_frame}");
        }
    }
}
