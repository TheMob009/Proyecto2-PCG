using System;
using System.Collections;
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

        [Header("Simulated Annealing: generación de pistas")]
        [SerializeField] private SudokuSimulatedAnnealing simulatedAnnealing;
        [SerializeField] private Slider saInitialTemperatureSlider;
        [SerializeField] private TMP_Text saInitialTemperatureLabel;
        [SerializeField] private Slider saCoolingSlider;
        [SerializeField] private TMP_Text saCoolingLabel;
        [SerializeField] private Slider saMinimumTemperatureSlider;
        [SerializeField] private TMP_Text saMinimumTemperatureLabel;
        [SerializeField] private Slider saMaxStepsSlider;
        [SerializeField] private TMP_Text saMaxStepsLabel;
        [SerializeField] private Slider targetCluesSlider;
        [SerializeField] private TMP_Text targetCluesLabel;
        [SerializeField] private Slider targetDifficultySlider;
        [SerializeField] private TMP_Text targetDifficultyLabel;
        [SerializeField] private Slider saRestartsSlider;
        [SerializeField] private TMP_Text saRestartsLabel;
        [SerializeField] private Slider clueWeightSlider;
        [SerializeField] private TMP_Text clueWeightLabel;
        [SerializeField] private Slider difficultyWeightSlider;
        [SerializeField] private TMP_Text difficultyWeightLabel;

        [Header("Modo de juego")]
        [SerializeField] private SudokuPlayController playController;
        [SerializeField] private Button playButton;

        private List<int[,]> boards = new List<int[,]>();
        private int currentIndex = -1;
        private Color statusColor;
        private SudokuGenerationMode selectedMode;
        private SudokuGenerationMode lastGeneratedMode;
        private SudokuPuzzleResult puzzleResult;
        private Coroutine generation;
        private bool isGenerating;

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
            // Solo amplía el rango; conserva los valores guardados hasta elegir otro nivel.
            if (saMaxStepsSlider != null) saMaxStepsSlider.maxValue = Math.Max(saMaxStepsSlider.maxValue, 30000);
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
            RegisterPuzzleListeners();

            UpdateBoardCountLabel(boardCountSlider.value);
            UpdateSeedInput(fixedSeedToggle.isOn);
            UpdateAllGeneticLabels();
            UpdateAllAnnealingLabels();
            UpdatePuzzleLabels();

            if (boards.Count == 0)
                SetStatus(fullPipelineOnly
                    ? "Genera una solución con GA y sus pistas con SA.\nAjusta cantidad y dificultad en el panel Pistas · SA."
                    : "Selecciona Random Start, Genetic o SA de pistas y pulsa Generar.");
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
            UnregisterPuzzleListeners();
            if (generation != null) StopCoroutine(generation);
            generation = null;
            isGenerating = false;
            if (generateButton != null) generateButton.interactable = true;
        }

        public void Generate()
        {
            if (isGenerating) return;
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
                // GA produce una solución completa; SA selecciona las pistas de la partida.
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
            puzzleResult = null;
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

        private void GenerateGenetic(int seed, bool generatePuzzle)
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

            if (generatePuzzle && !ConfigureAnnealing()) return;
            isGenerating = true;
            generateButton.interactable = false;
            UpdateNavigation();
            generation = StartCoroutine(GenerateRoutine(seed, generatePuzzle));
        }

        private IEnumerator GenerateRoutine(int seed, bool generatePuzzle)
        {
            SudokuPuzzleSettings settings = generatePuzzle ? simulatedAnnealing.Settings : null;
            puzzleResult = null;
            LastAnnealingSeed = null;
            SetStatus("Generando solución completa con GA...");
            yield return null;
            try
            {
                SudokuCandidate candidate = geneticAlgorithm.Generate(seed);
                if (candidate == null || candidate.board == null)
                {
                    SetStatus("El algoritmo genético no devolvió ningún candidato.", true);
                    yield break;
                }
                var solved = (int[,])candidate.board.Clone();
                boards = new List<int[,]> { solved };
                currentIndex = 0;
                lastGeneratedMode = SudokuGenerationMode.Genetic;
                LastSeed = seed;
                seedInput.SetTextWithoutNotify(seed.ToString(CultureInfo.InvariantCulture));
                if (!generatePuzzle) { ShowCurrentBoard(); yield break; }
                if (!SudokuEvaluator.Evaluate(solved).IsValid)
                {
                    boardView.SetBoard(solved);
                    SetStatus("GA no obtuvo una solución completa válida. Ajusta GA o genera otra semilla para crear pistas.", true);
                    yield break;
                }
                int annealingSeed = unchecked((seed * 397) ^ 0x5341);
                LastAnnealingSeed = annealingSeed;
                IEnumerator clues = GenerateCluesRoutine(solved, settings, annealingSeed);
                while (clues.MoveNext()) yield return clues.Current;
            }
            finally
            {
                isGenerating = false;
                generation = null;
                generateButton.interactable = true;
                UpdateNavigation();
            }
        }

        private IEnumerator GenerateCluesRoutine(int[,] solved, SudokuPuzzleSettings settings, int seed)
        {
            var search = new SudokuPuzzleGenerator(solved, settings, seed);
            var slice = System.Diagnostics.Stopwatch.StartNew();
            foreach (SudokuGenerationProgress progress in search.Run())
            {
                if (slice.ElapsedMilliseconds < 8) continue;
                SetStatus($"SA: {progress.Phase} | {progress.Steps} intentos\n" +
                    $"Mejor: {progress.Clues} pistas | costo {progress.Cost:F4}");
                yield return null;
                slice.Restart();
            }
            puzzleResult = search.Result;
            boards = new List<int[,]> { (int[,])solved.Clone(), puzzleResult.Puzzle };
            currentIndex = 1;
            lastGeneratedMode = SudokuGenerationMode.GeneticThenAnnealing;
            ShowCurrentBoard();
        }

        public void PreviousBoard()
        {
            if (isGenerating) return;
            if (currentIndex <= 0) return;
            if (playController != null) playController.StopPuzzle();
            currentIndex--;
            ShowCurrentBoard();
        }

        public void NextBoard()
        {
            if (isGenerating) return;
            if (currentIndex < 0 || currentIndex >= boards.Count - 1) return;
            if (playController != null) playController.StopPuzzle();
            currentIndex++;
            ShowCurrentBoard();
        }

        /// <summary>Abre la partida con el mismo conjunto de pistas generado y evaluado por SA.</summary>
        public void PlayCurrentBoard()
        {
            if (isGenerating || currentIndex != 1 || puzzleResult == null || !puzzleResult.CanPlay)
            {
                SetStatus("Genera y selecciona un puzzle de pistas resoluble mediante el repertorio lógico.", true);
                return;
            }

            if (playController == null)
            {
                playController = FindObjectOfType<SudokuPlayController>();
                if (playController == null)
                    playController = gameObject.AddComponent<SudokuPlayController>();
                playController.StatusChanged += OnPlayStatusChanged;
            }

            playController.StartPuzzle(puzzleResult);
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
            if (currentIndex < 0 || currentIndex >= boards.Count) return;
            boardView.SetBoard(boards[currentIndex]);
            boardView.ResetCellStyles();
            if (currentIndex == 1 && puzzleResult != null)
            {
                string achieved = puzzleResult.Logic.Difficulty == SudokuDifficulty.Unrated ? "Sin clasificar" :
                    SudokuLogicalEvaluator.Label(puzzleResult.Logic.Difficulty);
                string wanted = SudokuLogicalEvaluator.Label(puzzleResult.TargetDifficulty);
                string status = $"Pistas {puzzleResult.ClueCount}/{puzzleResult.TargetClues} · {achieved} (objetivo: {wanted})\n" +
                    $"Solución única · Semilla {LastSeed}\n" +
                    $"Costo {puzzleResult.BestCost:F3} · {puzzleResult.StepsPerformed} intentos · {puzzleResult.RestartsPerformed} reinicios\n" +
                    (puzzleResult.TargetsReached ? StopReasonText(puzzleResult.StopReason) :
                        $"Objetivos pendientes · {StopReasonText(puzzleResult.StopReason)}");
                SetStatus(status, !puzzleResult.TargetsReached);
            }
            else
            {
                SudokuEvaluation evaluation = SudokuEvaluator.Evaluate(boards[currentIndex]);
                string state = lastGeneratedMode == SudokuGenerationMode.RandomStart ? "Estado inicial" : "Solución de GA";
                SetStatus($"{state} {currentIndex + 1}/{boards.Count} | Semilla: {LastSeed}\n" +
                    $"Conflictos: {evaluation.TotalConflicts} | Filas {evaluation.RowConflicts}, " +
                    $"Cols {evaluation.ColumnConflicts}, Bloques {evaluation.BlockConflicts}");
            }
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
            BindControl(annealingPanel, ref targetCluesSlider, ref targetCluesLabel, "SA_TargetClues");
            BindControl(annealingPanel, ref targetDifficultySlider, ref targetDifficultyLabel, "SA_TargetDifficulty");
            BindControl(annealingPanel, ref saRestartsSlider, ref saRestartsLabel, "SA_Restarts");
            BindControl(annealingPanel, ref clueWeightSlider, ref clueWeightLabel, "SA_ClueWeight");
            BindControl(annealingPanel, ref difficultyWeightSlider, ref difficultyWeightLabel, "SA_DifficultyWeight");
            if (targetDifficultySlider != null)
                foreach (TMP_Text label in targetDifficultySlider.transform.parent.GetComponentsInChildren<TMP_Text>(true))
                    if (label != targetDifficultyLabel)
                        label.text = simulatedAnnealing.UseDifficultyPresets ? "Perfil de dificultad" : "Dificultad";
        }

        private bool ConfigureAnnealing()
        {
            if (simulatedAnnealing == null || saInitialTemperatureSlider == null || saCoolingSlider == null ||
                saMinimumTemperatureSlider == null || saMaxStepsSlider == null || targetCluesSlider == null ||
                targetDifficultySlider == null || saRestartsSlider == null || clueWeightSlider == null || difficultyWeightSlider == null)
            {
                SetStatus("Faltan referencias de Simulated Annealing en la interfaz.", true);
                return false;
            }
            try
            {
                SudokuPuzzleSettings settings = simulatedAnnealing.Settings;
                settings.InitialTemperature = saInitialTemperatureSlider.value;
                settings.CoolingRate = saCoolingSlider.value;
                settings.MinimumTemperature = saMinimumTemperatureSlider.value;
                settings.MaxSteps = Mathf.RoundToInt(saMaxStepsSlider.value);
                settings.TargetClues = Mathf.RoundToInt(targetCluesSlider.value);
                settings.TargetDifficulty = (SudokuDifficulty)Mathf.RoundToInt(targetDifficultySlider.value);
                settings.MaxRestarts = Mathf.RoundToInt(saRestartsSlider.value);
                settings.ClueWeight = clueWeightSlider.value;
                settings.DifficultyWeight = difficultyWeightSlider.value;
                simulatedAnnealing.SetParameters(settings);
                return true;
            }
            catch (ArgumentException)
            {
                SetStatus("Revisa SA: temperatura inicial > mínima > 0,\n" +
                    "enfriamiento entre 0 y 1, 17–80 pistas, nivel Fácil–Difícil y pesos positivos.", true);
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

        private void RegisterPuzzleListeners()
        {
            if (targetCluesSlider != null) targetCluesSlider.onValueChanged.AddListener(UpdatePuzzleLabels);
            if (targetDifficultySlider != null) targetDifficultySlider.onValueChanged.AddListener(ApplyDifficultyPreset);
            if (saRestartsSlider != null) saRestartsSlider.onValueChanged.AddListener(UpdatePuzzleLabels);
            if (clueWeightSlider != null) clueWeightSlider.onValueChanged.AddListener(UpdatePuzzleLabels);
            if (difficultyWeightSlider != null) difficultyWeightSlider.onValueChanged.AddListener(UpdatePuzzleLabels);
        }

        private void UnregisterPuzzleListeners()
        {
            if (targetCluesSlider != null) targetCluesSlider.onValueChanged.RemoveListener(UpdatePuzzleLabels);
            if (targetDifficultySlider != null) targetDifficultySlider.onValueChanged.RemoveListener(ApplyDifficultyPreset);
            if (saRestartsSlider != null) saRestartsSlider.onValueChanged.RemoveListener(UpdatePuzzleLabels);
            if (clueWeightSlider != null) clueWeightSlider.onValueChanged.RemoveListener(UpdatePuzzleLabels);
            if (difficultyWeightSlider != null) difficultyWeightSlider.onValueChanged.RemoveListener(UpdatePuzzleLabels);
        }

        private void ApplyDifficultyPreset(float value)
        {
            if (simulatedAnnealing.UseDifficultyPresets)
            {
                var settings = SudokuDifficultyPresets.Create((SudokuDifficulty)Mathf.RoundToInt(value));
                // El límite de verificación pertenece a la configuración del Inspector.
                settings.SolutionNodeLimit = simulatedAnnealing.Settings.SolutionNodeLimit;
                targetCluesSlider.SetValueWithoutNotify(settings.TargetClues);
                saInitialTemperatureSlider.SetValueWithoutNotify((float)settings.InitialTemperature);
                saCoolingSlider.SetValueWithoutNotify((float)settings.CoolingRate);
                saMinimumTemperatureSlider.SetValueWithoutNotify((float)settings.MinimumTemperature);
                saMaxStepsSlider.SetValueWithoutNotify(settings.MaxSteps);
                saRestartsSlider.SetValueWithoutNotify(settings.MaxRestarts);
                clueWeightSlider.SetValueWithoutNotify((float)settings.ClueWeight);
                difficultyWeightSlider.SetValueWithoutNotify((float)settings.DifficultyWeight);
                simulatedAnnealing.SetParameters(settings);
                UpdateAllAnnealingLabels();
            }
            UpdatePuzzleLabels();
        }

        private void UpdatePuzzleLabels(float ignored = 0)
        {
            if (targetCluesLabel != null && targetCluesSlider != null)
                targetCluesLabel.text = Mathf.RoundToInt(targetCluesSlider.value).ToString(CultureInfo.InvariantCulture);
            if (targetDifficultyLabel != null && targetDifficultySlider != null)
                targetDifficultyLabel.text = SudokuLogicalEvaluator.Label((SudokuDifficulty)Mathf.RoundToInt(targetDifficultySlider.value));
            if (saRestartsLabel != null && saRestartsSlider != null)
                saRestartsLabel.text = Mathf.RoundToInt(saRestartsSlider.value).ToString(CultureInfo.InvariantCulture);
            if (clueWeightLabel != null && clueWeightSlider != null)
                clueWeightLabel.text = clueWeightSlider.value.ToString("F2", CultureInfo.InvariantCulture);
            if (difficultyWeightLabel != null && difficultyWeightSlider != null)
                difficultyWeightLabel.text = difficultyWeightSlider.value.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string StopReasonText(AnnealingStopReason reason)
        {
            switch (reason)
            {
                case AnnealingStopReason.TargetsReached: return "Cantidad y dificultad alcanzadas";
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
            if (saInitialTemperatureLabel != null) saInitialTemperatureLabel.text = value.ToString("F3", CultureInfo.InvariantCulture);
        }

        private void UpdateSACoolingLabel(float value)
        {
            if (saCoolingLabel != null) saCoolingLabel.text = value.ToString("F4", CultureInfo.InvariantCulture);
        }

        private void UpdateSAMinimumTemperatureLabel(float value)
        {
            if (saMinimumTemperatureLabel != null) saMinimumTemperatureLabel.text = value.ToString("F5", CultureInfo.InvariantCulture);
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
            if (previousButton != null) previousButton.interactable = !isGenerating && currentIndex > 0;
            if (nextButton != null) nextButton.interactable = !isGenerating && currentIndex >= 0 && currentIndex < boards.Count - 1;
            if (playButton != null) playButton.interactable = !isGenerating && currentIndex == 1 && puzzleResult != null && puzzleResult.CanPlay;
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (statusLabel == null) return;
            statusLabel.text = message;
            statusLabel.color = isError ? new Color(1f, 0.55f, 0.5f) : statusColor;
        }
    }
}
