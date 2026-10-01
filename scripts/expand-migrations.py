#!/usr/bin/env python3
"""Expand gzip+base64 EF migrations from scripts/migrations.bundle.json into Infrastructure."""
from __future__ import annotations
import base64, gzip, json, pathlib, sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
BUNDLE = ROOT / "scripts" / "migrations.bundle.json"
DEST = ROOT / "src" / "FamilyOS.Infrastructure" / "Persistence" / "Migrations"

def main() -> int:
    if not BUNDLE.is_file():
        print(f"Bundle not found: {BUNDLE}", file=sys.stderr)
        return 1
    data = json.loads(BUNDLE.read_text(encoding="utf-8"))
    DEST.mkdir(parents=True, exist_ok=True)
    for name, b64 in data.items():
        raw = gzip.decompress(base64.b64decode(b64))
        path = DEST / name
        path.write_bytes(raw)
        print(f"Wrote {path.relative_to(ROOT)} ({len(raw)} bytes)")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
