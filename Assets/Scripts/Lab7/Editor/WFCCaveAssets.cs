using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Laboratorios.Lab7.WaveFunctionCollapse.Editor
{
    /// <summary>Prepara las imagenes fuente del segundo tileset como sprites de 128 px.</summary>
    public static class WFCCaveAssets
    {
        public static void PrepareBatch()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                string[] paths = Directory.GetFiles("Assets/Sprites/Tileset2", "cave_*.png").OrderBy(path => path).ToArray();
                if (paths.Length != 7)
                    throw new InvalidOperationException("Se requieren las siete piezas de cueva.");

                foreach (string sourcePath in paths)
                {
                    string path = sourcePath.Replace('\\', '/');
                    TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 128;
                    importer.spritePivot = new Vector2(0.5f, 0.5f);
                    importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.filterMode = FilterMode.Point;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 128;
                    importer.isReadable = true;
                    foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
                        importer.ClearPlatformTextureSettings(platform);
                    importer.SaveAndReimport();

                    // Usar el importador de Unity para reducir las fuentes generadas
                    // y guardar PNG de dimensiones reales uniformes, conservando el GUID.
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture.width != 128 || texture.height != 128)
                        throw new InvalidOperationException("La pieza no es cuadrada: " + path);
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                    Debug.Log("Sprite de cueva preparado: " + path);
                }
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
