import argparse
import json
from pathlib import Path


KINDS = ("inspect", "bootstrap", "feature", "ui", "figma", "api", "ai", "voice", "deploy", "bug", "architecture")
LEVELS = {"inspect": 1, "ui": 1, "figma": 2, "feature": 2, "api": 3, "ai": 3, "voice": 3, "deploy": 3, "bug": 2, "architecture": 4, "bootstrap": 3}


def load_catalog():
    return json.loads((Path(__file__).resolve().parents[1] / "catalog.json").read_text(encoding="utf-8"))


def order_modules(selected, catalog):
    selected = set(selected)
    pending = list(selected)
    while pending:
        name = pending.pop()
        if name not in catalog["modules"]:
            raise ValueError("Unknown module: " + name)
        for dependency in catalog["modules"][name]["requires"]:
            if dependency not in selected:
                selected.add(dependency)
                pending.append(dependency)
    predecessors = {name: set(catalog["modules"][name]["requires"]) for name in selected}
    for before, after in catalog["conditionalEdges"]:
        if before in selected and after in selected:
            predecessors[after].add(before)
    result = []
    while len(result) < len(selected):
        ready = [name for name in catalog["order"] if name in selected and name not in result and predecessors[name].issubset(result)]
        if not ready:
            raise ValueError("Dependency cycle or incomplete catalog order")
        result.append(ready[0])
    return result


def plan(kind, frontend="none", backend=False, database=False, auth=False, realtime=False, upload=False, ai=False, voice=False, provider="none", prototype_first=False, level=None, memory=False):
    if kind not in KINDS or frontend not in ("none", "unity", "web", "native") or provider not in ("none", "render", "custom"):
        raise ValueError("Unsupported request classification")
    if provider != "none" and kind != "deploy":
        raise ValueError("Provider deploy selection requires an explicit deploy request")
    if kind == "deploy" and provider == "none":
        raise ValueError("Deployment provider must be established by audit")
    if prototype_first and frontend == "none":
        raise ValueError("Prototype surface must be established by audit")
    level = LEVELS[kind] if level is None else level
    if level not in (1, 2, 3, 4):
        raise ValueError("Task level must be 1–4")
    selected = {"app-project-audit"}
    gates = []
    if kind == "inspect":
        return {"kind": kind, "level": level, "modules": ["app-project-audit"], "gates": [], "planningOnly": True}
    ai = ai or kind == "ai"
    voice = voice or kind == "voice"
    backend = backend or database or auth or realtime or upload or ai or kind == "api"
    selected.update(("app-git-safety", "app-testing-regression", "app-release-verification"))
    if kind == "bug":
        selected.add("app-root-cause-debug")
    if kind in ("bootstrap", "feature"):
        selected.add("app-feature-delivery")
    if level >= 3 or kind in ("bootstrap", "architecture"):
        selected.add("app-architecture")
    if backend or kind in ("bootstrap", "deploy", "architecture"):
        selected.add("app-environment-security")
    if backend:
        selected.add("app-backend-data")
    if kind in ("api", "ai") or ai or backend and frontend != "none" and kind != "deploy":
        selected.add("app-api-integration")
    if ai:
        selected.add("app-openai-chat")
        gates.append("Live provider calls and remote user audio require bounded data/cost authorization.")
    if voice:
        selected.add("app-voice-audio")
    visual = kind in ("ui", "figma") or prototype_first or frontend != "none" and kind in ("bootstrap", "feature", "api", "ai")
    if kind == "figma" or prototype_first:
        selected.add("app-figma-ui")
    if visual:
        selected.add("app-ui-navigation")
    if frontend == "unity" and kind != "deploy":
        selected.add("app-unity-app")
        gates.append("Reuse the open Editor; do not kill it or start a conflicting batch instance.")
    if prototype_first:
        gates.append("STOP after showing prototype; explicit approval is required before production application.")
    if kind == "deploy":
        if provider == "render":
            selected.add("app-render-deploy")
        else:
            gates.append("Load the audited custom-provider adapter; Render instructions do not apply.")
        gates.append("Publish only explicitly authorized reviewed source; paid resources require separate approval.")
    if memory or kind in ("bootstrap", "architecture"):
        selected.add("app-project-memory")
    return {"kind": kind, "level": level, "modules": order_modules(selected, load_catalog()), "gates": gates, "planningOnly": True}


def main():
    parser = argparse.ArgumentParser(description="Plan minimum app skills. Does not execute app or provider actions.")
    parser.add_argument("--kind", required=True, choices=KINDS)
    parser.add_argument("--frontend", choices=("none", "unity", "web", "native"), default="none")
    parser.add_argument("--provider", choices=("none", "render", "custom"), default="none")
    parser.add_argument("--level", type=int, choices=(1, 2, 3, 4))
    for flag in ("backend", "database", "auth", "realtime", "upload", "ai", "voice", "prototype-first", "memory"):
        parser.add_argument("--" + flag, action="store_true")
    options = vars(parser.parse_args())
    try:
        result = plan(**options)
    except ValueError as error:
        parser.error(str(error))
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
