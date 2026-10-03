#!/usr/bin/env python3
"""Assemble scripts/seed-brand-assets.py from seed-brand-parts/*.partN then optionally run it."""
from __future__ import annotations
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PARTS = ROOT / "scripts" / "seed-brand-parts"
OUT = ROOT / "scripts" / "seed-brand-assets.py"

def assemble() -> Path:
    files = sorted(PARTS.glob("seed-brand-assets.py.part*"), key=lambda p: int(p.name.rsplit("part", 1)[-1]))
    if not files:
        raise SystemExit(f"No parts in {PARTS}")
    text = "".join(p.read_text() for p in files)
    OUT.write_text(text)
    print(f"assembled {OUT} ({len(text)} chars from {len(files)} parts)")
    return OUT

if __name__ == "__main__":
    path = assemble()
    if "--run" in sys.argv:
        import runpy
        sys.exit(runpy.run_path(str(path), run_name="__main__") or 0)
