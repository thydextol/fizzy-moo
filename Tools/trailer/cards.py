"""Title and end cards for the trailer, drawn with the brand typeface and wordmark."""
from PIL import Image, ImageDraw, ImageFont
import sys
P="/Users/thynguyen/Desktop/Fall2026/ATLS4616_MixedReality/FizzyMoo/Assets/Resources/Brand"
OUT=sys.argv[1]
W,H=1920,1080
CREAM=(255,252,245); INK=(25,27,27); PEACH=(254,195,117)
font_big = ImageFont.truetype(f"{P}/FizzyMoo.ttf", 84)
font_mid = ImageFont.truetype(f"{P}/FizzyMoo.ttf", 44)
try: font_small = ImageFont.truetype("/System/Library/Fonts/HelveticaNeue.ttc", 34)
except Exception: font_small = ImageFont.load_default()

def wordmark(w):
    im=Image.open(f"{P}/Wordmark.png").convert("RGBA"); r=w/im.width
    return im.resize((int(im.width*r), int(im.height*r)), Image.LANCZOS)
def center_text(d, y, txt, font, fill):
    bb=d.textbbox((0,0),txt,font=font); d.text(((W-(bb[2]-bb[0]))/2 - bb[0], y), txt, font=font, fill=fill)

# --- title card ---
im=Image.new("RGB",(W,H),CREAM); d=ImageDraw.Draw(im)
wm=wordmark(760); im.paste(wm,((W-wm.width)//2, 250), wm)
center_text(d, 250+wm.height+40, "SODA COW", font_big, INK)
center_text(d, 250+wm.height+150, "A PLAYABLE AD  ·  UNITY 3D MINI PROJECT", font_mid, PEACH)
center_text(d, H-150, "Thy Nguyen  ·  ATLS/CSCI 4616 Intro to Mixed Reality  ·  Fall 2026", font_small, (90,90,90))
im.save(f"{OUT}/card_title.png")

# --- end card ---
im=Image.new("RGB",(W,H),PEACH); d=ImageDraw.Draw(im)
wm=wordmark(620); im.paste(wm,((W-wm.width)//2, 300), wm)
center_text(d, 300+wm.height+60, "TRY SOMETHING MOO.", font_big, INK)
center_text(d, H-170, "Key Lime  ·  Orange Cream  ·  Pina Colada       best served cold", font_small, (70,50,30))
im.save(f"{OUT}/card_end.png")
print("cards ok")
