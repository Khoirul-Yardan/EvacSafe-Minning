"""Checks that relative links in tracked Markdown files point to existing files.

External URLs and pure in-page anchors are skipped. Exit code 1 on a broken link.
"""
import os
import re
import subprocess
import sys
from urllib.parse import unquote

LINK = re.compile(r"!?\[[^\]]*\]\(([^)\s]+)(?:\s+\"[^\"]*\")?\)")
FENCE = re.compile(r"^\s*(```|~~~)")


def markdown_files():
    out = subprocess.run(["git", "ls-files", "-z", "*.md"], capture_output=True, check=True).stdout
    return [p for p in out.decode("utf-8").split("\0") if p]


def main():
    in_actions = os.getenv("GITHUB_ACTIONS") == "true"
    broken = 0
    files = markdown_files()
    for path in files:
        base = os.path.dirname(path)
        in_code = False
        with open(path, encoding="utf-8-sig") as stream:
            for number, line in enumerate(stream, 1):
                if FENCE.match(line):
                    in_code = not in_code
                if in_code:
                    continue
                for target in LINK.findall(line):
                    if re.match(r"^[a-zA-Z][a-zA-Z0-9+.-]*:", target) or target.startswith("#"):
                        continue
                    file_part = unquote(target.split("#", 1)[0])
                    if not file_part:
                        continue
                    resolved = os.path.normpath(os.path.join(base, file_part))
                    if not os.path.exists(resolved):
                        broken += 1
                        message = path + ":" + str(number) + " tautan rusak -> " + target
                        print(("::error file=" + path + ",line=" + str(number) + "::" + message) if in_actions else "GAGAL: " + message)
    print(str(len(files)) + " dokumen diperiksa, " + str(broken) + " tautan rusak.")
    return 1 if broken else 0


if __name__ == "__main__":
    sys.exit(main())
