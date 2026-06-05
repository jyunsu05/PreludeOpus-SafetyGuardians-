#!/usr/bin/env python3
"""
Heuristic scanner for Unity C# under Assets/.
Findings are deduplicated by fingerprint and optionally enriched by OpenAI in the workflow.
"""
from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

SCAN_ROOT = Path("Assets")
SKIP_DIRS = {
    "Library",
    "PackageCache",
    "Temp",
    "obj",
    "bin",
    ".git",
}

RULES: list[tuple[str, str, re.Pattern[str]]] = [
    (
        "gc-allocation-risk",
        "high",
        re.compile(
            r"\b(string\.Format|\.ToString\s*\(|new\s+List<|new\s+Dictionary<|\.Split\s*\(|"
            r"LINQ\.|\.Where\s*\(|\.Select\s*\(|\.OrderBy\s*\()",
            re.IGNORECASE,
        ),
    ),
    (
        "hot-path-find",
        "high",
        re.compile(r"\b(GameObject\.Find|FindObjectOfType|FindObjectsOfType)\s*\(", re.IGNORECASE),
    ),
    (
        "update-getcomponent",
        "medium",
        re.compile(r"\bGetComponent\s*<", re.IGNORECASE),
    ),
    (
        "empty-unity-message",
        "low",
        re.compile(r"\b(void)\s+(Awake|Start|Update|FixedUpdate|OnEnable)\s*\(\s*\)\s*\{\s*\}", re.IGNORECASE),
    ),
    (
        "async-void",
        "medium",
        re.compile(r"\basync\s+void\b", re.IGNORECASE),
    ),
    (
        "compare-tag-deprecated",
        "low",
        re.compile(r'\.tag\s*==\s*"', re.IGNORECASE),
    ),
]


def in_update_context(lines: list[str], idx: int) -> bool:
    """True if line idx is inside Update/FixedUpdate/LateUpdate."""
    depth = 0
    for i in range(idx, -1, -1):
        line = lines[i]
        if re.search(r"\bvoid\s+(Update|FixedUpdate|LateUpdate)\s*\(", line):
            return True
        depth += line.count("}") - line.count("{")
        if depth > 0 and i < idx:
            break
    return False


def scan_file(path: Path) -> list[dict]:
    findings: list[dict] = []
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return findings

    lines = text.splitlines()
    rel = path.as_posix()

    for i, line in enumerate(lines, start=1):
        stripped = line.strip()
        if not stripped or stripped.startswith("//"):
            continue

        for rule_id, severity, pattern in RULES:
            if not pattern.search(line):
                continue

            if rule_id in ("gc-allocation-risk", "update-getcomponent", "hot-path-find"):
                if not in_update_context(lines, i - 1):
                    if rule_id != "hot-path-find":
                        continue

            fingerprint = hashlib.sha256(
                f"{rel}:{i}:{rule_id}:{stripped[:120]}".encode()
            ).hexdigest()[:16]

            findings.append(
                {
                    "fingerprint": fingerprint,
                    "rule": rule_id,
                    "severity": severity,
                    "file": rel,
                    "line": i,
                    "snippet": stripped[:240],
                    "title": title_for(rule_id, rel, i),
                    "body": body_for(rule_id, rel, i, stripped),
                }
            )
    return findings


def title_for(rule_id: str, rel: str, line: int) -> str:
    names = {
        "gc-allocation-risk": "GC allocation risk in hot path",
        "hot-path-find": "Expensive Find* in Update loop",
        "update-getcomponent": "GetComponent in Update loop",
        "empty-unity-message": "Empty Unity lifecycle method",
        "async-void": "async void (unhandled exception risk)",
        "compare-tag-deprecated": "String tag compare (prefer CompareTag)",
    }
    label = names.get(rule_id, rule_id)
    return f"[AI-Scan] {label} — `{Path(rel).name}:{line}`"


def body_for(rule_id: str, rel: str, line: int, snippet: str) -> str:
    hints = {
        "gc-allocation-risk": "Cache references, use StringBuilder, avoid LINQ/allocation in Update.",
        "hot-path-find": "Assign references in Awake/Start or inject via Inspector.",
        "update-getcomponent": "Cache component in Awake/Start.",
        "empty-unity-message": "Remove empty method or add implementation.",
        "async-void": "Prefer async Task or coroutines for Unity.",
        "compare-tag-deprecated": "Use CompareTag() for performance and safety.",
    }
    hint = hints.get(rule_id, "Review and refactor this code path.")
    return f"""## Summary
Automated scan flagged a potential issue in the Unity codebase.

| Field | Value |
|-------|-------|
| **File** | `{rel}` |
| **Line** | {line} |
| **Rule** | `{rule_id}` |
| **Hint** | {hint} |

## Code snippet
```csharp
{snippet}
```

## Suggested next step
1. Triage this finding (valid / false positive).
2. If valid, add label `ai-auto-fix` to let the issue solver open a PR.

> Created by `issue_detector` workflow. Do not edit the fingerprint line below.
"""


def iter_cs_files(root: Path) -> list[Path]:
    files: list[Path] = []
    if not root.exists():
        return files
    for path in root.rglob("*.cs"):
        if any(part in SKIP_DIRS for part in path.parts):
            continue
        files.append(path)
    return files


def main() -> int:
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else SCAN_ROOT
    all_findings: list[dict] = []
    seen: set[str] = set()

    for cs in iter_cs_files(root):
        for f in scan_file(cs):
            if f["fingerprint"] in seen:
                continue
            seen.add(f["fingerprint"])
            all_findings.append(f)

    severity_order = {"high": 0, "medium": 1, "low": 2}
    all_findings.sort(key=lambda x: (severity_order.get(x["severity"], 9), x["file"], x["line"]))

    max_findings = int(__import__("os").environ.get("MAX_FINDINGS", "15"))
    payload = {
        "scanned_files": len(iter_cs_files(root)),
        "finding_count": len(all_findings),
        "findings": all_findings[:max_findings],
    }
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
