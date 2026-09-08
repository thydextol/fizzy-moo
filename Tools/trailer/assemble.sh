#!/bin/zsh
# Assemble the trailer: title card (3s) + captured game (60fps JPEG frames) + end card (4s),
# with the offline-rescored game audio. Usage: assemble.sh <frames_dir> <out.mp4>
set -e
FR="$1"; OUT="$2"
V=$(cd "$(dirname "$0")" && pwd)
N=$(ls "$FR" | grep -c jpg)
python3 "$V/rescore.py" "$FR/events.tsv" "$V/score.wav" "$N"
python3 "$V/cards.py" "$V/cards"
ENC=(-c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -r 60)
ffmpeg -loglevel error -y -loop 1 -t 3 -i "$V/cards/card_title.png" -vf "fade=t=in:st=0:d=0.5,fade=t=out:st=2.5:d=0.5,format=yuv420p" "${ENC[@]}" "$V/seg_title.mp4"
ffmpeg -loglevel error -y -framerate 60 -i "$FR/f_%05d.jpg" -vf "fade=t=in:st=0:d=0.4,format=yuv420p" "${ENC[@]}" "$V/seg_game.mp4"
ffmpeg -loglevel error -y -loop 1 -t 4 -i "$V/cards/card_end.png" -vf "fade=t=in:st=0:d=0.5,fade=t=out:st=3.3:d=0.7,format=yuv420p" "${ENC[@]}" "$V/seg_end.mp4"
printf "file '%s'\nfile '%s'\nfile '%s'\n" "$V/seg_title.mp4" "$V/seg_game.mp4" "$V/seg_end.mp4" > "$V/concat.txt"
ffmpeg -loglevel error -y -f concat -safe 0 -i "$V/concat.txt" -i "$V/score.wav" \
  -c:v copy -c:a aac -b:a 192k -af "afade=t=out:st=$(python3 -c "print(3+$N/60+4-1.2)"):d=1.2" -shortest -movflags +faststart "$OUT"
ffprobe -v error -show_entries format=duration:stream=codec_name,width,height,r_frame_rate -of default=nw=1 "$OUT"
ls -la "$OUT"
