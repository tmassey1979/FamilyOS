#!/usr/bin/env python3
"""Seed EAS-critical mobile/assets PNGs.

1) If seed-brand-parts exist and reassemble cleanly, use embedded real brand art.
2) Else write solid navy (#0F172A) placeholder PNGs so Expo prebuild succeeds.
"""
from __future__ import annotations
import base64, struct, sys, zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "mobile" / "assets"
PARTS = ROOT / "scripts" / "seed-brand-parts"
NAVY = (15, 23, 42)  # #0F172A


def chunk(tag: bytes, data: bytes) -> bytes:
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)


def solid_png(w: int, h: int, rgb=NAVY) -> bytes:
    r, g, b = rgb
    row = bytes([0] + [r, g, b] * w)
    raw = row * h
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )


def try_embedded() -> bool:
    files = sorted(PARTS.glob("seed-brand-assets.py.part*"), key=lambda p: int(p.name.rsplit("part", 1)[-1]))
    if not files:
        return False
    text = "".join(p.read_text() for p in files)
    # Embedded form stores zlib-compressed PNG base64 in ASSETS dict via exec of assembled body.
    # Prefer running the assembled script if it looks complete.
    if "ASSETS" not in text or len(text) < 50000:
        return False
    assembled = ROOT / "scripts" / "_seed_brand_assembled.py"
    assembled.write_text(text)
    import runpy
    try:
        runpy.run_path(str(assembled), run_name="__main__")
        return True
    except Exception as e:
        print(f"embedded seed failed ({e}); falling back to solid navy", file=sys.stderr)
        return False


def seed_solid() -> None:
    DEST.mkdir(parents=True, exist_ok=True)
    specs = [
        ("icon.png", 512),
        ("adaptive-icon.png", 512),
        ("splash-icon.png", 512),
        ("favicon.png", 48),
    ]
    for name, size in specs:
        data = solid_png(size, size)
        out = DEST / name
        out.write_bytes(data)
        print(f"seeded {out.relative_to(ROOT)} ({len(data)} bytes, solid navy {size}x{size})")


def main() -> int:
    if try_embedded():
        return 0
    seed_solid()
    for name in ("icon.png", "adaptive-icon.png", "splash-icon.png", "favicon.png"):
        p = DEST / name
        if not p.exists() or p.stat().st_size < 50:
            print(f"ERROR missing {p}", file=sys.stderr)
            return 1
        if p.read_bytes()[:8] != b"\x89PNG\r\n\x1a\n":
            print(f"ERROR not PNG {p}", file=sys.stderr)
            return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
