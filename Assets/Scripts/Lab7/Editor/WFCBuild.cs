using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Laboratorios.Lab7.WaveFunctionCollapse.Editor
{
    public static class WFCBuild
    {
        [Serializable]
        private sealed class BuildSummary
        {
            public string unityVersion, target, result, executable;
            public string[] scenes, warnings;
            public ulong bytes;
            public double seconds;
            public int errors;
        }

        // Compilacion reproducible sin abrir ni cerrar el Editor del usuario.
        public static void BuildWindowsBatch()
        {
            string output = Argument("-wfcBuildOutput", Path.GetFullPath("Builds/WFC-Windows64/WFC_Lab7.exe"));
            string reportPath = Argument("-wfcBuildReport", Path.GetFullPath("Documentation/Lab7/compilacion.json"));
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                string[] scenes = { "Assets/Scenes/WFC.unity" };
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[0]) == null)
                    throw new InvalidOperationException("No se encontro la escena WFC.");
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenes[0], true) };
                PlayerSettings.productName = "WFC Lab7";
                PlayerSettings.bundleVersion = "1.0.0";
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 800;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.runInBackground = true;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                BuildSummary summary = new BuildSummary
                {
                    unityVersion = Application.unityVersion,
                    target = "Windows x64 / Mono / Release",
                    result = report.summary.result.ToString(),
                    executable = Path.GetFileName(output),
                    scenes = scenes,
                    bytes = report.summary.totalSize,
                    seconds = report.summary.totalTime.TotalSeconds,
                    errors = (int)report.summary.totalErrors,
                    warnings = report.steps.SelectMany(step => step.messages)
                        .Where(message => message.type == LogType.Warning || message.type == LogType.Error)
                        .Select(message => message.content).Distinct().ToArray()
                };
                File.WriteAllText(reportPath, JsonUtility.ToJson(summary, true));
                Debug.Log($"WFC build: {summary.result}, {summary.bytes} bytes, {summary.errors} errores.");
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static string Argument(string key, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : fallback;
        }
    }
}
