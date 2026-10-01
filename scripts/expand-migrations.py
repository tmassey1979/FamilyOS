#!/usr/bin/env python3
"""Expand gzip+base64 EF migrations into Infrastructure."""
from __future__ import annotations
import base64, gzip, json, pathlib, sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
DEST = ROOT / "src" / "FamilyOS.Infrastructure" / "Persistence" / "Migrations"
BUNDLE = ROOT / "scripts" / "migrations.bundle.json"
PARTS = ROOT / "scripts" / "migrations"

NAMES = [
    "20261001190812_InitialCreate.cs",
    "20261001190812_InitialCreate.Designer.cs",
    "FamilyOsDbContextModelSnapshot.cs",
]

def load_b64(name: str) -> str | None:
    # Prefer split parts (smaller GitHub blobs)
    p0 = PARTS / f"{name}.gz.b64.part0"
    p1 = PARTS / f"{name}.gz.b64.part1"
    if p0.is_file() and p1.is_file():
        return p0.read_text(encoding="utf-8").strip() + p1.read_text(encoding="utf-8").strip()
    whole = PARTS / f"{name}.gz.b64"
    if whole.is_file():
        return whole.read_text(encoding="utf-8").strip()
    if BUNDLE.is_file():
        data = json.loads(BUNDLE.read_text(encoding="utf-8"))
        return data.get(name)
    return None

def main() -> int:
    DEST.mkdir(parents=True, exist_ok=True)
    for name in NAMES:
        b64 = load_b64(name)
        if not b64:
            print(f"Missing migration payload for {name}", file=sys.stderr)
            return 1
        raw = gzip.decompress(base64.b64decode(b64))
        path = DEST / name
        path.write_bytes(raw)
        print(f"Wrote {path.relative_to(ROOT)} ({len(raw)} bytes)")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
