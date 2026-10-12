#!/usr/bin/env python3
"""Generate mobile/eas.json (no secrets — EXPO_TOKEN stays in GitHub Actions secrets only)."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MOBILE = ROOT / "mobile"
EAS_PATH = MOBILE / "eas.json"
APP_PATH = MOBILE / "app.json"

DEFAULT_API = "https://api.18.226.226.118.sslip.io"
DEFAULT_AUTH = "https://auth.18.226.226.118.sslip.io/realms/familyos"


def public_env(api_url: str, auth_authority: str, *, include_dev_user: bool) -> dict[str, str]:
    env = {
        "EAS_BUILD_NO_EXPO_GO_WARNING": "true",
        "EXPO_PUBLIC_API_URL": api_url,
        "EXPO_PUBLIC_KEYCLOAK_AUTHORITY": auth_authority,
        "EXPO_PUBLIC_KEYCLOAK_CLIENT_ID": "familyos-mobile",
    }
    if include_dev_user:
        env["EXPO_PUBLIC_ALLOW_DEV_LOGIN"] = "1"
    return env


def build_eas(api_url: str, auth_authority: str) -> dict:
    return {
        "cli": {
            "version": ">= 12.0.0",
            "appVersionSource": "local",
        },
        "build": {
            "development": {
                "developmentClient": True,
                "distribution": "internal",
                "ios": {"simulator": True},
                "android": {"buildType": "apk"},
                "env": public_env(api_url, auth_authority, include_dev_user=True),
            },
            "preview": {
                "distribution": "internal",
                "android": {"buildType": "apk"},
                "env": public_env(api_url, auth_authority, include_dev_user=True),
            },
            "production": {
                "android": {"buildType": "app-bundle"},
                "env": public_env(api_url, auth_authority, include_dev_user=False),
            },
            "production-apk": {
                "android": {"buildType": "apk"},
                "env": public_env(api_url, auth_authority, include_dev_user=False),
            },
        },
        "submit": {
            "production": {
                "android": {
                    "track": "internal",
                    "releaseStatus": "draft",
                }
            }
        },
    }


def ensure_app_project_id(project_id: str | None) -> bool:
    if not project_id or not APP_PATH.exists():
        return False
    data = json.loads(APP_PATH.read_text(encoding="utf-8"))
    expo = data.setdefault("expo", {})
    extra = expo.setdefault("extra", {})
    eas = extra.setdefault("eas", {})
    if eas.get("projectId") == project_id:
        return False
    eas["projectId"] = project_id
    APP_PATH.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    return True


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--api-url", default=DEFAULT_API)
    p.add_argument("--auth-authority", default=DEFAULT_AUTH)
    p.add_argument("--project-id", default=None)
    p.add_argument("--check", action="store_true")
    args = p.parse_args()

    generated = build_eas(args.api_url, args.auth_authority)
    text = json.dumps(generated, indent=2) + "\n"

    if args.check and EAS_PATH.exists():
        current = EAS_PATH.read_text(encoding="utf-8")
        if json.loads(current) != generated:
            print("eas.json is out of date vs scripts/generate-eas-json.py")
            return 1
        print("eas.json matches generator")
        return 0

    EAS_PATH.parent.mkdir(parents=True, exist_ok=True)
    EAS_PATH.write_text(text, encoding="utf-8")
    print(f"Wrote {EAS_PATH.relative_to(ROOT)}")

    if ensure_app_project_id(args.project_id):
        print(f"Updated projectId in {APP_PATH.relative_to(ROOT)}")

    profiles = list(generated["build"].keys())
    print("profiles:", ", ".join(profiles))
    print("NOTE: EXPO_TOKEN is never written to eas.json — use GitHub secret EXPO_TOKEN only.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
