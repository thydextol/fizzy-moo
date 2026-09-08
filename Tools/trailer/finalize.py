"""Post-capture: hero figures, round numbers into the report, PDFs, full trailer + highlight cut."""
import sys, os, re, subprocess, json
from PIL import Image
SP="/private/tmp/claude-501/-Users-thynguyen-Desktop-Fall2026-CSCI7000-GenerativeAI/1e57338b-5e13-4ae5-b886-0c4851319195/scratchpad"
FR=sys.argv[1] if len(sys.argv)>1 else f"{SP}/final2"; D="/Users/thynguyen/Desktop/Fall2026/ATLS4616_MixedReality/Deliverables"
FPS=60
S=[]; 
for l in open(f"{FR}/events.tsv"):
    p=l.rstrip("\n").split("\t")
    if p[0]=="S": S.append((int(p[1]),p[2]))
Fcount=sum(1 for l in open(f"{FR}/events.tsv") if l.startswith("F"))
launches=[f for f,c in S if c=="launch"]; chimes=[f for f,c in S if c=="chime"]; pops=[f for f,c in S if c=="pop"]
buzz=[f for f,c in S if c=="buzz" and all(abs(f-L)>150 for L in launches)]
print("launches",launches,"chimes",chimes[:6],"buzz",buzz[:6])

def save(frame,name,crop=None,w=1400):
    im=Image.open(f"{FR}/f_{frame:05d}.jpg")
    if crop: im=im.crop(crop)
    im.thumbnail((w,w)); im.save(f"{D}/report/figs/{name}","JPEG",quality=90)
    im.save(f"{D}/slides/figs/{name}","JPEG",quality=90)
first=chimes[0] if chimes else pops[0]
save(60,"fig_title.png"); save(first-40,"fig_pour.png"); save(first+18,"fig_perfect.png")
if launches: save(launches[0]+22,"fig_blowout.png")
# gameplay + cow: a carrying frame ~2s before the first perfect (tank full, beacon likely on)
save(first-150,"fig_gameplay.png"); save(first-150,"fig_cow.png",crop=(560,330,1360,930))
save(first-40,"fig_can.png",crop=(880,150,1560,730))
# customer card close-up
save(first-40,"fig_customer.png",crop=(1000,120,1700,720))

# --- round numbers: read the HUD? not logged; count events instead ---
served = len([1 for f,c in S if c=="pop"])           # pop plays on every non-spilled serve (perfect ones play chime+pop)
perfect = len(chimes); blow=len([f for f in launches if f<7400]); sloppy=len(buzz)
rep=open(f"{D}/report/report.html").read()
rep=rep.replace('<span id="roundnums">SERVED served, PERFECT perfect, BLOWOUTS blowouts, SCORE points</span>',
                f'<span id="roundnums">{served} served, {perfect} perfect, {blow} blowout{"s" if blow!=1 else ""} (the autopilot deliberately over-eats on its second order to show the mechanic, and walks through wrong fruit once mid-round to show a sloppy pour)</span>')
rep=rep.replace("ONESHOTS one-shots and FIZZ fizz-level samples", f"{len(S)} one-shots and {Fcount:,} fizz-level samples")
open(f"{D}/report/report.html","w").write(rep)
print("report numbers:",served,perfect,blow)

C="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
for src,out in [(f"{D}/report/report.html",f"{D}/report/FizzyMoo_Report.pdf"),(f"{D}/slides/slides.html",f"{D}/slides/FizzyMoo_Slides.pdf")]:
    subprocess.run([C,"--headless=new","--disable-gpu","--no-pdf-header-footer",f"--print-to-pdf={out}",f"file://{src}"],capture_output=True)
    print("pdf",out.split('/')[-1],os.path.getsize(out)//1024,"KB")

# --- video ---
subprocess.run(["zsh",f"{SP}/video/assemble.sh",FR,f"{D}/video/FizzyMoo_SodaCow_FullRun.mp4"],check=True)
subprocess.run(["python3",f"{SP}/video/highlight.py",f"{D}/video/FizzyMoo_SodaCow_FullRun.mp4",f"{FR}/events.tsv",f"{D}/video/FizzyMoo_SodaCow_Demo.mp4"],check=True)
for v in ["FizzyMoo_SodaCow_Demo.mp4","FizzyMoo_SodaCow_FullRun.mp4"]:
    d=subprocess.run(["ffprobe","-v","error","-show_entries","format=duration","-of","csv=p=0",f"{D}/video/{v}"],capture_output=True,text=True).stdout.strip()
    print(v, round(float(d),1),"s", os.path.getsize(f"{D}/video/{v}")//1_000_000,"MB")
