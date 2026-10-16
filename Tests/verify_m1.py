"""Compile Unity references; run M1-M4.1 combat, file/state, placement and polish checks.

This never launches Unity and does not test rendering, physics, scenes or APKs.
"""
import argparse
import json
import pathlib
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-data", required=True, type=pathlib.Path)
    args = parser.parse_args()
    data = args.unity_data.resolve()
    project = pathlib.Path(__file__).resolve().parents[1]
    sdk = data / "DotNetSdk"
    dotnet = sdk / "dotnet"
    compiler = next((sdk / "sdk").glob("*/Roslyn/bincore/csc.dll"))
    managed = data / "Managed"
    reference = data / "UnityReferenceAssemblies/unity-4.8-api"
    package = (data / "Resources/PackageManager/ProjectTemplates/libcache/"
               "com.unity.template.2d-cross-platform-2d-6.1.4/ScriptAssemblies")
    framework = [reference / name for name in
                 ("mscorlib.dll", "System.dll", "System.Core.dll", "Facades/netstandard.dll")]
    engine = sorted((managed / "UnityEngine").glob("UnityEngine.*.dll"))
    editor_modules = sorted((managed / "UnityEngine").glob("UnityEditor.*Module.dll"))
    ui = package / "UnityEngine.UI.dll"
    input_system = package / "Unity.InputSystem.dll"
    urp = [package / name for name in (
        "Unity.RenderPipelines.Universal.Runtime.dll", "Unity.RenderPipelines.Universal.2D.Runtime.dll",
        "Unity.RenderPipelines.Core.Runtime.dll", "Unity.RenderPipelines.Core.Runtime.Shared.dll")]
    if not engine or not editor_modules:
        raise FileNotFoundError("Unity engine/editor module assemblies missing")
    for path in [dotnet, compiler, *framework, *engine, ui, input_system, *urp, *editor_modules]:
        if not path.is_file():
            raise FileNotFoundError(f"Required installed Unity assembly/tool missing: {path}")

    with tempfile.TemporaryDirectory(prefix="cookie-breaker-m1-") as directory:
        output = pathlib.Path(directory)

        def compile_sources(name, references, sources, executable=False):
            destination = output / (name + ".dll")
            options = ["-nologo", "-nostdlib+", "-target:exe" if executable else "-target:library",
                       f'-out:"{destination}"']
            options += [f'-r:"{path}"' for path in references]
            options += [f'"{path}"' for path in sources]
            response = output / (name + ".rsp")
            response.write_text("\n".join(options) + "\n", encoding="utf-8")
            subprocess.run([str(dotnet), str(compiler), "@" + str(response)], check=True)
            return destination

        runtime = compile_sources("CookieBreaker.Runtime", framework + engine + [ui],
                                  sorted((project / "Assets/Scripts").rglob("*.cs")))
        print("PASS: runtime C# compilation (no UnityEditor references).", flush=True)
        compile_sources("CookieBreaker.Editor", framework + engine + editor_modules + [ui, input_system, *urp, runtime],
                        sorted((project / "Assets/Editor").glob("*.cs")))
        print("PASS: Editor C# compilation against installed Unity/package assemblies.", flush=True)

        net_refs = sorted((sdk / "packs/Microsoft.NETCore.App.Ref").glob("8.*/ref/net8.0/*.dll"))
        if not net_refs:
            raise FileNotFoundError(".NET 8 reference pack missing")
        versions = [line.split()[1] for line in subprocess.check_output([str(dotnet), "--list-runtimes"], text=True).splitlines()
                    if line.startswith("Microsoft.NETCore.App 8.")]
        version = max(versions, key=lambda value: tuple(map(int, value.split("."))))
        for name, sources in (
            ("CombatStateChecks", ["Assets/Scripts/Combat/CombatState.cs", "Tests/CombatStateChecks.cs"]),
            ("UpgradeStateChecks", ["Assets/Scripts/Combat/CombatState.cs", "Assets/Scripts/Upgrades/UpgradeState.cs",
                                    "Tests/UpgradeStateChecks.cs"]),
            ("SaveStateChecks", ["Assets/Scripts/Combat/CombatState.cs", "Assets/Scripts/Upgrades/UpgradeState.cs",
                                 "Assets/Scripts/Persistence/SaveData.cs", "Assets/Scripts/Persistence/SaveFileStore.cs",
                                 "Assets/Scripts/Placement/GridPlacementState.cs", "Tests/SaveStateChecks.cs"]),
            ("GridPlacementChecks", ["Assets/Scripts/Combat/CombatState.cs", "Assets/Scripts/Placement/GridPlacementState.cs",
                                     "Assets/Scripts/Persistence/SaveData.cs", "Tests/GridPlacementChecks.cs"]),
            ("PlacementPolishChecks", ["Assets/Scripts/Combat/CombatState.cs", "Assets/Scripts/Combat/CircleSpawnRules.cs",
                                       "Assets/Scripts/Placement/GridPlacementState.cs", "Assets/Scripts/Placement/PlacementGesture.cs",
                                       "Assets/Scripts/Upgrades/UpgradeState.cs", "Tests/PlacementPolishChecks.cs"]),
        ):
            checks = compile_sources(name, net_refs, [project / path for path in sources], True)
            checks.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
                "tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": version}}}), encoding="utf-8")
            subprocess.run([str(dotnet), str(checks)], check=True)
        print("Unity import, physics, scene execution, rendering and APK build remain UNVERIFIED.", flush=True)


if __name__ == "__main__":
    main()
