using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PCG.Sudoku
{
    public enum SudokuGenerationMode
    {
        RandomStart,
        Genetic,
        GeneticThenAnnealing
    }

    /// <summary>Lee la interfaz, genera la colección y muestra el candidato seleccionado.</summary>
    [DisallowMultipleComponent]
    public sealed class SudokuUIController : MonoBehaviour
    {
        [Header("Tablero y generación")]
        [SerializeField] private SudokuBoardView boardView;
        [SerializeField] private Slider boardCountSlider;
        [SerializeField] private TMP_Text boardCountLabel;
        [SerializeField] private Toggle fixedSeedToggle;
        [SerializeField] private TMP_InputField seedInput;
        [SerializeField] private Button generateButton;
        [Tooltip("Generar siempre ejecuta Random Start, GA y SA; los botones de técnicas solo abren sus parámetros.")]
        [SerializeField] private bool fullPipelineOnly;

        [Header("Resultados")]
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Header("Selección de técnica")]
        [SerializeField] private Button randomStartButton;
        [SerializeField] private Button geneticButton;
        [SerializeField] private Button annealingButton;
        [SerializeField] private GameObject randomStartPanel;
        [SerializeField] private GameObject geneticPanel;
        [SerializeField] private GameObject annealingPanel;

        [Header("Algoritmo Genético")]
        [SerializeField] private SudokuGeneticA geneticAlgorithm;
        [SerializeField] private Slider gaPopulationSlider;
        [SerializeField] private TMP_Text gaPopulationLabel;
        [SerializeField] private Slider gaGenerationsSlider;
        [SerializeField] private TMP_Text gaGenerationsLabel;
        [SerializeField] private Slider gaCrossoverSlider;
        [SerializeField] private TMP_Text gaCrossoverLabel;
        [SerializeField] private Slider gaMutationSlider;
        [SerializeField] private TMP_Text gaMutationLabel;
        [SerializeField] private Slider gaTournamentSlider;
        [SerializeField] private TMP_Text gaTournamentLabel;
        [SerializeField] private Slider gaEliteSlider;
        [SerializeField] private TMP_Text gaEliteLabel;

        [Header("Simulated Annealing después del genético")]
        [SerializeField] private SudokuSimulatedAnnealing simulatedAnnealing;
        [SerializeField] private Slider saInitialTemperatureSlider;
        [SerializeField] private TMP_Text saInitialTemperatureLabel;
        [SerializeField] private Slider saCoolingSlider;
        [SerializeField] private TMP_Text saCoolingLabel;
        [SerializeField] private Slider saMinimumTemperatureSlider;
        [SerializeField] private TMP_Text saMinimumTemperatureLabel;
        [SerializeField] private Slider saMaxStepsSlider;
        [SerializeField] private TMP_Text saMaxStepsLabel;

        [Header("Modo de juego")]
        [SerializeField] private SudokuPlayController playController;
        [SerializeField] private Button playButton;
        [SerializeField] private Slider removeCountSlider;
        [SerializeField] private TMP_Text removeCountLabel;
        private const int DefaultRemoveCount = 40;

        private List<int[,]> boards = new List<int[,]>();
        private int currentIndex = -1;
        private Color statusColor;
        private SudokuGenerationMode selectedMode;
        private SudokuGenerationMode lastGeneratedMode;
        private int annealingInitialCost;
        private int annealingBestCost;
        private int annealingSteps;
        private AnnealingStopReason annealingStopReason;

        public int BoardCount => boards.Count;
        public int CurrentBoardIndex => currentIndex;
        public int? LastSeed { get; private set; }
        public int? LastAnnealingSeed { get; private set; }
        public SudokuGenerationMode SelectedMode => selectedMode;

        private void Awake()
        {
            if (boardView == null || boardCountSlider == null || fixedSeedToggle == null ||
                seedInput == null || generateButton == null)
            {
                Debug.LogError("SudokuUIController: faltan referencias de la interfaz.", this);
                enabled = false;
                return;
            }
            if (statusLabel != null)
                statusColor = statusLabel.color;
            seedInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            BindGeneticControls();
            BindAnnealingControls();
            BindPlayControls();
            SelectRandomStart();
            if (geneticButton != null)
                geneticButton.interactable = true;
            if (annealingButton != null)
                annealingButton.interactable = true;
        }

        private void OnEnable()
        {
            generateButton.onClick.AddListener(Generate);
            boardCountSlider.onValueChanged.AddListener(UpdateBoardCountLabel);
            fixedSeedToggle.onValueChanged.AddListener(UpdateSeedInput);
            if (previousButton != null) previousButton.onClick.AddListener(PreviousBoard);
            if (nextButton != null) nextButton.onClick.AddListener(NextBoard);
            if (randomStartButton != null) randomStartButton.onClick.AddListener(SelectRandomStart);
            if (geneticButton != null) geneticButton.onClick.AddListener(SelectGeneticParameters);
            if (annealingButton != null) annealingButton.onClick.AddListener(SelectAnnealingParameters);
            if (playButton != null) playButton.onClick.AddListener(PlayCurrentBoard);
            if (playController != null) playController.StatusChanged += OnPlayStatusChanged;

            RegisterGeneticListeners();
            RegisterAnnealingListeners();
            RegisterPlayListeners();

            UpdateBoardCountLabel(boardCountSlider.value);
            UpdateSeedInput(fixedSeedToggle.isOn);
            UpdateAllGeneticLabels();
            UpdateAllAnnealingLabels();
            UpdateAllPlayLabels();

            if (boards.Count == 0)
                SetStatus(fullPipelineOnly
                    ? "Ajusta los parámetros y pulsa Generar: Random + GA + SA."
                    : "Selecciona Random Start, Genetic o GA + SA y pulsa Generar.");
            else
                ShowCurrentBoard();
            UpdateNavigation();
        }

        private void OnDisable()
        {
            if (generateButton != null) generateButton.onClick.RemoveListener(Generate);
            if (boardCountSlider != null) boardCountSlider.onValueChanged.RemoveListener(UpdateBoardCountLabel);
            if (fixedSeedToggle != null) fixedSeedToggle.onValueChanged.RemoveListener(UpdateSeedInput);
            if (previousButton != null) previousButton.onClick.RemoveListener(PreviousBoard);
            if (nextButton != null) nextButton.onClick.RemoveListener(NextBoard);
            if (randomStartButton != null) randomStartButton.onClick.RemoveListener(SelectRandomStart);
            if (geneticButton != null) geneticButton.onClick.RemoveListener(SelectGeneticParameters);
            if (annealingButton != null) annealingButton.onClick.RemoveListener(SelectAnnealingParameters);
            if (playButton != null) playButton.onClick.RemoveListener(PlayCurrentBoard);
            if (playController != null) playController.StatusChanged -= OnPlayStatusChanged;

            UnregisterGeneticListeners();
            UnregisterAnnealingListeners();
            UnregisterPlayListeners();
        }

        public void Generate()
        {
            // Salir del modo juego antes de reemplazar el tablero mostrado.
            if (playController != null) playController.StopPuzzle();

            int seed;
            if (fixedSeedToggle.isOn)
            {
                if (!int.TryParse(seedInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
                {
                    SetStatus("Escribe una semilla entera válida (por ejemplo, 12345).", true);
                    return;
                }
            }
            else
            {
                // Independiente del estado aleatorio global de Unity; conservar la semilla real.
                do { seed = Guid.NewGuid().GetHashCode(); } while (LastSeed == seed);
            }

            if (fullPipelineOnly)
            {
                // GA crea su población con Random Start; SA recibe el mejor candidato de GA.
                GenerateGenetic(seed, true);
                return;
            }

            switch (selectedMode)
            {
                case SudokuGenerationMode.Genetic:
                    GenerateGenetic(seed, false);
                    break;
                case SudokuGenerationMode.GeneticThenAnnealing:
                    GenerateGenetic(seed, true);
                    break;
                default:
                    GenerateRandomStart(seed);
                    break;
            }
        }

        private void GenerateRandomStart(int seed)
        {
            float requestedCount = boardCountSlider.value;
            if (float.IsNaN(requestedCount) || float.IsInfinity(requestedCount) ||
                requestedCount < 1 || requestedCount > SudokuRandomStartState.MaxBoardCount)
            {
                SetStatus($"La cantidad debe estar entre 1 y {SudokuRandomStartState.MaxBoardCount}.", true);
                return;
            }

            boards = SudokuRandomStartState.Generate(Mathf.RoundToInt(requestedCount), seed);
            LastSeed = seed;
            currentIndex = 0;
            lastGeneratedMode = SudokuGenerationMode.RandomStart;
            LastAnnealingSeed = null;
            seedInput.SetTextWithoutNotify(seed.ToString(CultureInfo.InvariantCulture));
            ShowCurrentBoard();
        }

        private void GenerateGenetic(int seed, bool improveWithAnnealing)
        {
            if (geneticAlgorithm == null)
            {
                geneticAlgorithm = FindObjectOfType<SudokuGeneticA>();
                if (geneticAlgorithm == null)
                    geneticAlgorithm = gameObject.AddComponent<SudokuGeneticA>();
            }

            if (gaPopulationSlider == null || gaGenerationsSlider == null || gaCrossoverSlider == null ||
                gaMutationSlider == null || gaTournamentSlider == null || gaEliteSlider == null)
            {
                SetStatus("Faltan controles del algoritmo genético en la interfaz.", true);
                return;
            }

            int population = Mathf.RoundToInt(gaPopulationSlider.value);
            int generations = Mathf.RoundToInt(gaGenerationsSlider.value);
            float crossover = gaCrossoverSlider.value;
            float mutation = gaMutationSlider.value;
            int tournament = Mathf.RoundToInt(gaTournamentSlider.value);
            int elites = Mathf.RoundToInt(gaEliteSlider.value);

            if (population < 2 || population > SudokuRandomStartState.MaxBoardCount || generations < 1 ||
                tournament < 1 || tournament > population || elites < 0 || elites >= population ||
                float.IsNaN(crossover) || crossover < 0f || crossover > 1f ||
                float.IsNaN(mutation) || mutation < 0f || mutation > 1f)
            {
                SetStatus("Parámetros inválidos: revisa población, generaciones y probabilidades.\n" +
                    "El torneo no debe superar la población y el elitismo debe ser menor.", true);
                return;
            }

            // Entregar los seis valores visibles; no sustituir controles ausentes por valores del Inspector.
            geneticAlgorithm.SetParameters(population, generations, crossover, mutation, tournament, elites);

            // Rechazar parámetros locales inválidos antes de invertir tiempo en el genético.
            if (improveWithAnnealing && !ConfigureAnnealing()) return;

            SudokuCandidate result = geneticAlgorithm.Generate(seed);
            if (result != null && result.board != null)
            {
                var geneticBoard = (int[,])result.board.Clone();
                if (improveWithAnnealing)
                {
                    // Derivación estable: una ejecución es reproducible sin compartir la secuencia RNG de GA.
                    int annealingSeed = unchecked((seed * 397) ^ 0x5341);
                    SudokuCandidate improved = simulatedAnnealing.Improve(result, annealingSeed);
                    boards = new List<int[,]> { geneticBoard, (int[,])improved.board.Clone() };
                    annealingInitialCost = simulatedAnnealing.InitialCost;
                    annealingBestCost = simulatedAnnealing.BestCost;
                    annealingSteps = simulatedAnnealing.StepsPerformed;
                    annealingStopReason = simulatedAnnealing.StopReason;
                    LastAnnealingSeed = annealingSeed;
                    lastGeneratedMode = SudokuGenerationMode.GeneticThenAnnealing;
                    currentIndex = 1;
                }
                else
                {
                    boards = new List<int[,]> { geneticBoard };
                    LastAnnealingSeed = null;
                    lastGeneratedMode = SudokuGenerationMode.Genetic;
                    currentIndex = 0;
                }
                LastSeed = seed;
                seedInput.SetTextWithoutNotify(seed.ToString(CultureInfo.InvariantCulture));
                ShowCurrentBoard();
            }
            else
            {
                SetStatus("El algoritmo genético no devolvió ningún candidato.", true);
            }
        }

        public void PreviousBoard()
        {
            if (currentIndex <= 0) return;
            if (playController != null) playController.StopPuzzle();
            currentIndex--;
            ShowCurrentBoard();
        }

        public void NextBoard()
        {
            if (currentIndex < 0 || currentIndex >= boards.Count - 1) return;
            if (playController != null) playController.StopPuzzle();
            currentIndex++;
            ShowCurrentBoard();
        }

        /// <summary>Convierte el tablero mostrado (debe tener cero conflictos) en un puzzle jugable, vaciando celdas al azar.</summary>
        public void PlayCurrentBoard()
        {
            if (currentIndex < 0 || currentIndex >= boards.Count)
            {
                SetStatus("Genera un tablero completo antes de jugar.", true);
                return;
            }

            int[,] current = boards[currentIndex];
            SudokuEvaluation evaluation;
            try
            {
                evaluation = SudokuEvaluator.Evaluate(current);
            }
            catch (ArgumentException)
            {
                SetStatus("El tablero mostrado no está completo; no se puede usar para jugar.", true);
                return;
            }

            if (!evaluation.IsValid)
            {
                SetStatus($"El tablero mostrado tiene {evaluation.TotalConflicts} conflictos; resuélvelo antes de jugar.", true);
                return;
            }

            if (playController == null)
            {
                playController = FindObjectOfType<SudokuPlayController>();
                if (playController == null)
                    playController = gameObject.AddComponent<SudokuPlayController>();
                playController.StatusChanged += OnPlayStatusChanged;
            }

            int removeCount = removeCountSlider != null ? Mathf.RoundToInt(removeCountSlider.value) : DefaultRemoveCount;
            int seed = LastSeed ?? SudokuRandomStartState.DefaultSeed;
            playController.StartPuzzle(current, removeCount, seed);
        }

        private void OnPlayStatusChanged(string message)
        {
            SetStatus(message);
        }

        /// <summary>Entrega una copia independiente para futuras búsquedas, sin alterar el estado inicial.</summary>
        public int[,] GetBoardCopy(int index)
        {
            if (index < 0 || index >= boards.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return (int[,])boards[index].Clone();
        }

        public void SelectRandomStart()
        {
            selectedMode = fullPipelineOnly ? SudokuGenerationMode.GeneticThenAnnealing : SudokuGenerationMode.RandomStart;
            ShowParameterPanel(randomStartPanel);
        }

        public void SelectGeneticParameters()
        {
            selectedMode = fullPipelineOnly ? SudokuGenerationMode.GeneticThenAnnealing : SudokuGenerationMode.Genetic;
            ShowParameterPanel(geneticPanel);
        }

        public void SelectAnnealingParameters()
        {
            selectedMode = SudokuGenerationMode.GeneticThenAnnealing;
            ShowParameterPanel(annealingPanel);
        }

        private static void ShowParameterPanel(GameObject selectedPanel)
        {
            if (selectedPanel == null || selectedPanel.transform.parent == null) return;

            // Mostrar solamente el panel seleccionado entre los hijos de ParametersBackground.
            foreach (Transform panel in selectedPanel.transform.parent)
                panel.gameObject.SetActive(panel.gameObject == selectedPanel);
        }

        private void ShowCurrentBoard()
        {
            boardView.SetBoard(boards[currentIndex]);
            SudokuEvaluation evaluation = SudokuEvaluator.Evaluate(boards[currentIndex]);
            string state = lastGeneratedMode == SudokuGenerationMode.RandomStart ? "Estado inicial" :
                lastGeneratedMode == SudokuGenerationMode.Genetic ? "Genetic" :
                currentIndex == 0 ? "GA (antes de SA)" :
                fullPipelineOnly ? "Random + GA + SA" : "GA + SA";
            if (evaluation.IsValid) state += " | Válido";

            string status = $"{state} {currentIndex + 1}/{boards.Count} | Semilla: {LastSeed}\n" +
                $"Conflictos: {evaluation.TotalConflicts} | Filas {evaluation.RowConflicts}, " +
                $"Cols {evaluation.ColumnConflicts}, Bloques {evaluation.BlockConflicts}";
            if (lastGeneratedMode == SudokuGenerationMode.GeneticThenAnnealing)
                status += $"\nSA: {annealingInitialCost} -> {annealingBestCost} | {annealingSteps} pasos | {StopReasonText(annealingStopReason)}";
            SetStatus(status);
            UpdateNavigation();
        }

        private void BindGeneticControls()
        {
            if (geneticAlgorithm == null)
            {
                geneticAlgorithm = FindObjectOfType<SudokuGeneticA>();
                if (geneticAlgorithm == null)
                    geneticAlgorithm = gameObject.AddComponent<SudokuGeneticA>();
            }

            if (geneticPanel != null)
            {
                BindControl(geneticPanel, ref gaPopulationSlider, ref gaPopulationLabel, "GAPoblation");
                BindControl(geneticPanel, ref gaGenerationsSlider, ref gaGenerationsLabel, "GAGenerations");
                BindControl(geneticPanel, ref gaCrossoverSlider, ref gaCrossoverLabel, "GACrossover");
                BindControl(geneticPanel, ref gaMutationSlider, ref gaMutationLabel, "GAMutacion");
                BindControl(geneticPanel, ref gaTournamentSlider, ref gaTournamentLabel, "GATorneo");
                BindControl(geneticPanel, ref gaEliteSlider, ref gaEliteLabel, "GAElitismo");
            }
        }

        private void BindAnnealingControls()
        {
            if (simulatedAnnealing == null)
            {
                simulatedAnnealing = FindObjectOfType<SudokuSimulatedAnnealing>();
                if (simulatedAnnealing == null)
                    simulatedAnnealing = gameObject.AddComponent<SudokuSimulatedAnnealing>();
            }
            if (annealingPanel == null) return;
            BindControl(annealingPanel, ref saInitialTemperatureSlider, ref saInitialTemperatureLabel, "SA_InitialTemperature");
            BindControl(annealingPanel, ref saCoolingSlider, ref saCoolingLabel, "SA_CooldownFactor");
            BindControl(annealingPanel, ref saMinimumTemperatureSlider, ref saMinimumTemperatureLabel, "SA_MinimumTemperature");
            BindControl(annealingPanel, ref saMaxStepsSlider, ref saMaxStepsLabel, "SA_MaxSteps");
        }

        private bool ConfigureAnnealing()
        {
            if (simulatedAnnealing == null || saInitialTemperatureSlider == null || saCoolingSlider == null ||
                saMinimumTemperatureSlider == null || saMaxStepsSlider == null)
            {
                SetStatus("Faltan referencias de Simulated Annealing en la interfaz.", true);
                return false;
            }
            try
            {
                simulatedAnnealing.SetParameters(saInitialTemperatureSlider.value, saCoolingSlider.value,
                    saMinimumTemperatureSlider.value, Mathf.RoundToInt(saMaxStepsSlider.value));
                return true;
            }
            catch (ArgumentException)
            {
                SetStatus("Revisa SA: temperatura inicial > mínima > 0,\n" +
                    "enfriamiento entre 0 y 1 y pasos positivos.", true);
                return false;
            }
        }

        private void BindPlayControls()
        {
            if (playController == null)
            {
                playController = FindObjectOfType<SudokuPlayController>();
                if (playController == null)
                    playController = gameObject.AddComponent<SudokuPlayController>();
            }
        }

        private void RegisterPlayListeners()
        {
            if (removeCountSlider != null) removeCountSlider.onValueChanged.AddListener(UpdateRemoveCountLabel);
        }

        private void UnregisterPlayListeners()
        {
            if (removeCountSlider != null) removeCountSlider.onValueChanged.RemoveListener(UpdateRemoveCountLabel);
        }

        private void UpdateAllPlayLabels()
        {
            if (removeCountSlider != null) UpdateRemoveCountLabel(removeCountSlider.value);
        }

        private void UpdateRemoveCountLabel(float value)
        {
            if (removeCountLabel != null) removeCountLabel.text = Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
        }

        private static string StopReasonText(AnnealingStopReason reason)
        {
            switch (reason)
            {
                case AnnealingStopReason.AlreadySolved: return "GA ya resuelto";
                case AnnealingStopReason.SolutionFound: return "Cero conflictos";
                case AnnealingStopReason.StepLimit: return "Límite de pasos";
                default: return "Temperatura mínima";
            }
        }

        private void BindControl(GameObject panel, ref Slider slider, ref TMP_Text label, string childName)
        {
            Transform t = panel.transform.Find(childName);
            if (t == null) return;
            if (slider == null)
            {
                Transform s = t.Find("Slider");
                if (s != null) slider = s.GetComponent<Slider>();
            }
            if (label == null)
            {
                Transform c = t.Find("Cantidad");
                if (c != null) label = c.GetComponent<TMP_Text>();
            }
        }

        private void RegisterGeneticListeners()
        {
            if (gaPopulationSlider != null) gaPopulationSlider.onValueChanged.AddListener(UpdateGAPopulationLabel);
            if (gaGenerationsSlider != null) gaGenerationsSlider.onValueChanged.AddListener(UpdateGAGenerationsLabel);
            if (gaCrossoverSlider != null) gaCrossoverSlider.onValueChanged.AddListener(UpdateGACrossoverLabel);
            if (gaMutationSlider != null) gaMutationSlider.onValueChanged.AddListener(UpdateGAMutationLabel);
            if (gaTournamentSlider != null) gaTournamentSlider.onValueChanged.AddListener(UpdateGATournamentLabel);
            if (gaEliteSlider != null) gaEliteSlider.onValueChanged.AddListener(UpdateGAEliteLabel);
        }

        private void RegisterAnnealingListeners()
        {
            if (saInitialTemperatureSlider != null) saInitialTemperatureSlider.onValueChanged.AddListener(UpdateSAInitialTemperatureLabel);
            if (saCoolingSlider != null) saCoolingSlider.onValueChanged.AddListener(UpdateSACoolingLabel);
            if (saMinimumTemperatureSlider != null) saMinimumTemperatureSlider.onValueChanged.AddListener(UpdateSAMinimumTemperatureLabel);
            if (saMaxStepsSlider != null) saMaxStepsSlider.onValueChanged.AddListener(UpdateSAMaxStepsLabel);
        }

        private void UnregisterAnnealingListeners()
        {
            if (saInitialTemperatureSlider != null) saInitialTemperatureSlider.onValueChanged.RemoveListener(UpdateSAInitialTemperatureLabel);
            if (saCoolingSlider != null) saCoolingSlider.onValueChanged.RemoveListener(UpdateSACoolingLabel);
            if (saMinimumTemperatureSlider != null) saMinimumTemperatureSlider.onValueChanged.RemoveListener(UpdateSAMinimumTemperatureLabel);
            if (saMaxStepsSlider != null) saMaxStepsSlider.onValueChanged.RemoveListener(UpdateSAMaxStepsLabel);
        }

        private void UpdateAllAnnealingLabels()
        {
            if (saInitialTemperatureSlider != null) UpdateSAInitialTemperatureLabel(saInitialTemperatureSlider.value);
            if (saCoolingSlider != null) UpdateSACoolingLabel(saCoolingSlider.value);
            if (saMinimumTemperatureSlider != null) UpdateSAMinimumTemperatureLabel(saMinimumTemperatureSlider.value);
            if (saMaxStepsSlider != null) UpdateSAMaxStepsLabel(saMaxStepsSlider.value);
        }

        private void UpdateSAInitialTemperatureLabel(float value)
        {
            if (saInitialTemperatureLabel != null) saInitialTemperatureLabel.text = value.ToString("F2", CultureInfo.InvariantCulture);
        }

        private void UpdateSACoolingLabel(float value)
        {
            if (saCoolingLabel != null) saCoolingLabel.text = value.ToString("F4", CultureInfo.InvariantCulture);
        }

        private void UpdateSAMinimumTemperatureLabel(float value)
        {
            if (saMinimumTemperatureLabel != null) saMinimumTemperatureLabel.text = value.ToString("F4", CultureInfo.InvariantCulture);
        }

        private void UpdateSAMaxStepsLabel(float value)
        {
            if (saMaxStepsLabel != null) saMaxStepsLabel.text = Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
        }

        private void UnregisterGeneticListeners()
        {
            if (gaPopulationSlider != null) gaPopulationSlider.onValueChanged.RemoveListener(UpdateGAPopulationLabel);
            if (gaGenerationsSlider != null) gaGenerationsSlider.onValueChanged.RemoveListener(UpdateGAGenerationsLabel);
            if (gaCrossoverSlider != null) gaCrossoverSlider.onValueChanged.RemoveListener(UpdateGACrossoverLabel);
            if (gaMutationSlider != null) gaMutationSlider.onValueChanged.RemoveListener(UpdateGAMutationLabel);
            if (gaTournamentSlider != null) gaTournamentSlider.onValueChanged.RemoveListener(UpdateGATournamentLabel);
            if (gaEliteSlider != null) gaEliteSlider.onValueChanged.RemoveListener(UpdateGAEliteLabel);
        }

        private void UpdateAllGeneticLabels()
        {
            if (gaPopulationSlider != null) UpdateGAPopulationLabel(gaPopulationSlider.value);
            if (gaGenerationsSlider != null) UpdateGAGenerationsLabel(gaGenerationsSlider.value);
            if (gaCrossoverSlider != null) UpdateGACrossoverLabel(gaCrossoverSlider.value);
            if (gaMutationSlider != null) UpdateGAMutationLabel(gaMutationSlider.value);
            if (gaTournamentSlider != null) UpdateGATournamentLabel(gaTournamentSlider.value);
            if (gaEliteSlider != null) UpdateGAEliteLabel(gaEliteSlider.value);
        }

        private void UpdateGAPopulationLabel(float val)
        {
            if (gaPopulationLabel != null) gaPopulationLabel.text = Mathf.RoundToInt(val).ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateGAGenerationsLabel(float val)
        {
            if (gaGenerationsLabel != null) gaGenerationsLabel.text = Mathf.RoundToInt(val).ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateGACrossoverLabel(float val)
        {
            if (gaCrossoverLabel != null) gaCrossoverLabel.text = val.ToString("F2", CultureInfo.InvariantCulture);
        }

        private void UpdateGAMutationLabel(float val)
        {
            if (gaMutationLabel != null) gaMutationLabel.text = val.ToString("F2", CultureInfo.InvariantCulture);
        }

        private void UpdateGATournamentLabel(float val)
        {
            if (gaTournamentLabel != null) gaTournamentLabel.text = Mathf.RoundToInt(val).ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateGAEliteLabel(float val)
        {
            if (gaEliteLabel != null) gaEliteLabel.text = Mathf.RoundToInt(val).ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateBoardCountLabel(float value)
        {
            if (boardCountLabel != null)
                boardCountLabel.text = Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateSeedInput(bool useFixedSeed)
        {
            seedInput.interactable = useFixedSeed;
        }

        private void UpdateNavigation()
        {
            if (previousButton != null) previousButton.interactable = currentIndex > 0;
            if (nextButton != null) nextButton.interactable = currentIndex >= 0 && currentIndex < boards.Count - 1;
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (statusLabel == null) return;
            statusLabel.text = message;
            statusLabel.color = isError ? new Color(1f, 0.55f, 0.5f) : statusColor;
        }
    }
}
