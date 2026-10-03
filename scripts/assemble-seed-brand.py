#!/usr/bin/env python3
"""Run seed-brand-assets.py (embedded parts if present, else solid navy PNGs)."""
from __future__ import annotations
import runpy
import sys
from pathlib import Path

SEED = Path(__file__).resolve().parents[1] / "scripts" / "seed-brand-assets.py"

def main() -> int:
    if not SEED.exists():
        print(f"missing {SEED}", file=sys.stderr)
        return 1
    runpy.run_path(str(SEED), run_name="__main__")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
