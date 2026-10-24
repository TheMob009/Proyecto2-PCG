using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Laboratorios.Lab7.WaveFunctionCollapse
{
    /// <summary>Presenta los dominios y controla WFC sin modificar sus reglas.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WFCGenerator))]
    public sealed class WFCBoardView : MonoBehaviour
    {
        [SerializeField] private bool initializeOnStart = true;
        [SerializeField, Min(0.1f)] private float stepInterval = 0.5f;

        private static readonly Color Background = new Color32(15, 25, 31, 255);
        private static readonly Color Panel = new Color32(26, 41, 49, 255);
        private static readonly Color Neutral = new Color32(55, 75, 85, 255);
        private static readonly Color Observed = new Color32(255, 191, 71, 255);
        private static readonly Color Propagated = new Color32(63, 185, 218, 255);
        private static readonly Color Error = new Color32(239, 91, 103, 255);

        private WFCGenerator generator;
        private Canvas canvas;
        private RectTransform boardHost, board;
        private Font font;
        private Cell[,] cells;
        private int builtWidth, builtHeight, builtPatternCount;
        private WFCTileSet builtTileSet;
        private Vector2 previousSize;
        private Vector2Int selected = new Vector2Int(-1, -1);
        private Text tileSetText, statusText, phaseText, statisticsText, selectionText;
        private Text stepText, playText, speedText;
        private InputField widthInput, heightInput;
        private Button stepButton, playButton, changeTileSetButton;
        private bool playing;
        private float nextStepTime;

        public int VisibleCellCount => cells == null ? 0 : cells.Length;
        public bool IsPlaying => playing;

        private sealed class Cell
        {
            public RectTransform Root;
            public Image Frame, Result;
            public RectTransform Possibilities;
            public readonly List<Image> Previews = new List<Image>();
            public GameObject Badge;
            public Text Count;
        }

        private void Awake()
        {
            generator = GetComponent<WFCGenerator>();
            BuildInterface();
        }

        private void OnEnable()
        {
            generator = GetComponent<WFCGenerator>();
            generator.ObservationsChanged += Refresh;
            if (canvas != null)
            {
                canvas.enabled = true;
                Refresh();
            }
        }

        private void Start()
        {
            if (initializeOnStart && !generator.IsInitialized)
                generator.Initialize();
        }

        private void OnDisable()
        {
            if (generator != null)
                generator.ObservationsChanged -= Refresh;
            playing = false;
            if (canvas != null)
                canvas.enabled = false;
        }

        private void Update()
        {
            if (!playing || Time.unscaledTime < nextStepTime)
                return;

            nextStepTime = Time.unscaledTime + stepInterval;
            generator.Step();
        }

        private void LateUpdate()
        {
            if (boardHost != null && boardHost.rect.size != previousSize)
                FitBoard();
        }

        // La interfaz se crea una vez. Solo se reconstruye la grilla al
        // cambiar las dimensiones o el conjunto de patrones.
        public void BuildInterface()
        {
            if (canvas != null)
                return;

            generator = GetComponent<WFCGenerator>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RectTransform root = CreateRect(transform, "WFC Interface");
            canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 800f);
            scaler.matchWidthOrHeight = 0.5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            AddImage(root, Background);

            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject events = new GameObject("WFC EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(root, false);
            }

            Text title = CreateText(root, "Title", "WAVE FUNCTION COLLAPSE", 26, FontStyle.Bold);
            Place(title.rectTransform, 24, 18, 850, 34);
            Text subtitle = CreateText(root, "Subtitle", "Observa una decision y luego propaga sus restricciones.", 15);
            Place(subtitle.rectTransform, 25, 54, 950, 24);

            RectTransform sidebar = CreateRect(root, "Controls");
            sidebar.anchorMax = new Vector2(0, 1);
            sidebar.offsetMin = new Vector2(20, 20);
            sidebar.offsetMax = new Vector2(320, -92);
            AddImage(sidebar, Panel);

            // El panel se puede desplazar si la ventana es pequena.
            ScrollRect scroll = sidebar.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            RectTransform viewport = CreateRect(sidebar, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 940);
            scroll.viewport = viewport;
            scroll.content = content;

            Label(content, "TILESET", 16, 16, 268, 22, 13, FontStyle.Bold);
            tileSetText = Label(content, "", 16, 40, 268, 32, 19, FontStyle.Bold);
            changeTileSetButton = CreateButton(content, "Cambiar tileset", 16, 80, 268, 34, ChangeTileSet);
            Label(content, "Ancho", 16, 126, 120, 22);
            Label(content, "Alto", 156, 126, 120, 22);
            widthInput = CreateInput(content, "Width", generator.Width.ToString(), 16, 152);
            heightInput = CreateInput(content, "Height", generator.Height.ToString(), 156, 152);
            CreateButton(content, "Inicializar / reiniciar", 16, 202, 268, 38, InitializeFromControls);
            CreateButton(content, "Limpiar", 16, 248, 268, 34, ClearGeneration);
            stepButton = CreateButton(content, "Propagar", 16, 302, 268, 42, StepOnce);
            stepText = stepButton.GetComponentInChildren<Text>();
            playButton = CreateButton(content, "Reproducir", 16, 352, 268, 38, TogglePlayback);
            playText = playButton.GetComponentInChildren<Text>();
            Button speed = CreateButton(content, "Intervalo: 0.5 s", 16, 398, 268, 30, CycleSpeed);
            speedText = speed.GetComponentInChildren<Text>();

            phaseText = Label(content, "", 16, 448, 268, 28, 19, FontStyle.Bold);
            statusText = Label(content, "", 16, 486, 268, 76, 15);
            statisticsText = Label(content, "", 16, 572, 268, 62, 14);
            Label(content, "DETALLE DE CELDA", 16, 656, 268, 24, 13, FontStyle.Bold);
            selectionText = Label(content, "Selecciona una celda para consultar sus patrones posibles.", 16, 690, 268, 230, 14);

            boardHost = CreateRect(root, "Board Area");
            boardHost.offsetMin = new Vector2(344, 100);
            boardHost.offsetMax = new Vector2(-24, -92);
            board = CreateRect(boardHost, "Board");
            board.anchorMin = board.anchorMax = new Vector2(0.5f, 0.5f);

            Text legend = CreateText(root, "Legend", "Amarillo: observacion    Azul: propagacion    Rojo: contradiccion", 14);
            legend.rectTransform.anchorMin = Vector2.zero;
            legend.rectTransform.anchorMax = new Vector2(1, 0);
            legend.rectTransform.offsetMin = new Vector2(344, 54);
            legend.rectTransform.offsetMax = new Vector2(-24, 90);
            Text hint = CreateText(root, "Hint", "Miniaturas = patrones posibles. Numero = cantidad.", 13);
            hint.rectTransform.anchorMin = Vector2.zero;
            hint.rectTransform.anchorMax = new Vector2(1, 0);
            hint.rectTransform.offsetMin = new Vector2(344, 12);
            hint.rectTransform.offsetMax = new Vector2(-24, 52);
            Refresh();
        }

        public void Refresh()
        {
            if (canvas == null)
                return;

            WFCTileSet tileSet = generator.ActiveTileSet;
            int patternCount = tileSet == null ? 0 : tileSet.Tiles.Count;
            if (cells == null || builtWidth != generator.Width || builtHeight != generator.Height ||
                builtPatternCount != patternCount || builtTileSet != tileSet)
                RebuildCells(tileSet, patternCount);

            int resolved = 0;
            int contradictions = 0;
            for (int x = 0; x < builtWidth; x++)
                for (int y = 0; y < builtHeight; y++)
                {
                    IReadOnlyList<int> domain = generator.GetDomain(x, y);
                    if (domain != null && domain.Count == 1) resolved++;
                    if (domain != null && domain.Count == 0) contradictions++;
                    RenderCell(cells[x, y], x, y, domain);
                }

            bool canStep = generator.Phase == WFCPhase.ReadyToObserve || generator.Phase == WFCPhase.Propagating;
            if (!canStep) playing = false;
            stepButton.interactable = canStep && !playing;
            playButton.interactable = canStep;
            stepText.text = generator.Phase == WFCPhase.Propagating ? "Propagar" : "Observar";
            playText.text = playing ? "Pausar" : "Reproducir";
            changeTileSetButton.interactable = generator.TileSetCount > 1;
            speedText.text = $"Intervalo: {stepInterval:0.##} s";
            tileSetText.text = tileSet == null ? "Sin tileset" : tileSet.id;
            phaseText.text = PhaseLabel(generator.Phase);
            phaseText.color = generator.Phase == WFCPhase.Contradiction ? Error : Color.white;
            statusText.text = generator.Status;
            statusText.color = Color.white;
            statisticsText.text = $"Resueltas: {resolved} / {builtWidth * builtHeight}   Vacias: {contradictions}\n" +
                $"Observaciones: {generator.ObservationCount}   Propagaciones: {generator.PropagationCount}\n" +
                $"Celdas modificadas en el ultimo paso: {generator.ChangedCellCount}";
            RefreshSelection();
        }

        private void RebuildCells(WFCTileSet tileSet, int patternCount)
        {
            if (cells != null)
                foreach (Cell cell in cells)
                {
                    cell.Root.gameObject.SetActive(false);
                    Destroy(cell.Root.gameObject);
                }

            builtWidth = generator.Width;
            builtHeight = generator.Height;
            builtPatternCount = patternCount;
            builtTileSet = tileSet;
            selected = new Vector2Int(-1, -1);
            cells = new Cell[builtWidth, builtHeight];
            for (int x = 0; x < builtWidth; x++)
                for (int y = 0; y < builtHeight; y++)
                {
                    Cell cell = new Cell();
                    cell.Root = CreateRect(board, $"Cell {x},{y}");
                    cell.Root.anchorMin = new Vector2((float)x / builtWidth, (float)y / builtHeight);
                    cell.Root.anchorMax = new Vector2((float)(x + 1) / builtWidth, (float)(y + 1) / builtHeight);
                    cell.Root.offsetMin = Vector2.one;
                    cell.Root.offsetMax = -Vector2.one;
                    cell.Frame = AddImage(cell.Root, Neutral);
                    Button button = cell.Root.gameObject.AddComponent<Button>();
                    button.targetGraphic = cell.Frame;
                    button.transition = Selectable.Transition.None;
                    int cellX = x, cellY = y;
                    button.onClick.AddListener(() => SelectCell(cellX, cellY));
                    RectTransform interior = CreateRect(cell.Root, "Interior");
                    interior.offsetMin = Vector2.one * 3;
                    interior.offsetMax = -Vector2.one * 3;
                    AddImage(interior, Background).raycastTarget = false;
                    cell.Result = AddImage(CreateRect(interior, "Assigned Pattern"), Color.white);
                    cell.Result.preserveAspect = true;
                    cell.Result.raycastTarget = false;
                    cell.Possibilities = CreateRect(interior, "Possible Patterns");
                    // Reservar una franja para el contador sin tapar patrones.
                    cell.Possibilities.anchorMax = new Vector2(1, 0.78f);
                    RectTransform badge = CreateRect(cell.Root, "Domain Count");
                    badge.anchorMin = new Vector2(0.45f, 0.78f);
                    badge.anchorMax = Vector2.one;
                    badge.offsetMin = badge.offsetMax = Vector2.zero;
                    AddImage(badge, Background).raycastTarget = false;
                    cell.Count = CreateText(badge, "Count", "", 12, FontStyle.Bold);
                    cell.Count.alignment = TextAnchor.MiddleCenter;
                    cell.Count.resizeTextForBestFit = true;
                    cell.Count.resizeTextMinSize = 6;
                    cell.Count.resizeTextMaxSize = 12;
                    cell.Badge = badge.gameObject;
                    cells[x, y] = cell;
                }
            FitBoard();
        }

        private void RenderCell(Cell cell, int x, int y, IReadOnlyList<int> domain)
        {
            int count = domain == null ? -1 : domain.Count;
            bool resolved = count == 1;
            cell.Result.gameObject.SetActive(resolved);
            if (resolved)
                cell.Result.sprite = generator.ActiveTileSet.Tiles[domain[0]].sprite;
            cell.Possibilities.gameObject.SetActive(count > 1);
            cell.Badge.SetActive(!resolved);
            cell.Count.text = count < 0 ? "-" : count == 0 ? "X" : count.ToString();
            Color border = count == 0 ? Error : Neutral;
            if (count > 0 && generator.WasChanged(x, y))
                border = generator.LastOperation == WFCOperation.Observation ? Observed : Propagated;
            cell.Frame.color = border;

            if (count <= 1)
                return;

            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / columns);
            for (int i = 0; i < count; i++)
            {
                if (i == cell.Previews.Count)
                {
                    Image preview = AddImage(CreateRect(cell.Possibilities, $"Option {i}"), Color.white);
                    preview.raycastTarget = false;
                    preview.preserveAspect = true;
                    cell.Previews.Add(preview);
                }
                Image image = cell.Previews[i];
                image.gameObject.SetActive(true);
                image.sprite = generator.ActiveTileSet.Tiles[domain[i]].sprite;
                RectTransform rect = image.rectTransform;
                int column = i % columns, row = i / columns;
                rect.anchorMin = new Vector2((float)column / columns, 1f - (float)(row + 1) / rows);
                rect.anchorMax = new Vector2((float)(column + 1) / columns, 1f - (float)row / rows);
                rect.offsetMin = Vector2.one * 0.5f;
                rect.offsetMax = -Vector2.one * 0.5f;
            }
            for (int i = count; i < cell.Previews.Count; i++)
                cell.Previews[i].gameObject.SetActive(false);
        }

        private void FitBoard()
        {
            previousSize = boardHost.rect.size;
            float side = Mathf.Max(1, Mathf.Min(previousSize.x / builtWidth, previousSize.y / builtHeight));
            board.sizeDelta = new Vector2(side * builtWidth, side * builtHeight);
        }

        private void SelectCell(int x, int y)
        {
            selected = new Vector2Int(x, y);
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            IReadOnlyList<int> domain = generator.GetDomain(selected.x, selected.y);
            if (domain == null)
            {
                selectionText.text = "Selecciona una celda para consultar sus patrones posibles.\n\nDesliza este panel para ver todo el detalle.";
                selectionText.rectTransform.sizeDelta = new Vector2(268, 230);
                ((RectTransform)selectionText.transform.parent).sizeDelta = new Vector2(0, 950);
                return;
            }
            StringBuilder detail = new StringBuilder();
            detail.Append($"Celda ({selected.x}, {selected.y})\n{domain.Count} patrones posibles:\n");
            foreach (int index in domain)
                detail.Append('\n').Append(generator.ActiveTileSet.Tiles[index].id);
            if (domain.Count == 0)
                detail.Append("\nSin patrones compatibles. Reinicia la generacion.");
            selectionText.text = detail.ToString();
            selectionText.rectTransform.sizeDelta = new Vector2(268, Mathf.Max(230, 52 + domain.Count * 18));
            RectTransform content = selectionText.transform.parent as RectTransform;
            content.sizeDelta = new Vector2(0, Mathf.Max(940, 720 + selectionText.rectTransform.sizeDelta.y));
        }

        private void InitializeFromControls()
        {
            playing = false;
            if (!int.TryParse(widthInput.text, out int width) || !int.TryParse(heightInput.text, out int height) ||
                width < 1 || width > 32 || height < 1 || height > 32)
            {
                Refresh();
                statusText.text = "Escribe un ancho y un alto entre 1 y 32.";
                statusText.color = Error;
                return;
            }
            generator.ConfigureSize(width, height);
            generator.Initialize();
        }

        private void ClearGeneration()
        {
            playing = false;
            generator.Clear();
        }

        private void ChangeTileSet()
        {
            playing = false;
            if (generator.TileSetCount > 0)
                generator.SelectTileSet((generator.ActiveTileSetIndex + 1) % generator.TileSetCount);
        }

        private void StepOnce()
        {
            playing = false;
            generator.Step();
        }

        private void TogglePlayback()
        {
            playing = !playing;
            nextStepTime = Time.unscaledTime + stepInterval;
            Refresh();
        }

        private void CycleSpeed()
        {
            stepInterval = stepInterval < 0.3f ? 0.5f : stepInterval < 0.8f ? 1f : 0.15f;
            Refresh();
        }

        private static string PhaseLabel(WFCPhase phase)
        {
            switch (phase)
            {
                case WFCPhase.ReadyToObserve: return "Siguiente: observar";
                case WFCPhase.Propagating: return "Siguiente: propagar";
                case WFCPhase.Completed: return "Generacion completada";
                case WFCPhase.Contradiction: return "Contradiccion";
                default: return "Sin inicializar";
            }
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Image AddImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(Transform parent, string name, string value, int size, FontStyle style = FontStyle.Normal)
        {
            Text text = CreateRect(parent, name).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.raycastTarget = false;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Text Label(Transform parent, string value, float x, float y, float width, float height,
            int size = 15, FontStyle style = FontStyle.Normal)
        {
            Text text = CreateText(parent, "Label", value, size, style);
            Place(text.rectTransform, x, y, width, height);
            return text;
        }

        private Button CreateButton(Transform parent, string label, float x, float y, float width, float height,
            UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateRect(parent, label);
            Place(rect, x, y, width, height);
            Image background = AddImage(rect, new Color32(48, 78, 90, 255));
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(action);
            Text text = CreateText(rect, "Text", label, 16, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private InputField CreateInput(Transform parent, string name, string value, float x, float y)
        {
            RectTransform rect = CreateRect(parent, name);
            Place(rect, x, y, 128, 34);
            Image background = AddImage(rect, Background);
            Text text = CreateText(rect, "Value", value, 18);
            text.alignment = TextAnchor.MiddleCenter;
            InputField input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = text;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = 2;
            input.text = value;
            return input;
        }
    }
}
