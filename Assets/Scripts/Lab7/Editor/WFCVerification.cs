using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Laboratorios.Lab7.WaveFunctionCollapse.Editor
{
    // Verificacion de integracion en el Editor; no se incluye en el ejecutable.
    // Unity.exe -batchmode -projectPath ... -executeMethod
    // Laboratorios.Lab7.WaveFunctionCollapse.Editor.WFCVerification.RunBatch
    [InitializeOnLoad]
    public static class WFCVerification
    {
        private const string SessionKey = "Lab7.WFCVerification";
        private static readonly string Output = Path.GetFullPath(".utmp/wfc-checks");
        private static int stage, assertions;
        private static double stageTime;
        private static WFCGenerator source;
        private static WFCBoardView view;
        private static int[] cellIds;

        static WFCVerification()
        {
            if (SessionState.GetBool(SessionKey, false))
                EditorApplication.update += Tick;
        }

        public static void RunBatch()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/Scenes/WFC.unity");
            SessionState.SetBool(SessionKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || Time.frameCount < 6)
                return;

            try
            {
                if (stage == 0)
                {
                    source = Object.FindObjectOfType<WFCGenerator>();
                    view = source.GetComponent<WFCBoardView>();
                    Check(source != null && view != null, "La escena contiene generador y vista.");
                    Check(view.VisibleCellCount == 192, "La escena inicia con 16 x 12 celdas.");
                    Check(source.IsInitialized, "La escena se inicializa al comenzar Play.");
                    Check(source.ActiveTileSet.Tiles.Count == 16, "El primer tileset tiene 16 patrones.");
                    Check(source.TileSetCount == 2, "La escena contiene ambos tilesets.");
                    foreach (WFCTile tile in source.ActiveTileSet.Tiles)
                    {
                        Check(tile.sprite != null && tile.sprite.name == tile.id, "Sprite y patron coinciden: " + tile.id);
                        TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tile.sprite)) as TextureImporter;
                        Check(importer != null && importer.textureType == TextureImporterType.Sprite &&
                            importer.spriteImportMode == SpriteImportMode.Single, "Importacion Sprite Single: " + tile.id);
                    }

                    VerifyAlgorithm();
                    cellIds = CellIds();
                    Button step = FindButton("Propagar");
                    Check(step.interactable, "El avance manual esta habilitado al iniciar.");
                    step.onClick.Invoke();
                    Check(source.Phase == WFCPhase.ReadyToObserve, "El primer paso valida las restricciones iniciales.");
                    step.onClick.Invoke();
                    Check(source.LastOperation == WFCOperation.Observation && source.ChangedCellCount == 1,
                        "El boton ejecuta una observacion separada.");
                    Vector2Int observed = source.LastObservedCell;
                    Transform cell = view.transform.Find($"WFC Interface/Board Area/Board/Cell {observed.x},{observed.y}");
                    Image result = cell.Find("Interior/Assigned Pattern").GetComponent<Image>();
                    Check(result.gameObject.activeSelf && result.sprite == source.ActiveTileSet.Tiles[source.GetDomain(observed.x, observed.y)[0]].sprite,
                        "La observacion muestra el sprite asignado.");
                    Check(CellIds().SequenceEqual(cellIds), "La observacion reutiliza las celdas.");
                    Canvas.ForceUpdateCanvases();
                    CapturePreview("observation.png");
                    stage = 1;
                    stageTime = EditorApplication.timeSinceStartup;
                }
                else if (stage == 1 && EditorApplication.timeSinceStartup - stageTime > 1)
                {
                    FindButton("Propagar").onClick.Invoke();
                    Check(source.LastOperation == WFCOperation.Propagation && source.ChangedCellCount > 0,
                        "El boton propaga y modifica los dominios vecinos.");
                    Check(CellIds().SequenceEqual(cellIds), "La propagacion reutiliza las celdas.");
                    Canvas.ForceUpdateCanvases();
                    CapturePreview("propagation.png");
                    stage = 2;
                    stageTime = EditorApplication.timeSinceStartup;
                }
                else if (stage == 2 && EditorApplication.timeSinceStartup - stageTime > 1)
                {
                    FindButton("Reproducir").onClick.Invoke();
                    Check(view.IsPlaying, "La reproduccion automatica comienza.");
                    stage = 3;
                    stageTime = EditorApplication.timeSinceStartup;
                }
                else if (stage == 3 && EditorApplication.timeSinceStartup - stageTime > 1.2)
                {
                    Check(source.ObservationCount > 1, "La reproduccion avanza sin pulsar botones.");
                    FindButton("Reproducir").onClick.Invoke();
                    Check(!view.IsPlaying, "La reproduccion se puede pausar.");
                    InputField[] inputs = view.GetComponentsInChildren<InputField>();
                    inputs.Single(input => input.name == "Width").text = "8";
                    inputs.Single(input => input.name == "Height").text = "6";
                    FindButton("Inicializar / reiniciar").onClick.Invoke();
                    Check(source.Width == 8 && source.Height == 6 && view.VisibleCellCount == 48,
                        "Los controles cambian las dimensiones y reconstruyen la grilla.");
                    cellIds = CellIds();
                    FindButton("Inicializar / reiniciar").onClick.Invoke();
                    Check(CellIds().SequenceEqual(cellIds), "Reiniciar conserva las celdas si las dimensiones coinciden.");
                    inputs.Single(input => input.name == "Width").text = "0";
                    FindButton("Inicializar / reiniciar").onClick.Invoke();
                    Check(source.Width == 8 && source.IsInitialized, "Una dimension invalida no destruye la generacion.");
                    inputs.Single(input => input.name == "Width").text = "8";
                    FindButton("Limpiar").onClick.Invoke();
                    Check(!source.IsInitialized && !FindButton("Propagar").interactable,
                        "Limpiar borra los dominios y deshabilita el avance.");
                    Check(CellIds().SequenceEqual(cellIds), "Limpiar conserva la grilla visual.");
                    FindButton("Cambiar tileset").onClick.Invoke();
                    Check(source.ActiveTileSetIndex == 1 && !source.IsInitialized, "Cambiar tileset limpia el mapa.");
                    FindButton("Inicializar / reiniciar").onClick.Invoke();
                    Check(source.IsInitialized && source.ActiveTileSet.Tiles.Count == 7 && FindButton("Propagar").interactable,
                        "El segundo tileset de siete piezas se inicializa desde los controles.");
                    Check(source.GetDomainCount(0, 0) == 7, "La vista y los dominios usan los patrones del segundo conjunto.");
                    FindButton("Cambiar tileset").onClick.Invoke();
                    FindButton("Inicializar / reiniciar").onClick.Invoke();
                    int steps = 0;
                    while (source.Phase != WFCPhase.Completed && source.Phase != WFCPhase.Contradiction && steps++ < 200)
                        source.Step();
                    Check(source.Phase == WFCPhase.Completed, "La escena completa una generacion tras cambiar y reiniciar.");
                    Check(!FindButton("Propagar").interactable && !FindButton("Reproducir").interactable,
                        "Los controles se detienen al completar.");
                    Canvas.ForceUpdateCanvases();
                    CapturePreview("completed.png");
                    stage = 4;
                    stageTime = EditorApplication.timeSinceStartup;
                }
                else if (stage == 4 && EditorApplication.timeSinceStartup - stageTime > 1)
                {
                    FindButton("Cambiar tileset").onClick.Invoke();
                    SerializedObject settings = new SerializedObject(source);
                    settings.FindProperty("randomSeed").boolValue = false;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    FindButton("Inicializar / reiniciar").onClick.Invoke();
                    foreach (WFCTile tile in source.ActiveTileSet.Tiles)
                    {
                        Check(tile.sprite != null && tile.sprite.name == tile.id, "Sprite de cueva asignado: " + tile.id);
                        Check(tile.sprite.texture.width == 128 && tile.sprite.texture.height == 128, "Sprite de cueva de 128 x 128.");
                        int openings = (tile.north ? 1 : 0) + (tile.east ? 1 : 0) + (tile.south ? 1 : 0) + (tile.west ? 1 : 0);
                        Check(openings == 0 || openings == 2, "La cueva tiene roca o tuneles sin ramificaciones.");
                    }
                    source.Step();
                    source.Step();
                    Check(source.LastOperation == WFCOperation.Observation && source.ChangedCellCount == 1,
                        "La cueva permite observar un patron individual.");
                    CapturePreview("cave-observation.png");
                    stage = 5;
                    stageTime = EditorApplication.timeSinceStartup;
                }
                else if (stage == 5 && EditorApplication.timeSinceStartup - stageTime > 1)
                {
                    source.Step();
                    Check(source.LastOperation == WFCOperation.Propagation && source.ChangedCellCount > 0,
                        "La cueva propaga las conexiones a los dominios vecinos.");
                    CapturePreview("cave-propagation.png");
                    stage = 6;
                    stageTime = EditorApplication.timeSinceStartup;
                }
                else if (stage == 6 && EditorApplication.timeSinceStartup - stageTime > 1)
                {
                    Complete(source);
                    Check(!FindButton("Propagar").interactable && !FindButton("Reproducir").interactable,
                        "La vista reconoce el resultado completo del segundo conjunto.");
                    CapturePreview("cave-completed.png");
                    stage = 7;
                    stageTime = EditorApplication.timeSinceStartup;
                }
                else if (stage == 7 && EditorApplication.timeSinceStartup - stageTime > 1)
                    Finish(null);
            }
            catch (Exception exception)
            {
                Finish(exception);
            }
        }

        private static void VerifyAlgorithm()
        {
            GameObject testObject = new GameObject("WFC verification instance");
            try
            {
                WFCGenerator test = testObject.AddComponent<WFCGenerator>();
                EditorUtility.CopySerialized(source, test);
                SerializedObject settings = new SerializedObject(test);
                settings.FindProperty("randomSeed").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                test.ConfigureSize(4, 3);
                int notifications = 0;
                test.ObservationsChanged += () => notifications++;
                Check(test.Initialize(), "Inicializacion valida.");
                Check(!ReferenceEquals(test.GetDomain(0, 0), test.GetDomain(1, 0)), "Los dominios son independientes.");
                Check(test.Propagate() && test.Phase == WFCPhase.ReadyToObserve, "Consistencia inicial valida.");
                int before = notifications;
                Check(test.Observe() && notifications > before, "Observe notifica a la vista directamente.");
                before = notifications;
                Check(test.Propagate() && notifications > before, "Propagate notifica a la vista directamente.");
                int[] first = Complete(test);
                Check(test.Initialize(), "Se puede reiniciar.");
                Check(first.SequenceEqual(Complete(test)), "La misma semilla reproduce el resultado.");

                test.Clear();
                Check(!test.IsInitialized && test.LastOperation == WFCOperation.None, "Clear restaura el estado inicial.");
                test.SelectTileSet(1);
                Check(test.Initialize(), "El generador acepta el segundo tileset sin reescribir el algoritmo.");
                Complete(test);
                // La prueba negativa usa una copia vacia; la escena conserva
                // las siete piezas reales del segundo conjunto.
                settings.Update();
                settings.FindProperty("tileSets").GetArrayElementAtIndex(1).FindPropertyRelative("tiles").ClearArray();
                settings.ApplyModifiedPropertiesWithoutUndo();
                Check(!test.Initialize(), "Se rechaza un tileset vacio.");
                test.SelectTileSet(0);
                foreach (WFCTile tile in test.ActiveTileSet.Tiles) tile.available = false;
                Check(!test.Initialize(), "Se rechaza un tileset sin patrones habilitados.");

                WFCTile ground = test.ActiveTileSet.Tiles.Single(tile => tile.id == "path_default");
                ground.available = true;
                Check(test.Initialize() && test.Propagate() && test.Phase == WFCPhase.Completed,
                    "Un unico patron compatible completa la grilla tras validarla.");
                ground.available = false;
                WFCTile end = test.ActiveTileSet.Tiles.Single(tile => tile.id == "path_end_T");
                end.available = true;
                Check(test.Initialize() && !test.Propagate() && test.Phase == WFCPhase.Contradiction,
                    "Un unico patron incompatible detecta contradiccion inicial.");
                Check(test.ChangedCellCount > 0 && test.Status.Contains("dominio vacio"), "La contradiccion informa la celda vacia.");
                test.ConfigureSize(1, 1);
                Check(test.Initialize() && test.Propagate() && test.Phase == WFCPhase.Completed,
                    "Una celda aislada no tiene restricciones fuera del mapa.");
                end.weight = 0;
                Check(!test.Initialize() && !test.IsInitialized, "Un peso invalido produce un error de configuracion.");
            }
            finally
            {
                Object.Destroy(testObject);
            }
        }

        private static int[] Complete(WFCGenerator generator)
        {
            int steps = 0;
            int budget = generator.Width * generator.Height * 2 + 4;
            while (generator.Phase != WFCPhase.Completed && generator.Phase != WFCPhase.Contradiction && steps++ < budget)
                generator.Step();
            Check(generator.Phase == WFCPhase.Completed, "La generacion alcanza una solucion.");
            int[] result = new int[generator.Width * generator.Height];
            for (int x = 0; x < generator.Width; x++)
                for (int y = 0; y < generator.Height; y++)
                {
                    int index = generator.GetDomain(x, y)[0];
                    result[y * generator.Width + x] = index;
                    WFCTile tile = generator.ActiveTileSet.Tiles[index];
                    if (x + 1 < generator.Width)
                        Check(tile.IsCompatible(generator.ActiveTileSet.Tiles[generator.GetDomain(x + 1, y)[0]], WFCDirection.East), "Adyacencia horizontal valida.");
                    if (y + 1 < generator.Height)
                        Check(tile.IsCompatible(generator.ActiveTileSet.Tiles[generator.GetDomain(x, y + 1)[0]], WFCDirection.North), "Adyacencia vertical valida.");
                }
            return result;
        }

        private static Button FindButton(string name) => view.GetComponentsInChildren<Button>().Single(button => button.name == name);

        // ScreenCapture no produce archivos en algunas sesiones batchmode.
        // Renderizar explicitamente el mismo Canvas permite revisar la vista.
        private static void CapturePreview(string name)
        {
            Canvas canvas = view.GetComponentInChildren<Canvas>();
            Camera camera = Camera.main;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(1280, 800, 24);
            Texture2D image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                view.SendMessage("LateUpdate");
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline == null)
                    camera.Render();
                else
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                image.Apply();
                bool containsTerrain = source.ActiveTileSetIndex == 0
                    ? image.GetPixels32().Any(pixel => pixel.g > 100 && pixel.g > pixel.r * 1.4f && pixel.g > pixel.b * 1.2f)
                    : image.GetPixels32().Any(pixel => pixel.r > 90 && pixel.r > pixel.g * 1.1f && pixel.g > pixel.b * 1.1f);
                Check(containsTerrain, "La captura contiene los sprites del tileset activo: " + name);
                File.WriteAllBytes(Path.Combine(Output, name), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                camera.targetTexture = previousTarget;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(image);
                Canvas.ForceUpdateCanvases();
                view.SendMessage("LateUpdate");
            }
        }

        private static int[] CellIds() => view.GetComponentsInChildren<RectTransform>()
            .Where(rect => rect.name.StartsWith("Cell ")).Select(rect => rect.GetInstanceID()).OrderBy(id => id).ToArray();

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            assertions++;
        }

        private static void Finish(Exception error)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Tick;
            string report = error == null ? $"PASS: {assertions} comprobaciones de algoritmo, escena y controles." : "FAIL: " + error;
            File.WriteAllText(Path.Combine(Output, "results.txt"), report);
            Debug.Log(report);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
