#!/usr/bin/env python3
"""WAV loudness normalization tool for the Injection project.

Normalizes every .wav under Assets/Sounds/ to a consistent RMS baseline with a
peak ceiling, so in-game audio starts balanced. Fine balance is still done via
the per-sound volume sliders in SoundManager.

Two loudness groups (classified by path):
  - Assets/Sounds/MUSIC/  -> -22 dBFS RMS  (music + ambient, plays continuously)
  - Assets/Sounds/SFX/     -> -18 dBFS RMS  (one-shot / loop SFX)
Shared peak ceiling: -1 dBFS. Max boost is capped at +12 dB.

Pure stdlib (no ffmpeg / numpy). Writes files IN PLACE so .meta/GUIDs stay
intact and Unity references never break. Originals are backed up to
_AudioBackup_raw/ at the repo root before the first apply.

Usage:
    python Tools/normalize_audio.py            # dry-run: print table, touch nothing
    python Tools/normalize_audio.py --apply    # backup (if absent) then overwrite
"""

import argparse
import array
import math
import shutil
import sys
import wave
from pathlib import Path

# ── Tunables (see plan) ────────────────────────────────────────────────────
SFX_TARGET_DBFS = -18.0
MUSIC_TARGET_DBFS = -22.0
PEAK_CEILING_DBFS = -1.0
MAX_BOOST_DB = 12.0

# Paths are resolved relative to the repo root (parent of this Tools/ folder).
REPO_ROOT = Path(__file__).resolve().parent.parent
SOUNDS_DIR = REPO_ROOT / "Assets" / "Sounds"
BACKUP_DIR = REPO_ROOT / "_AudioBackup_raw"


def db_to_linear(db: float) -> float:
    return 10.0 ** (db / 20.0)


def classify_target(path: Path) -> float:
    """Return the RMS target (dBFS) for a file based on its folder."""
    parts = {p.lower() for p in path.parts}
    if "music" in parts:
        return MUSIC_TARGET_DBFS
    return SFX_TARGET_DBFS  # default: everything else (SFX) is louder


def group_name(target: float) -> str:
    return "MUSIC" if target == MUSIC_TARGET_DBFS else "SFX"


def _typecode_and_fullscale(sampwidth: int):
    """Map sample width (bytes) to an array typecode and the +1 full-scale value.

    Only PCM 8/16-bit are present in this project. 16-bit is signed; 8-bit WAV
    is unsigned (centered at 128). 24/32-bit are not handled (skipped + warned).
    """
    if sampwidth == 2:
        return "h", 32768.0, False  # signed 16-bit
    if sampwidth == 1:
        return "B", 128.0, True     # unsigned 8-bit (bias-corrected below)
    return None, None, None


def analyze_file(path: Path):
    """Read a WAV and return analysis dict, or None if unsupported."""
    with wave.open(str(path), "rb") as w:
        params = w.getparams()
        sampwidth = params.sampwidth
        nframes = params.nframes
        raw = w.readframes(nframes)

    typecode, full_scale, is_unsigned = _typecode_and_fullscale(sampwidth)
    if typecode is None:
        return {"path": path, "unsupported": True, "sampwidth": sampwidth}

    samples = array.array(typecode)
    samples.frombytes(raw)

    if not samples:
        return {"path": path, "empty": True}

    # Work in a signed domain centered at 0.
    bias = 128 if is_unsigned else 0

    sum_sq = 0.0
    peak = 0
    for s in samples:
        v = s - bias
        sum_sq += float(v) * float(v)
        a = -v if v < 0 else v
        if a > peak:
            peak = a

    rms = math.sqrt(sum_sq / len(samples))

    target = classify_target(path)
    rms_dbfs = 20.0 * math.log10(rms / full_scale) if rms > 0 else -120.0
    peak_dbfs = 20.0 * math.log10(peak / full_scale) if peak > 0 else -120.0

    # Desired gain to hit the RMS target.
    want_gain = target - rms_dbfs
    # Max gain before the peak crosses the ceiling.
    peak_headroom = PEAK_CEILING_DBFS - peak_dbfs

    boost_capped = want_gain > MAX_BOOST_DB and MAX_BOOST_DB < peak_headroom
    final_gain = min(want_gain, peak_headroom, MAX_BOOST_DB)
    peak_limited = peak_headroom < want_gain and peak_headroom < MAX_BOOST_DB

    result_peak_dbfs = peak_dbfs + final_gain

    return {
        "path": path,
        "params": params,
        "samples": samples,
        "full_scale": full_scale,
        "bias": bias,
        "target": target,
        "rms_dbfs": rms_dbfs,
        "peak_dbfs": peak_dbfs,
        "gain_db": final_gain,
        "result_peak_dbfs": result_peak_dbfs,
        "boost_capped": boost_capped,
        "peak_limited": peak_limited,
    }


