#!/usr/bin/env python3
import pathlib, base64, sys
root = pathlib.Path(__file__).resolve().parents[1]
parts_dir = root / "scripts" / "taskcommands-handlers-parts"
out = root / "src" / "FamilyOS.Application" / "Tasks" / "TaskCommands.Handlers.cs"
if out.exists() and out.stat().st_size > 1000:
    print("handlers already present", out.stat().st_size)
    sys.exit(0)
chunks = sorted(parts_dir.glob("part*.b64"))
if not chunks:
    print("no parts"); sys.exit(1)
data = base64.b64decode("".join(p.read_text().strip() for p in chunks))
out.write_bytes(data)
print("wrote", out, len(data))
