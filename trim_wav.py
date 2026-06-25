#!/usr/bin/env python3
"""
WAV trimmer for SFX assets.
Usage:
  python trim_wav.py <file.wav> <start_sec> <end_sec> [output.wav]

Examples:
  python trim_wav.py "Assets/Sounds/SFX/Grab.wav" 0.5 2.3
  python trim_wav.py "Assets/Sounds/SFX/Emp.wav" 0 1.5 "Assets/Sounds/SFX/Emp_trimmed.wav"
  python trim_wav.py "Assets/Sounds/SFX/Grab.wav" --info
"""

import sys
import wave
import os


def wav_info(path: str) -> None:
    with wave.open(path, "rb") as w:
        channels = w.getnchannels()
        sample_width = w.getsampwidth()
        framerate = w.getframerate()
        n_frames = w.getnframes()
        duration = n_frames / framerate
        print(f"File      : {path}")
        print(f"Duration  : {duration:.4f}s  ({n_frames} frames @ {framerate} Hz)")
        print(f"Channels  : {channels}")
        print(f"Bit depth : {sample_width * 8}-bit")


def trim_wav(src: str, start_sec: float, end_sec: float, dst: str) -> None:
    with wave.open(src, "rb") as w:
        framerate = w.getframerate()
        n_frames = w.getnframes()
        duration = n_frames / framerate

        start_frame = int(start_sec * framerate)
        end_frame = int(end_sec * framerate)

        if start_sec < 0:
            print(f"Error: start ({start_sec}s) must be >= 0")
            sys.exit(1)
        if end_sec > duration:
            print(f"Warning: end ({end_sec}s) exceeds file duration ({duration:.4f}s) — clamping.")
            end_frame = n_frames
        if start_frame >= end_frame:
            print(f"Error: start frame ({start_frame}) >= end frame ({end_frame})")
            sys.exit(1)

        w.setpos(start_frame)
        frames = w.readframes(end_frame - start_frame)

        with wave.open(dst, "wb") as out:
            out.setnchannels(w.getnchannels())
            out.setsampwidth(w.getsampwidth())
            out.setframerate(framerate)
            out.writeframes(frames)

    trimmed_duration = (end_frame - start_frame) / framerate
    print(f"Trimmed   : {start_sec}s → {end_sec}s  ({trimmed_duration:.4f}s)")
    print(f"Saved to  : {dst}")


def main():
    args = sys.argv[1:]

    if not args or args[0] in ("-h", "--help"):
        print(__doc__)
        sys.exit(0)

    src = args[0]
    if not os.path.isfile(src):
        print(f"Error: file not found: {src}")
        sys.exit(1)

    if len(args) == 2 and args[1] == "--info":
        wav_info(src)
        return

    if len(args) < 3:
        print("Usage: python trim_wav.py <file.wav> <start_sec> <end_sec> [output.wav]")
        print('       python trim_wav.py <file.wav> --info')
        sys.exit(1)

    try:
        start_sec = float(args[1])
        end_sec = float(args[2])
    except ValueError:
        print(f"Error: start/end must be numbers, got '{args[1]}' '{args[2]}'")
        sys.exit(1)

    if len(args) >= 4:
        dst = args[3]
    else:
        base, ext = os.path.splitext(src)
        dst = f"{base}_trim{ext}"

    trim_wav(src, start_sec, end_sec, dst)


if __name__ == "__main__":
    main()
