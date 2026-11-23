"""Exporta a SVG y PNG los diagramas Mermaid de Docs/*.md en Docs/uml.

Uso: python Tools/render_uml.py [nombre ...]   (requiere Node.js y Chrome o Edge instalados)
Cada bloque mermaid precedido por <!-- diagrama: nombre --> se exporta como Docs/uml/nombre.svg y .png.
"""
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
DOCS = os.path.join(HERE, "..", "Docs")
TARGET = os.path.join(DOCS, "uml")
CLI = "@mermaid-js/mermaid-cli@11.17.1"
BROWSERS = (
    os.environ.get("PUPPETEER_EXECUTABLE_PATH", ""),
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "/usr/bin/google-chrome",
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
)
BLOCK = re.compile(r"<!-- diagrama: (\w+) -->\r?\n```mermaid\r?\n(.*?)\r?\n```", re.S)


def main():
    wanted = sys.argv[1:]
    browser = next((path for path in BROWSERS if path and os.path.exists(path)), None)
    if browser is None:
        sys.exit("No se encontró Chrome ni Edge: definí PUPPETEER_EXECUTABLE_PATH.")
    work = tempfile.mkdtemp(prefix="uml_")
    puppeteer = os.path.join(work, "puppeteer.json")
    style = os.path.join(work, "mermaid.json")
    with open(puppeteer, "w", encoding="utf-8") as file:
        json.dump({"executablePath": browser}, file)
    with open(style, "w", encoding="utf-8") as file:
        json.dump({"theme": "neutral", "themeVariables": {"fontFamily": "Arial, Helvetica, sans-serif"}}, file)
    environment = dict(os.environ, PUPPETEER_SKIP_DOWNLOAD="1")
    failed = 0
    for document in sorted(os.listdir(DOCS)):
        if not document.endswith(".md"):
            continue
        with open(os.path.join(DOCS, document), encoding="utf-8") as file:
            blocks = BLOCK.findall(file.read())
        for name, source in blocks:
            if wanted and name not in wanted:
                continue
            diagram = os.path.join(work, name + ".mmd")
            with open(diagram, "w", encoding="utf-8") as file:
                file.write(source.replace("\r\n", "\n") + "\n")
            for extension, extra in (("svg", []), ("png", ["-s", "2"])):
                output = os.path.join(TARGET, f"{name}.{extension}")
                result = subprocess.run(
                    ["npx", "-y", CLI, "-i", diagram, "-o", output, "-p", puppeteer, "-c", style,
                     "-b", "white", "-w", "1800", "-q", *extra],
                    capture_output=True, text=True, encoding="utf-8", errors="replace",
                    env=environment, shell=os.name == "nt")
                if result.returncode != 0:
                    failed += 1
                    print(f"ERROR {name}: {(result.stderr or result.stdout)[:600]}")
                    break
            else:
                print(f"{document}: {name}")
    shutil.rmtree(work, ignore_errors=True)
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
