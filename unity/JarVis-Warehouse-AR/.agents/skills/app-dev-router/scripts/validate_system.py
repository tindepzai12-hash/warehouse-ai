import json
import re
from pathlib import Path

from route import load_catalog, order_modules


def validate():
    router = Path(__file__).resolve().parents[1]
    skills = router.parent
    catalog = load_catalog()
    names = [catalog["entrypoint"], *catalog["modules"]]
    if len(set(names)) != len(names) or set(catalog["order"]) != set(catalog["modules"]):
        raise ValueError("Catalog contains duplicate or missing module order")
    order_modules(catalog["modules"], catalog)
    checked = []
    for name in names:
        skill = skills / name / "SKILL.md"
        body = skill.read_text(encoding="utf-8")
        frontmatter = re.match(r'^---\nname: ([a-z0-9-]+)\ndescription: ("[^\n]+")\n---\n', body)
        if not frontmatter or frontmatter.group(1) != name or len(name) > 64 or name.endswith("-") or "--" in name:
            raise ValueError("Invalid frontmatter identity: " + name)
        description = json.loads(frontmatter.group(2))
        if not description.strip() or len(description) > 1024 or "<" in description or ">" in description or description.startswith("[TODO:"):
            raise ValueError("Invalid frontmatter description: " + name)
        for section in ("Purpose", "Trigger", "Pre-check", "Workflow", "Safety Rules", "Verification", "Failure Handling", "Output"):
            if "\n## " + section + "\n" not in body:
                raise ValueError("Incomplete workflow contract: " + name + "/" + section)
        for resource in [skill, *(skills / name).glob("references/*.md")]:
            text = resource.read_text(encoding="utf-8")
            if re.search(r"warehouse-ai|Ng Tín|srv-d|[A-Z]:[\\/]|warehouse-live\.sqlite", text):
                raise ValueError("Project-specific data leaked into reusable module: " + str(resource))
            for link in re.findall(r"\[[^\]]+\]\(([^)]+)\)", text):
                if "://" not in link and not link.startswith("#") and not (resource.parent / link.split("#")[0]).exists():
                    raise ValueError("Broken reference: " + str(resource) + " -> " + link)
        checked.append(name)
    for name, metadata in catalog["modules"].items():
        if (router / metadata["path"]).resolve() != (skills / name / "SKILL.md").resolve():
            raise ValueError("Catalog points outside its named module: " + name)
    return {"passed": True, "skills": len(checked), "frontmatter": "validated JSON-quoted YAML subset", "dependencyGraph": "acyclic", "references": "resolved", "projectIsolation": "checked"}


if __name__ == "__main__":
    print(json.dumps(validate(), indent=2))