def apply_gain(info: dict):
    """Apply the computed gain to samples (clamped) and write back in place."""
    gain = db_to_linear(info["gain_db"])
    samples = info["samples"]
    bias = info["bias"]
    typecode = samples.typecode

    if typecode == "h":
        lo, hi = -32768, 32767
    else:  # "B" unsigned 8-bit
        lo, hi = 0, 255

    out = array.array(typecode)
    for s in samples:
        v = (s - bias) * gain
        v = int(round(v)) + bias
        if v < lo:
            v = lo
        elif v > hi:
            v = hi
        out.append(v)

    params = info["params"]
    with wave.open(str(info["path"]), "wb") as w:
        w.setnchannels(params.nchannels)
        w.setsampwidth(params.sampwidth)
        w.setframerate(params.framerate)
        w.writeframes(out.tobytes())


def make_backup(files):
    """Mirror originals into _AudioBackup_raw/. Skips if backup already exists."""
    if BACKUP_DIR.exists():
        print(f"[backup] {BACKUP_DIR} already exists — keeping it, not overwriting.")
        return
    print(f"[backup] copying {len(files)} originals -> {BACKUP_DIR}")
    for f in files:
        rel = f.relative_to(REPO_ROOT)
        dest = BACKUP_DIR / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(f, dest)
    print("[backup] done.")


def main():
    parser = argparse.ArgumentParser(description="Normalize WAV loudness (RMS + peak ceiling).")
    parser.add_argument("--apply", action="store_true",
                        help="Actually overwrite files (default is dry-run).")
    args = parser.parse_args()

    if not SOUNDS_DIR.exists():
        print(f"ERROR: sounds dir not found: {SOUNDS_DIR}", file=sys.stderr)
        return 1

    files = sorted(SOUNDS_DIR.rglob("*.wav"))
    if not files:
        print(f"No .wav files under {SOUNDS_DIR}")
        return 0

    infos = []
    unsupported = []
    for f in files:
        info = analyze_file(f)
        if info is None or info.get("unsupported"):
            unsupported.append(info)
            continue
        if info.get("empty"):
            continue
        infos.append(info)

    # ── Print table ────────────────────────────────────────────────────────
    mode = "APPLY" if args.apply else "DRY-RUN"
    print(f"\n=== WAV Normalization [{mode}] - {len(infos)} files ===")
    print(f"Targets: SFX {SFX_TARGET_DBFS:+.0f} | MUSIC {MUSIC_TARGET_DBFS:+.0f} | "
          f"peak ceiling {PEAK_CEILING_DBFS:+.0f} dBFS | max boost {MAX_BOOST_DB:+.0f} dB\n")
    header = f"{'file':<52} {'grp':<5} {'RMS':>7} {'peak':>7} {'gain':>7} {'->peak':>7}  flag"
    print(header)
    print("-" * len(header))

    boost_capped = 0
    peak_limited = 0
    for info in infos:
        rel = info["path"].relative_to(SOUNDS_DIR).as_posix()
        if len(rel) > 51:
            rel = "..." + rel[-48:]
        flags = []
        if info["boost_capped"]:
            flags.append("boost-capped")
            boost_capped += 1
        if info["peak_limited"]:
            flags.append("peak-limited")
            peak_limited += 1
        print(f"{rel:<52} {group_name(info['target']):<5} "
              f"{info['rms_dbfs']:>7.1f} {info['peak_dbfs']:>7.1f} "
              f"{info['gain_db']:>+7.1f} {info['result_peak_dbfs']:>+7.1f}  "
              f"{', '.join(flags)}")

    print("-" * len(header))
    print(f"Total: {len(infos)} | boost-capped: {boost_capped} | peak-limited: {peak_limited}")
    if unsupported:
        print(f"\nWARNING: {len(unsupported)} unsupported file(s) skipped "
              f"(non 8/16-bit PCM):")
        for u in unsupported:
            print(f"  - {u['path'].relative_to(SOUNDS_DIR).as_posix()} "
                  f"(sampwidth={u.get('sampwidth')} bytes)")

    if not args.apply:
        print("\nDry-run only. Re-run with --apply to write changes.")
        return 0

    # ── Apply ────────────────────────────────────────────────────────────────
    make_backup([info["path"] for info in infos])
    print("\n[apply] writing normalized files in place...")
    for info in infos:
        apply_gain(info)
    print(f"[apply] done — {len(infos)} files normalized.")
    print("Return to Unity and let it auto-reimport. Originals are in "
          f"{BACKUP_DIR.relative_to(REPO_ROOT)}/")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
