"""
Re-score a Fizzy Moo frame capture.

The game's audio is synthesised at runtime (ProcAudio.cs). A recording only
captures pixels, so the FrameRecorder also logs an event track (events.tsv):
    S <frame> <clip> <vol> <pitch>   one-shot SFX
    F <frame> <vol> <pitch>          fizz-bed level that frame
This script ports the same synth formulas to numpy, places every event on the
timeline, lays a light procedural music bed underneath, and writes score.wav.
"""
import sys, math, numpy as np, wave

SR = 44100
FPS = 60
rng = np.random.default_rng(7)

def env_lin(t, dur, attack, power):
    u = np.clip(t / dur, 0, 1)
    return np.minimum(1.0, u / max(attack, 1e-6)) * (1 - u) ** power

# ---------------- SFX ports of ProcAudio.cs ----------------
def moo(dur=1.05):
    t = np.arange(int(dur*SR)) / SR; u = t/dur
    f0 = 138 + (96-138) * (1 - (1-u)**3)
    f0 = f0 * (1 + 0.035*np.sin(t*2*np.pi*5.5))
    ph = np.cumsum(f0)/SR
    saw = 2*(ph - np.floor(ph+0.5))
    formant = 0.42*np.sin(np.cumsum(f0*3.02)/SR*2*np.pi)
    env = np.minimum(1, u/0.16) * (1-u)**1.5
    return (0.75*saw + formant) * env * 0.55

def pop(dur=0.16):
    t = np.arange(int(dur*SR))/SR; u=t/dur
    f = 880 + (180-880)*(1-(1-u)**3)
    return (np.sin(np.cumsum(f)/SR*2*np.pi)*0.8 + rng.uniform(-1,1,t.size)*0.25) * (1-u)**3

def chime(dur=0.70):
    t = np.arange(int(dur*SR))/SR; s = np.zeros_like(t)
    for i, f in enumerate([784, 988, 1319]):
        on = i*0.085; lt = np.clip(t-on, 0, None)
        s += np.where(t >= on, np.sin(lt*2*np.pi*f)*np.exp(-lt*5.0), 0)
    return s*0.30

def buzz(dur=0.38):
    t = np.arange(int(dur*SR))/SR; u=t/dur
    f = 150*(1-0.35*u)
    return np.sign(np.sin(np.cumsum(f)/SR*2*np.pi)) * (1-u)**1.2 * 0.30

def launch(dur=1.30):
    t = np.arange(int(dur*SR))/SR; u=t/dur
    f = 120 + (1500-120)*u**3
    tone = np.sin(np.cumsum(f)/SR*2*np.pi)
    jet = rng.uniform(-1,1,t.size) * (0.9 + (0.25-0.9)*u)
    env = np.minimum(1, u/0.05) * (1-u)**0.7
    return (tone*0.45 + jet*0.55)*env*0.6

def chomp(dur=0.18):
    t = np.arange(int(dur*SR))/SR; u=t/dur
    f = 300 + (90-300)*u
    return (np.sin(np.cumsum(f)/SR*2*np.pi)*0.6 + rng.uniform(-1,1,t.size)*0.4)*(1-u)**2*0.5

def tick(dur=0.06):
    t = np.arange(int(dur*SR))/SR; u=t/dur
    return np.sin(t*2*np.pi*1400)*(1-u)**4*0.25

CLIPS = {"moo": moo, "pop": pop, "chime": chime, "buzz": buzz, "launch": launch, "chomp": chomp, "tick": tick}

def fizz_bed(n):
    """Band-limited noise + sparse bubble sines, same recipe as FizzFn."""
    from scipy.signal import lfilter
    noise = rng.uniform(-1, 1, n)
    a, b = 0.35, 0.86
    lp = lfilter([a], [1.0, -(1.0 - a)], noise)      # y = y + a*(x - y)
    hp = lfilter([1.0], [1.0, -(b - 1.0)], lp)       # h = b*h + (lp - h)
    hiss = hp * 0.9
    bub = (rng.uniform(0, 1, n) < 0.010) * 0.5
    t = np.arange(n)/SR
    return hiss*0.5 + bub*np.sin(t*2*np.pi*3200)*0.25

