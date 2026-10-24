#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Laboratorios.Lab7.WaveFunctionCollapse
{
    /// <summary>Prueba el ejecutable real solo al recibir -wfc-self-test.</summary>
    public sealed class WFCPlayerVerification : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public string product, unityVersion, platform, failure;
            public int assertions;
            public List<string> checks = new List<string>();
            public List<string> errors = new List<string>();
        }

        private WFCGenerator generator;
        private WFCBoardView view;
        private readonly Result result = new Result();
        private string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchWhenRequested()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-wfc-self-test") >= 0)
                new GameObject("WFC Player Verification").AddComponent<WFCPlayerVerification>();
        }

        private IEnumerator Start()
        {
            output = Argument("-wfc-test-output", Path.Combine(Application.persistentDataPath, "Verification"));
            Directory.CreateDirectory(output);
            Application.logMessageReceived += RecordError;
            IEnumerator checks = RunChecks();
            while (true)
            {
                bool next;
                object delay = null;
                try
                {
                    next = checks.MoveNext();
                    if (next) delay = checks.Current;
                }
                catch (Exception error)
                {
                    result.failure = error.ToString();
                    break;
                }
                if (!next) break;
                yield return delay;
            }
            result.product = Application.productName;
            result.unityVersion = Application.unityVersion;
            result.platform = Application.platform.ToString();
            result.passed = result.failure == null && result.errors.Count == 0;
            File.WriteAllText(Path.Combine(output, "resultado.json"), JsonUtility.ToJson(result, true));
            Debug.Log($"WFC PLAYER TEST: {(result.passed ? "PASS" : "FAIL")} ({result.assertions} comprobaciones)");
            Application.logMessageReceived -= RecordError;
            Application.Quit(result.passed ? 0 : 1);
        }

        private IEnumerator RunChecks()
        {
            for (int i = 0; i < 4; i++) yield return null;
            generator = FindObjectOfType<WFCGenerator>();
            view = FindObjectOfType<WFCBoardView>();
            Check(generator != null && view != null && generator.IsInitialized, "Arranque e inicializacion de WFC.");
            Check(SceneManager.sceneCountInBuildSettings == 1 && SceneManager.GetActiveScene().name == "WFC", "El ejecutable contiene solo la escena WFC.");
            Check(!typeof(WFCGenerator).Assembly.GetTypes().Any(type =>
                (type.Namespace != null && type.Namespace.StartsWith("PCG.Sudoku")) || type.Name.Contains("Pokemon")),
                "El ejecutable no contiene los scripts de Sudoku ni Pokemon.");
            Check(generator.TileSetCount == 2 && generator.ActiveTileSet.Tiles.Count == 16, "Disponibles ambos conjuntos; inicia Caminos.");
            Check(view.VisibleCellCount == 192, "Tablero inicial de 16 x 12.");

            int[] ids = CellIds();
            Click("Propagar");
            Check(generator.Phase == WFCPhase.ReadyToObserve, "Primer paso: consistencia inicial.");
            Click("Propagar");
            Check(generator.LastOperation == WFCOperation.Observation && generator.ChangedCellCount == 1, "Observacion manual independiente.");
            Check(ids.SequenceEqual(CellIds()), "La observacion reutiliza las celdas visuales.");
            Vector2Int observed = generator.LastObservedCell;
            Button cell = view.GetComponentsInChildren<Button>().Single(button => button.name == $"Cell {observed.x},{observed.y}");
            Click(cell);
            Check(view.GetComponentsInChildren<Text>(true).Any(text => text.text.StartsWith($"Celda ({observed.x}, {observed.y})")), "La seleccion muestra el detalle del dominio.");
            Capture("caminos_observacion.png");
            Click("Propagar");
            Check(generator.LastOperation == WFCOperation.Propagation && generator.ChangedCellCount > 0, "Propagacion manual reduce dominios vecinos.");
            Check(ids.SequenceEqual(CellIds()), "La propagacion conserva los objetos de la grilla.");
            Capture("caminos_propagacion.png");

            int observations = generator.ObservationCount;
            Click("Reproducir");
            Check(view.IsPlaying, "La reproduccion automatica comienza.");
            yield return new WaitForSecondsRealtime(1.2f);
            Check(generator.ObservationCount > observations, "La reproduccion avanza realmente entre fotogramas.");
            Click("Reproducir");
            Check(!view.IsPlaying, "La reproduccion se puede pausar.");
            observations = generator.ObservationCount;
            int propagations = generator.PropagationCount;
            yield return new WaitForSecondsRealtime(0.7f);
            Check(generator.ObservationCount == observations && generator.PropagationCount == propagations, "La pausa detiene el proceso.");

            SetSize(8, 6);
            Check(generator.Width == 8 && generator.Height == 6 && view.VisibleCellCount == 48, "Controles de dimensiones 8 x 6.");
            ids = CellIds();
            Click("Inicializar / reiniciar");
            Check(ids.SequenceEqual(CellIds()), "Reiniciar conserva la grilla del mismo tamano.");
            Input("Width").text = "0";
            Click("Inicializar / reiniciar");
            Check(generator.Width == 8 && generator.IsInitialized, "Dimensiones invalidas no destruyen la generacion.");
            Input("Width").text = "8";
            Click("Limpiar");
            Check(!generator.IsInitialized && !Button("Propagar").interactable, "Limpiar borra dominios y deshabilita el avance.");
            Check(ids.SequenceEqual(CellIds()), "Limpiar conserva las celdas.");

            SetSeed(12345);
            Click("Inicializar / reiniciar");
            Complete();
            ValidateAdjacencies();
            Capture("caminos_resultado.png");
            Check(!Button("Propagar").interactable && !Button("Reproducir").interactable, "Los controles se detienen al completar Caminos.");

            Click("Cambiar tileset");
            Check(generator.ActiveTileSetIndex == 1 && !generator.IsInitialized, "Cambiar a Cueva limpia el estado anterior.");
            Click("Inicializar / reiniciar");
            Check(generator.ActiveTileSet.Tiles.Count == 7 && generator.GetDomainCount(0, 0) == 7, "Cueva inicia con sus siete patrones.");
            foreach (WFCTile tile in generator.ActiveTileSet.Tiles)
            {
                Check(tile.sprite != null && tile.sprite.name == tile.id && tile.sprite.texture.width == 128,
                    "Sprite de cueva incluido correctamente: " + tile.id);
                int openings = (tile.north ? 1 : 0) + (tile.east ? 1 : 0) + (tile.south ? 1 : 0) + (tile.west ? 1 : 0);
                Check(openings == 0 || openings == 2, "Cueva mantiene su estructura de roca o dos conexiones.");
            }
            Click("Propagar");
            Click("Propagar");
            Check(generator.LastOperation == WFCOperation.Observation, "Observacion en el segundo conjunto.");
            Capture("cueva_observacion.png");
            Click("Propagar");
            Check(generator.LastOperation == WFCOperation.Propagation && generator.ChangedCellCount > 0, "Propagacion en el segundo conjunto.");
            Capture("cueva_propagacion.png");
            Complete();
            ValidateAdjacencies();
            Capture("cueva_resultado.png");

            Screen.SetResolution(960, 600, false);
            for (int i = 0; i < 4; i++) yield return null;
            Click("Inicializar / reiniciar");
            Check(view.VisibleCellCount == 48 && generator.GetDomainCount(0, 0) == 7, "Los controles siguen funcionando a 960 x 600.");
            SetSize(1, 1);
            Complete();
            Check(generator.GetDomainCount(0, 0) == 1, "Generacion correcta de una unica celda.");
            Screen.SetResolution(1280, 800, false);
            for (int i = 0; i < 4; i++) yield return null;
            VerifyContradiction();
            Check(result.errors.Count == 0, "Ejecucion sin errores ni excepciones de Unity.");
        }

        private void VerifyContradiction()
        {
            FieldInfo setsField = typeof(WFCGenerator).GetField("tileSets", BindingFlags.Instance | BindingFlags.NonPublic);
            object original = setsField.GetValue(generator);
            WFCTileSet invalid = new WFCTileSet { id = "Prueba de contradiccion" };
            typeof(WFCTileSet).GetField("tiles", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(invalid,
                new List<WFCTile> { new WFCTile { id = "solo_salida_superior", north = true, available = true, weight = 1 } });
            try
            {
                setsField.SetValue(generator, new List<WFCTileSet> { invalid });
                generator.SelectTileSet(0);
                SetSize(2, 2);
                Click("Propagar");
                Check(generator.Phase == WFCPhase.Contradiction && generator.Status.Contains("dominio vacio"), "Se detecta una contradiccion con ubicacion y motivo.");
                Check(!Button("Propagar").interactable && !Button("Reproducir").interactable, "La contradiccion detiene los controles.");
                Check(view.GetComponentsInChildren<Text>().Any(text => text.text == "X"), "La celda sin posibilidades se identifica visualmente.");
                Capture("contradiccion.png", false);
            }
            finally
            {
                setsField.SetValue(generator, original);
                generator.SelectTileSet(0);
            }
            SetSize(8, 6);
            Check(generator.IsInitialized && generator.ActiveTileSet.Tiles.Count == 16, "Se puede reiniciar tras una contradiccion.");
        }

        private void Complete()
        {
            int budget = generator.Width * generator.Height * 2 + 4;
            while ((generator.Phase == WFCPhase.ReadyToObserve || generator.Phase == WFCPhase.Propagating) && budget-- > 0)
                Click("Propagar");
            Check(generator.Phase == WFCPhase.Completed, "La generacion finaliza: " + generator.ActiveTileSet.id);
        }

        private void ValidateAdjacencies()
        {
            for (int x = 0; x < generator.Width; x++)
                for (int y = 0; y < generator.Height; y++)
                {
                    Check(generator.GetDomainCount(x, y) == 1, "Una sola asignacion final por celda.");
                    WFCTile tile = generator.ActiveTileSet.Tiles[generator.GetDomain(x, y)[0]];
                    if (x + 1 < generator.Width)
                        Check(tile.IsCompatible(generator.ActiveTileSet.Tiles[generator.GetDomain(x + 1, y)[0]], WFCDirection.East), "Adyacencia horizontal valida.");
                    if (y + 1 < generator.Height)
                        Check(tile.IsCompatible(generator.ActiveTileSet.Tiles[generator.GetDomain(x, y + 1)[0]], WFCDirection.North), "Adyacencia vertical valida.");
                }
        }

        private void SetSize(int width, int height)
        {
            Input("Width").text = width.ToString();
            Input("Height").text = height.ToString();
            Click("Inicializar / reiniciar");
        }

        private void SetSeed(int seed)
        {
            typeof(WFCGenerator).GetField("randomSeed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(generator, false);
            typeof(WFCGenerator).GetField("seed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(generator, seed);
        }

        private InputField Input(string name) => view.GetComponentsInChildren<InputField>().Single(input => input.name == name);
        private Button Button(string name) => view.GetComponentsInChildren<Button>().Single(button => button.name == name);
        private int[] CellIds() => view.GetComponentsInChildren<RectTransform>().Where(rect => rect.name.StartsWith("Cell "))
            .Select(rect => rect.GetInstanceID()).OrderBy(id => id).ToArray();
        private void Click(string name) => Click(Button(name));

        private void Click(Button button)
        {
            Check(button.interactable, "Control habilitado: " + button.name);
            Canvas.ForceUpdateCanvases();
            view.SendMessage("LateUpdate");
            RectTransform rect = button.transform as RectTransform;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                position = position,
                button = PointerEventData.InputButton.Left,
                eligibleForClick = true
            };
            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
                "El control recibe clics en pantalla: " + button.name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        }

        private void Capture(string name, bool requireTerrain = true)
        {
            Canvas canvas = view.GetComponentInChildren<Canvas>();
            Camera camera = Camera.main;
            RenderMode mode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            float distance = canvas.planeDistance;
            RenderTexture oldTarget = camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(1280, 800, 24);
            Texture2D image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                view.SendMessage("LateUpdate");
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
                else RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                image.Apply();
                if (requireTerrain)
                {
                    bool terrain = generator.ActiveTileSetIndex == 0
                        ? image.GetPixels32().Any(pixel => pixel.g > 100 && pixel.g > pixel.r * 1.4f && pixel.g > pixel.b * 1.2f)
                        : image.GetPixels32().Any(pixel => pixel.r > 90 && pixel.r > pixel.g * 1.1f && pixel.g > pixel.b * 1.1f);
                    Check(terrain, "El ejecutable renderiza los sprites: " + name);
                }
                File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = oldActive;
                canvas.renderMode = mode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = distance;
                camera.targetTexture = oldTarget;
                RenderTexture.ReleaseTemporary(target);
                Destroy(image);
                Canvas.ForceUpdateCanvases();
                view.SendMessage("LateUpdate");
            }
        }

        private void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            result.assertions++;
            result.checks.Add(message);
        }

        private void RecordError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                result.errors.Add(message + "\n" + stack);
        }

        private static string Argument(string key, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : fallback;
        }
    }
}
#endif
