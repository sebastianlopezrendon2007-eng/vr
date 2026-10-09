#!/usr/bin/env python3
"""Abrir, compilar y conectar el proyecto Quest. Python estándar, sin pip."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import zipfile

PROJECT = Path(__file__).resolve().parents[1]
APK = PROJECT / "Builds/Quest3/ConsultorioQuest3.apk"
PACKAGE = "mx.telemedicina.consultorio.beta"


def version():
    for line in (PROJECT / "ProjectSettings/ProjectVersion.txt").read_text().splitlines():
        if line.startswith("m_EditorVersion:"):
            return line.split(":", 1)[1].strip()
    raise RuntimeError("No se encontró la versión del proyecto.")


def editor_path():
    configured = os.environ.get("UNITY_EDITOR")
    if configured:
        p = Path(configured).expanduser()
        if p.is_file():
            return p
        raise RuntimeError("UNITY_EDITOR no apunta al ejecutable Unity.")
    v = version()
    candidates = [
        Path.home() / f"Unity/Hub/Editor/{v}/Editor/Unity",
        Path.home() / f"Unity/Hub/Editor/{v}/Editor/Unity.exe",
        Path(os.environ.get("ProgramFiles", "C:/Program Files")) / f"Unity/Hub/Editor/{v}/Editor/Unity.exe",
        Path(f"/opt/unity/editors/{v}/Editor/Unity"),
    ]
    for p in candidates:
        if p.is_file():
            return p
    raise RuntimeError(f"No se encontró Unity {v}. Abre Unity Hub o indica UNITY_EDITOR con la ruta del ejecutable.")


def android_tools(editor=None):
    editor = editor or editor_path()
    return editor.parent / "Data/PlaybackEngines/AndroidPlayer"


def adb_path():
    if os.environ.get("ADB"):
        p = Path(os.environ["ADB"]).expanduser()
        if p.is_file():
            return str(p)
        raise RuntimeError("ADB no apunta a un ejecutable existente.")
    try:
        p = android_tools() / "SDK/platform-tools" / ("adb.exe" if os.name == "nt" else "adb")
        if p.is_file():
            return str(p)
    except RuntimeError:
        pass
    found = shutil.which("adb")
    if found:
        return found
    raise RuntimeError("No se encontró adb. Instala Android SDK & NDK Tools en Unity Hub o indica ADB.")


def run(command, *, timeout=None, capture=False):
    try:
        r = subprocess.run(command, cwd=PROJECT, check=False, timeout=timeout,
                           text=True, encoding="utf-8", errors="replace", capture_output=capture)
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise RuntimeError(f"No se pudo ejecutar {Path(command[0]).name}: {exc}") from exc
    if r.returncode != 0:
        detail = (r.stderr or r.stdout or "")[-1000:] if capture else "Revisa el archivo de log indicado."
        raise RuntimeError(f"{Path(command[0]).name} terminó con código {r.returncode}. {detail}")
    return r.stdout or ""


def device():
    adb = adb_path()
    listing = run([adb, "devices"], capture=True, timeout=20)
    rows = [line.split() for line in listing.splitlines() if line.strip() and not line.startswith(("List", "*"))]
    devices = [r for r in rows if len(r) >= 2]
    if any(r[1] == "unauthorized" for r in devices):
        raise RuntimeError("Ponte el Quest y acepta Permitir depuración USB; después vuelve a ejecutar el comando.")
    if len(devices) != 1 or devices[0][1] != "device":
        raise RuntimeError("Conecta exactamente un Quest por un cable USB de datos. Habilita modo desarrollador y desbloquea el visor.")
    serial = devices[0][0]
    model = run([adb, "-s", serial, "shell", "getprop", "ro.product.model"], capture=True, timeout=20).strip()
    if not model.lower().startswith("quest"):
        raise RuntimeError(f"El dispositivo conectado no es un Quest: {model or 'modelo desconocido'}.")
    return [adb, "-s", serial]


def check_apk(path=None):
    path = APK if path is None else path
    if not path.is_file():
        raise RuntimeError("Todavía no hay APK. Ejecuta compilar o usa el menú Quest 3 > 2. Crear APK en Unity.")
    try:
        with zipfile.ZipFile(path) as z:
            names = set(z.namelist())
            required = {"AndroidManifest.xml", "lib/arm64-v8a/libunity.so", "lib/arm64-v8a/libil2cpp.so"}
            if not required <= names:
                raise RuntimeError("El archivo no contiene el APK Unity ARM64/IL2CPP esperado.")
            if any(n.startswith(("lib/armeabi", "lib/x86")) for n in names):
                raise RuntimeError("El APK contiene arquitecturas distintas de ARM64.")
            if z.testzip():
                raise RuntimeError("El APK está dañado.")
    except zipfile.BadZipFile as exc:
        raise RuntimeError("El APK no es un archivo válido.") from exc
    # Esto verifica estructura e integridad, no ejecución ni firma Android.
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def compile_android():
    editor = editor_path()
    tools = android_tools(editor)
    missing = [name for name in ("SDK", "NDK", "OpenJDK") if not (tools / name).is_dir()]
    if missing:
        raise RuntimeError("Faltan módulos Android de Unity Hub: " + ", ".join(missing) + ". Usa Installs > engranaje > Add modules.")
    logs = PROJECT / "Logs"; logs.mkdir(exist_ok=True)
    # El script requiere que cierres este proyecto en el editor; Unity rechazará dos instancias.
    prepare_log = logs / "android-prepare.log"
    build_log = logs / "android-build.log"
    base = [str(editor), "-batchmode", "-nographics", "-quit", "-projectPath", str(PROJECT)]
    print(f"Preparando Android. Log: {prepare_log}", flush=True)
    run(base + ["-executeMethod", "Telemedicina.Editor.QuestEditor.PrepareBatch", "-logFile", str(prepare_log)])
    started = time.time_ns()
    print(f"Creando APK. Log: {build_log}", flush=True)
    run(base + ["-buildTarget", "Android", "-executeMethod", "Telemedicina.Editor.QuestEditor.BuildAndroidBatch", "-logFile", str(build_log)])
    if not APK.is_file() or APK.stat().st_mtime_ns < started:
        raise RuntimeError("Unity no generó un APK nuevo. Revisa Logs/android-build.log.")
    sha = check_apk()
    info = {"apk": APK.name, "sha256": sha, "unity": version(), "tipo": "Development Build", "probado_en_quest": False}
    (APK.parent / "build-info.json").write_text(json.dumps(info, ensure_ascii=False, indent=2) + "\n")
    print(f"APK creado: {APK}\nSHA256: {sha}")


def connect():
    command = device()
    run(command + ["reverse", "tcp:8000", "tcp:8000"], capture=True, timeout=20)
    print("Backend por USB conectado. En la app usa http://localhost:8000. Reconecta después de quitar el cable o reiniciar.")


def install():
    check_apk()
    command = device()
    result = run(command + ["install", "-r", str(APK)], capture=True, timeout=180)
    if "Success" not in result.splitlines():
        raise RuntimeError("adb no confirmó la instalación del APK.")
    run(command + ["reverse", "tcp:8000", "tcp:8000"], capture=True, timeout=20)
    run(command + ["shell", "monkey", "-p", PACKAGE, "-c", "android.intent.category.LAUNCHER", "1"], capture=True, timeout=20)
    print("App instalada y solicitada su apertura. También está en Biblioteca > Fuentes desconocidas > Consultorio Quest.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("accion", choices=["abrir", "diagnostico", "compilar", "conectar", "instalar"])
    args = parser.parse_args()
    try:
        if args.accion == "abrir":
            command = [str(editor_path()), "-projectPath", str(PROJECT)]
            if sys.platform.startswith("linux"):
                command += ["-force-glcore"]
            subprocess.Popen(command, cwd=PROJECT); print("Abriendo Unity. Pulsa Telemedicina > Abrir consultorio y Play.")
        elif args.accion == "diagnostico":
            print("Proyecto:", PROJECT, "\nVersión:", version())
            editor = editor_path(); print("Editor:", editor)
            tools = android_tools(editor)
            for name in ("SDK", "NDK", "OpenJDK"):
                print(name + ":", "encontrado" if (tools / name).is_dir() else "falta instalar")
            print("APK:", "existente (todavía requiere prueba)" if APK.is_file() else "pendiente de compilar")
        elif args.accion == "compilar":
            compile_android()
        elif args.accion == "conectar":
            connect()
        else:
            install()
        return 0
    except RuntimeError as exc:
        print("ERROR:", exc, file=sys.stderr); return 1


if __name__ == "__main__":
    sys.exit(main())
