# EF migration payloads

These `.gz.b64` files are gzip+base64 encoded EF Core migration sources.

Expand before build:

```bash
python3 scripts/expand-migrations.py
```

CI and Docker run this automatically.
