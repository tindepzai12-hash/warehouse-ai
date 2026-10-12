import argparse
import json
import shutil
from pathlib import Path


def installation_plan(target, source=None):
    root = Path(target).resolve(strict=True)
    if not root.is_dir():
        raise ValueError("Target must be an existing explicitly authorized project directory")
    source = Path(source).resolve(strict=True) if source else Path(__file__).resolve().parents[2]
    catalog = json.loads((source / "app-dev-router" / "catalog.json").read_text(encoding="utf-8"))
    names = [catalog["entrypoint"], *catalog["modules"]]
    destination = root / ".agents" / "skills"
    if not destination.resolve().is_relative_to(root):
        raise ValueError("Destination escapes the intended project")
    copies = []
    for name in names:
        if not name.startswith("app-") or "/" in name or "\\" in name:
            raise ValueError("Invalid skill directory")
        origin = source / name
        if not origin.is_dir() or not (origin / "SKILL.md").is_file():
            raise ValueError("Incomplete source skill: " + name)
        if origin.is_symlink() or any(item.is_symlink() for item in origin.rglob("*")):
            raise ValueError("Refusing symlinked source resources")
        final = destination / name
        if final.exists() or final.is_symlink():
            raise FileExistsError("Refusing to overwrite existing skill: " + str(final))
        copies.append((origin, final))
    return copies


def install(target, apply=False, source=None):
    copies = installation_plan(target, source)
    result = {"apply": apply, "target": str(Path(target).resolve()), "skills": [final.name for _, final in copies], "copied": []}
    if apply:
        for origin, final in copies:
            final.parent.mkdir(parents=True, exist_ok=True)
            shutil.copytree(origin, final, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
            result["copied"].append(str(final))
    return result


def main():
    parser = argparse.ArgumentParser(description="Copy only reusable skills to an authorized existing project; default is dry-run.")
    parser.add_argument("--target", required=True)
    parser.add_argument("--apply", action="store_true")
    options = parser.parse_args()
    try:
        result = install(options.target, options.apply)
    except (ValueError, OSError) as error:
        parser.exit(1, "Installation refused: " + str(error) + "\nA failed copy may be partial; inspect target before retrying. No existing skill is overwritten.\n")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
