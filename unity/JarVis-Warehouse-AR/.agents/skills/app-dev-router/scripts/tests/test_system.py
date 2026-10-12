import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
from route import load_catalog, order_modules, plan
from install import install
from validate_system import validate


def fixture_directory():
    root = Path.cwd().resolve()
    fixture = tempfile.TemporaryDirectory(prefix=".app-dev-test-", dir=root)
    if not Path(fixture.name).resolve().is_relative_to(root):
        raise ValueError("Fixture escaped the intended workspace")
    return fixture


class DevelopmentSystemTests(unittest.TestCase):
    def test_package_contract_and_graph(self):
        self.assertEqual(validate()["skills"], 18)
        self.assertEqual(validate()["dependencyGraph"], "acyclic")

    def test_inspect_is_read_only_and_minimal(self):
        result = plan("inspect")
        self.assertEqual(result["modules"], ["app-project-audit"])
        self.assertTrue(result["planningOnly"])

    def test_small_web_ui_skips_integrations_and_architecture(self):
        result = plan("ui", frontend="web", level=1)
        self.assertEqual(set(result["modules"]), {"app-project-audit", "app-git-safety", "app-ui-navigation", "app-testing-regression", "app-release-verification"})

    def test_openai_unity_has_server_dependencies_in_order(self):
        modules = plan("ai", frontend="unity")["modules"]
        for name in ("app-environment-security", "app-backend-data", "app-api-integration", "app-openai-chat", "app-unity-app"):
            self.assertIn(name, modules)
        self.assertLess(modules.index("app-environment-security"), modules.index("app-backend-data"))
        self.assertLess(modules.index("app-backend-data"), modules.index("app-api-integration"))
        self.assertLess(modules.index("app-api-integration"), modules.index("app-openai-chat"))
        self.assertNotIn("app-render-deploy", modules)
        self.assertNotIn("app-voice-audio", modules)

    def test_local_voice_does_not_trigger_remote_provider(self):
        modules = plan("voice", frontend="unity")["modules"]
        self.assertIn("app-voice-audio", modules)
        self.assertNotIn("app-openai-chat", modules)
        self.assertNotIn("app-backend-data", modules)

    def test_voice_ai_adds_remote_dependency_and_gate(self):
        result = plan("voice", frontend="unity", ai=True)
        self.assertIn("app-openai-chat", result["modules"])
        self.assertTrue(any("authorization" in gate for gate in result["gates"]))

    def test_bug_route_only_uses_identified_owner(self):
        modules = plan("bug", frontend="unity", voice=True)["modules"]
        self.assertIn("app-root-cause-debug", modules)
        self.assertLess(modules.index("app-root-cause-debug"), modules.index("app-voice-audio"))
        self.assertNotIn("app-openai-chat", modules)
        self.assertNotIn("app-render-deploy", modules)

    def test_prototype_route_retains_stop_gate(self):
        result = plan("figma", frontend="unity", prototype_first=True)
        self.assertIn("app-figma-ui", result["modules"])
        self.assertTrue(any(gate.startswith("STOP") for gate in result["gates"]))
        self.assertLess(result["modules"].index("app-figma-ui"), result["modules"].index("app-ui-navigation"))

    def test_backend_render_deploy_skips_client_and_inference(self):
        modules = plan("deploy", backend=True, provider="render")["modules"]
        self.assertIn("app-render-deploy", modules)
        self.assertLess(modules.index("app-testing-regression"), modules.index("app-render-deploy"))
        self.assertNotIn("app-unity-app", modules)
        self.assertNotIn("app-openai-chat", modules)

    def test_custom_provider_does_not_choose_render(self):
        result = plan("deploy", provider="custom")
        self.assertNotIn("app-render-deploy", result["modules"])
        self.assertTrue(any("custom-provider" in gate for gate in result["gates"]))

    def test_missing_provider_or_surface_is_rejected(self):
        with self.assertRaises(ValueError):
            plan("deploy")
        with self.assertRaises(ValueError):
            plan("ui", prototype_first=True)
        with self.assertRaises(ValueError):
            plan("feature", provider="render")

    def test_database_auth_realtime_upload_select_server(self):
        for capability in ("database", "auth", "realtime", "upload"):
            with self.subTest(capability=capability):
                result = plan("feature", **{capability: True})
                self.assertIn("app-backend-data", result["modules"])
                self.assertIn("app-environment-security", result["modules"])

    def test_architecture_records_memory_and_impact(self):
        modules = plan("architecture")["modules"]
        self.assertIn("app-architecture", modules)
        self.assertIn("app-project-memory", modules)

    def test_dependency_cycles_fail_closed(self):
        catalog = load_catalog()
        catalog["conditionalEdges"].append(["app-git-safety", "app-project-audit"])
        with self.assertRaises(ValueError):
            order_modules({"app-project-audit", "app-git-safety"}, catalog)

    def test_router_cli_returns_json_without_execution(self):
        result = subprocess.run([sys.executable, str(SCRIPTS / "route.py"), "--kind", "inspect"], text=True, capture_output=True, check=True)
        self.assertEqual(json.loads(result.stdout)["modules"], ["app-project-audit"])

    def test_dry_run_does_not_create_directories(self):
        with fixture_directory() as target:
            result = install(target)
            self.assertFalse((Path(target) / ".agents").exists())
            self.assertEqual(len(result["skills"]), 18)

    def test_install_is_portable_and_preserves_unrelated_files(self):
        with fixture_directory() as target:
            root = Path(target)
            (root / "AGENTS.md").write_text("Existing authority", encoding="utf-8")
            result = install(target, apply=True)
            self.assertEqual(len(result["copied"]), 18)
            self.assertEqual((root / "AGENTS.md").read_text(encoding="utf-8"), "Existing authority")
            self.assertFalse((root / "Docs").exists())
            copied_router = root / ".agents" / "skills" / "app-dev-router" / "scripts" / "route.py"
            output = subprocess.run([sys.executable, str(copied_router), "--kind", "voice", "--frontend", "unity"], text=True, capture_output=True, check=True)
            self.assertNotIn("app-openai-chat", json.loads(output.stdout)["modules"])

    def test_collision_refuses_before_any_copy(self):
        with fixture_directory() as target:
            collision = Path(target) / ".agents" / "skills" / "app-voice-audio"
            collision.mkdir(parents=True)
            (collision / "SKILL.md").write_text("Owner's existing skill", encoding="utf-8")
            with self.assertRaises(FileExistsError):
                install(target, apply=True)
            self.assertEqual((collision / "SKILL.md").read_text(encoding="utf-8"), "Owner's existing skill")
            self.assertFalse((collision.parent / "app-dev-router").exists())

    def test_nonexistent_target_is_not_created(self):
        with fixture_directory() as target:
            missing = Path(target) / "not-an-app"
            with self.assertRaises(OSError):
                install(missing, apply=True)
            self.assertFalse(missing.exists())


if __name__ == "__main__":
    unittest.main(verbosity=2)
