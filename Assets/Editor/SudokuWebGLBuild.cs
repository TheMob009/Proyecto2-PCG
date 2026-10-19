using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCG.Sudoku.Editor
{
    /// <summary>Configuración y exportación reproducible de la entrega web.</summary>
    public static class SudokuWebGLBuild
    {
        private const string MainScene = "Assets/Scenes/Proyecto2 V2.unity";
        private const string Output = "Builds/itch.io/WebGL";
        private const string Archive = "Builds/itch.io/Proyecto2-PCG-WebGL.zip";

        [MenuItem("Tools/PCG/Configurar WebGL para itch.io")]
        public static void Configure()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
            PlayerSettings.defaultWebScreenWidth = 960;
            PlayerSettings.defaultWebScreenHeight = 540;
            PlayerSettings.WebGL.template = "PROJECT:ItchIO";
            // itch.io comprime en su CDN; no se necesitan cabeceras especiales del servidor.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.initialMemorySize = 128;
            AssetDatabase.SaveAssets();
            Debug.Log("WebGL configurado: Proyecto2 V2, resolución ItchIo 960 x 540.");
        }

        [MenuItem("Tools/PCG/Exportar ZIP para itch.io")]
        public static void Export()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Detén Play antes de exportar.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Selecciona WebGL y pulsa Switch Platform en Build Settings antes de exportar.");
            if (!EditorSceneManager.SaveOpenScenes())
                throw new InvalidOperationException("No se pudieron guardar las escenas abiertas.");

            Configure();
            Directory.CreateDirectory(Output);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = Output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Falló WebGL: {report.summary.result}, {report.summary.totalErrors} errores.");

            // Solo empaqueta los recursos del reproductor; excluye informes y copias de depuración.
            using (var stream = new FileStream(Archive, FileMode.Create))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(Path.Combine(Output, "index.html"), "index.html");
                AddDirectory(zip, "Build");
                AddDirectory(zip, "StreamingAssets");
            }
            Debug.Log($"Exportación completada: {Path.GetFullPath(Archive)} ({report.summary.totalSize} bytes de build, {report.summary.totalTime}).");
        }

        private static void AddDirectory(ZipArchive zip, string directory)
        {
            string folder = Path.Combine(Output, directory);
            if (!Directory.Exists(folder)) return;
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string entry = file.Substring(Output.Length + 1).Replace('\\', '/');
                zip.CreateEntryFromFile(file, entry);
            }
        }
    }
}