# ---------------- music bed ----------------
def music(n, bpm=118):
    t = np.arange(n)/SR
    beat = 60/bpm
    out = np.zeros(n)
    # I - vi - IV - V in C, one chord per bar
    chords = [(261.63, 329.63, 392.00), (220.00, 261.63, 329.63), (174.61, 220.00, 261.63), (196.00, 246.94, 293.66)]
    roots = [65.41, 55.00, 87.31, 98.00]
    total_beats = int(n / SR / beat) + 1
    for bt in range(total_beats):
        bar = (bt // 4) % 4; inbar = bt % 4
        s0 = int(bt*beat*SR)
        # kick on 1 and 3, soft snare on 2 and 4
        if inbar in (0, 2):
            L = int(0.16*SR); tt = np.arange(L)/SR
            k = np.sin(np.cumsum(120 + (45-120)*np.minimum(1, tt/0.12))/SR*2*np.pi) * (1-tt/0.16)**2 * 0.55
            out[s0:s0+L] += k[:max(0, min(L, n-s0))]
        else:
            L = int(0.12*SR); tt = np.arange(L)/SR
            sn = rng.uniform(-1,1,L)*(1-tt/0.12)**3*0.16
            out[s0:s0+L] += sn[:max(0, min(L, n-s0))]
        # bass: root on the beat, octave pickup on the "and" of 2 and 4
        L = int(beat*0.9*SR); tt = np.arange(L)/SR
        f = roots[bar]
        bass = (np.sin(tt*2*np.pi*f) + 0.35*np.sin(tt*2*np.pi*f*2)) * np.exp(-tt*4.5) * 0.30
        out[s0:s0+L] += bass[:max(0, min(L, n-s0))]
        # plucked chord on the off-beat 8th
        s1 = int((bt+0.5)*beat*SR); L = int(0.30*SR); tt = np.arange(L)/SR
        pl = sum(np.sin(tt*2*np.pi*fr) + 0.25*np.sin(tt*2*np.pi*fr*2) for fr in chords[bar]) * np.exp(-tt*9.0) * 0.09
        if s1 < n: out[s1:s1+L] += pl[:max(0, min(L, n-s1))]
        # hats on every 8th
        for h in (0.0, 0.5):
            sh = int((bt+h)*beat*SR); L = int(0.03*SR); tt = np.arange(L)/SR
            hat = rng.uniform(-1,1,L)*(1-tt/0.03)**2*(0.07 if h == 0 else 0.045)
            if sh < n: out[sh:sh+L] += hat[:max(0, min(L, n-sh))]
    return out

# ---------------- assemble ----------------
def render(events_path, out_path, intro_s=3.0, outro_s=4.0, game_frames=None):
    S, F = [], []
    fps = FPS
    for line in open(events_path):
        if line.startswith("#"):
            if "fps=" in line: fps = int(line.split("fps=")[1]); 
            continue
        p = line.rstrip("\n").split("\t")
        if p[0] == "S": S.append((int(p[1]), p[2], float(p[3]), float(p[4])))
        elif p[0] == "F": F.append((int(p[1]), float(p[2]), float(p[3])))
    last = max([s[0] for s in S] + [f[0] for f in F] + [0])
    if game_frames: last = max(last, game_frames)
    game_len = last / fps
    total = intro_s + game_len + outro_s
    n = int(total*SR)
    mix = np.zeros(n)

    # one-shots
    for fr, name, vol, pitch in S:
        gen = CLIPS.get(name)
        if gen is None: continue
        clip = gen() * vol
        if abs(pitch - 1.0) > 0.01:   # resample for pitch
            idx = np.arange(0, clip.size, pitch)
            clip = np.interp(idx, np.arange(clip.size), clip)
        s0 = int((intro_s + fr/fps)*SR)
        e = min(n, s0 + clip.size)
        if s0 < n: mix[s0:e] += clip[:e-s0]

    # fizz bed with per-frame gain envelope
    if F:
        fr_idx = np.array([f[0] for f in F]); vols = np.array([f[1] for f in F]); pits = np.array([f[2] for f in F])
        bed_n = int(game_len*SR) + SR
        bed = fizz_bed(bed_n)
        tt = np.arange(bed_n)/SR*fps
        g = np.interp(tt, fr_idx, vols, left=0, right=0)
        # crude pitch: brighten with a short high-pass mix when pitch > 1
        pv = np.interp(tt, fr_idx, pits, left=1, right=1)
        bright = np.concatenate([[0], np.diff(bed)]) * 6.0
        bed = bed*(1 - 0.5*(pv-1)) + bright*(0.5*(pv-1))
        s0 = int(intro_s*SR); e = min(n, s0+bed_n)
        mix[s0:e] += (bed*g*0.9)[:e-s0]

    # music under everything, ducked slightly during the round
    m = music(n)
    duck = np.ones(n)
    s0 = int(intro_s*SR); duck[s0:] = 0.72
    mix += m * duck * 0.55

    # master: soft clip + normalise
    mix = np.tanh(mix*1.3)
    mix = mix / max(1e-6, np.abs(mix).max()) * 0.89
    pcm = (mix*32767).astype(np.int16)
    with wave.open(out_path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    print(f"score: {total:.1f}s  one-shots={len(S)}  fizz-samples={len(F)} -> {out_path}")

if __name__ == "__main__":
    ev, out = sys.argv[1], sys.argv[2]
    gf = int(sys.argv[3]) if len(sys.argv) > 3 else None
    render(ev, out, game_frames=gf)
