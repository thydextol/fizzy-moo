"""
Cut a ~75 s highlight from the full trailer, driven by the capture's event log.
Segments (capture time; video time = capture + 3 s title card):
  A  0 .. first blowout landing + 6 s      (title, first order, the showboat blowout)
  B  first PERFECT pour  -5 .. +4 s
  C  the deliberate SLOPPY pour (buzz not preceded by overflow) -6 .. +4 s
  D  second blowout -4 .. +5 s
  E  last 9 s of the round (TIME! card) and the end card
Usage: highlight.py <full.mp4> <events.tsv> <out.mp4>
"""
import sys, subprocess
full, ev, out = sys.argv[1:4]
INTRO = 3.0; FPS = 60
S = []
for l in open(ev):
    p = l.rstrip("\n").split("\t")
    if p[0] == "S": S.append((int(p[1]) / FPS, p[2]))
launches = [t for t, c in S if c == "launch"]
chimes   = [t for t, c in S if c == "chime"]
buzzes   = [t for t, c in S if c == "buzz" and all(abs(t - L) > 2.5 for L in launches)]
total_capture = 130.0
segs = []
segs.append((0.0, min(launches[0] + 6.5, 40.0) if launches else 40.0))
if chimes: segs.append((chimes[0] - 5.0, chimes[0] + 4.0))
if buzzes:
    b = min(buzzes, key=lambda t: abs(t - 65))          # the mid-round fumble
    segs.append((b - 6.0, b + 4.0))
if len(launches) > 1: segs.append((launches[1] - 4.0, launches[1] + 5.0))
segs.append((total_capture - 9.0, total_capture))       # last seconds + TIME! card
# merge overlaps, clamp, convert to video time
segs = sorted(segs); merged = []
for a, b in segs:
    a = max(0.0, a); b = min(total_capture, b)
    if merged and a <= merged[-1][1] + 0.5: merged[-1] = (merged[-1][0], max(merged[-1][1], b))
    else: merged.append((a, b))
vsegs = [(a + INTRO, b + INTRO) for a, b in merged]
vsegs.insert(0, (0.0, INTRO))                            # title card
vsegs.append((INTRO + total_capture, INTRO + total_capture + 4.0))  # end card
dur = sum(b - a for a, b in vsegs)
print("segments (video s):", [(round(a,1), round(b,1)) for a, b in vsegs], "->", round(dur,1), "s")
n = len(vsegs)
# One decoded stream cannot feed six trims directly - it has to be split first,
# otherwise only the first segment reaches the concat and the output is 3 s long.
f = [f"[0:v]split={n}" + "".join(f"[vs{i}]" for i in range(n)) + ";",
     f"[0:a]asplit={n}" + "".join(f"[as{i}]" for i in range(n)) + ";"]
for i, (a, b) in enumerate(vsegs):
    fade = "" if i in (0, n-1) else f",fade=t=in:st={a:.3f}:d=0.25,fade=t=out:st={b-0.25:.3f}:d=0.25"
    f.append(f"[vs{i}]trim=start={a:.3f}:end={b:.3f}{fade},setpts=PTS-STARTPTS[v{i}];"
             f"[as{i}]atrim=start={a:.3f}:end={b:.3f},asetpts=PTS-STARTPTS,afade=t=in:st=0:d=0.15,afade=t=out:st={b-a-0.15:.3f}:d=0.15[a{i}];")
concat = "".join(f"[v{i}][a{i}]" for i in range(n)) + f"concat=n={n}:v=1:a=1[v][a]"
cmd = ["ffmpeg", "-loglevel", "warning", "-y", "-i", full, "-filter_complex", "".join(f) + concat,
       "-map", "[v]", "-map", "[a]", "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p",
       "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", out]
subprocess.run(cmd, check=True)
print("wrote", out)
