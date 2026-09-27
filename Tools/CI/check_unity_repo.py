"""Repository hygiene checks for the SAFE-MINING EVAC Unity project.

Runs without Unity or network access. Reads only files tracked by Git, so
local Library/Temp folders never affect the result. Exit code 1 on failure.
"""
import os
import subprocess
import sys

EXPECTED_UNITY = "6000.3.23f1"
FAIL_BYTES = 50 * 1024 * 1024   # GitHub warns at 50 MB and rejects at 100 MB.
WARN_BYTES = 10 * 1024 * 1024
FORBIDDEN_ROOTS = ("Library/", "Temp/", "Obj/", "Logs/", "UserSettings/", "MemoryCaptures/",
                   "Build/", "Builds/", "Recordings/", ".tools/", ".vs/", ".idea/")
FORBIDDEN_SUFFIXES = (".csproj", ".sln", ".pidb", ".booproj", ".svd", ".pdb", ".mdb", ".apk", ".unityproj")
LFS_POINTER_PREFIX = b"version https://git-lfs.github.com/spec/v1"


def tracked_files():
    out = subprocess.run(["git", "ls-files", "-z"], capture_output=True, check=True).stdout
    return [p for p in out.decode("utf-8").split("\0") if p]


def unity_ignored(path):
    # Unity skips hidden entries and names ending with "~"; they never receive .meta files.
    for part in path.split("/"):
        if part.startswith(".") or part.endswith("~") or part.lower() == "cvs" or part.endswith(".tmp"):
            return True
    return False


def check_meta_pairs(files, errors):
    tracked = set(files)
    assets = [f for f in files if f.startswith("Assets/") and not unity_ignored(f)]
    folders = set()
    for f in assets:
        parts = f.split("/")
        for i in range(2, len(parts)):
            folders.add("/".join(parts[:i]))
    for f in assets:
        if not f.endswith(".meta") and f + ".meta" not in tracked:
            errors.append("Aset tanpa .meta: " + f)
    for folder in sorted(folders):
        if folder + ".meta" not in tracked:
            errors.append("Folder tanpa .meta: " + folder)
    for f in assets:
        if f.endswith(".meta"):
            target = f[:-5]
            if target not in tracked and target not in folders:
                errors.append(".meta yatim (aset/folder tidak ada di Git): " + f)


def check_unity_version(errors):
    try:
        with open("ProjectSettings/ProjectVersion.txt", encoding="utf-8") as stream:
            first = stream.readline().strip()
    except OSError as error:
        errors.append("ProjectVersion.txt tidak terbaca: " + str(error))
        return
    if first != "m_EditorVersion: " + EXPECTED_UNITY:
        errors.append("Versi Unity berubah: '" + first + "', seharusnya " + EXPECTED_UNITY +
                      ". Jangan commit hasil membuka proyek di editor lain.")


def check_forbidden(files, errors):
    for f in files:
        if f.startswith(FORBIDDEN_ROOTS) or (("/" not in f) and f.endswith(FORBIDDEN_SUFFIXES)):
            errors.append("Berkas lokal/hasil build ikut ter-commit: " + f)


def check_sizes(files, errors, warnings):
    for f in files:
        try:
            size = os.path.getsize(f)
        except OSError:
            continue  # Deleted in the working tree; Git still lists it until staged.
        if size < WARN_BYTES:
            continue
        with open(f, "rb") as stream:
            if stream.read(len(LFS_POINTER_PREFIX)) == LFS_POINTER_PREFIX:
                continue
        message = f + " berukuran " + str(round(size / 1048576, 1)) + " MB di luar Git LFS"
        (errors if size >= FAIL_BYTES else warnings).append(message)


def main():
    files = tracked_files()
    errors, warnings = [], []
    check_meta_pairs(files, errors)
    check_unity_version(errors)
    check_forbidden(files, errors)
    check_sizes(files, errors, warnings)
    in_actions = os.getenv("GITHUB_ACTIONS") == "true"
    for w in warnings:
        print(("::warning::" if in_actions else "PERINGATAN: ") + w)
    for e in errors:
        print(("::error::" if in_actions else "GAGAL: ") + e)
    print(str(len(files)) + " berkas diperiksa, " + str(len(errors)) + " kegagalan, " + str(len(warnings)) + " peringatan.")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
