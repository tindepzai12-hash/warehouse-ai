"""Start the local PC service, including bundled-Python virtual environments."""
import runpy
import site
import sys
from pathlib import Path


def main():
    root = Path(__file__).resolve().parent
    local_packages = root / ".venv" / "Lib" / "site-packages"
    if local_packages.is_dir():
        # An embedded Python ._pth can omit the virtual environment's packages.
        site.addsitedir(str(local_packages))
        sys.path.insert(0, str(local_packages))
    sys.path.insert(0, str(root))
    server = root / "server.py"
    sys.argv[0] = str(server)
    runpy.run_path(str(server), run_name="__main__")


if __name__ == "__main__":
    main()
