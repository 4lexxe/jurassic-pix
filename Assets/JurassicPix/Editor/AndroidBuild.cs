using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JurassicPix.Editor
{
    public static class AndroidBuild
    {
        public const string Output = "Build/Android/JurassicPix.apk";

        [MenuItem("Jurassic Pix/Compilar APK de Android")]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Finish(false, "Falta el módulo Android Build Support de esta versión de Unity. Instalalo desde Unity Hub > Installs > Add modules, con OpenJDK y Android SDK & NDK Tools.");
                return;
            }
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.jurassicpix.juego");
            // ARM64 solo existe con IL2CPP, y es lo que piden los teléfonos actuales.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // El multijugador por red local necesita el permiso de red.
            PlayerSettings.Android.forceInternetPermission = true;
            EditorUserBuildSettings.buildAppBundle = false;
            BuildOptions options = BuildOptions.None;
            // Si el módulo Android quedó instalado a medias (sin las bibliotecas Release
            // de IL2CPP), se compila una versión de desarrollo, que usa otra carpeta.
            string engine = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
            if (!File.Exists(Path.Combine(engine, "Variations", "il2cpp", "Release", "StaticLibs", "arm64-v8a", "libunityruntime.a")))
            {
                Debug.LogWarning("Módulo Android incompleto (sin IL2CPP Release): se compila como versión de desarrollo.");
                options = BuildOptions.Development;
            }
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ExpeditionScene.ScenePath }, locationPathName = Output, target = BuildTarget.Android,
                options = options
            });
            BuildSummary summary = report.summary;
            Finish(summary.result == BuildResult.Succeeded,
                $"APK de Android: {summary.result}, {summary.totalSize / 1048576f:0.0} MB, {summary.totalErrors} errores, en {Output}");
        }

        private static void Finish(bool succeeded, string message)
        {
            if (succeeded)
                Debug.Log(message);
            else
                Debug.LogError(message);
            if (Application.isBatchMode)
                EditorApplication.Exit(succeeded ? 0 : 1);
        }
    }
}
