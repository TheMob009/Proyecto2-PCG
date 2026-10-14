using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PCG.Sudoku
{
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

        [Header("Resultados")]
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Header("Selección de técnica")]
        [SerializeField] private Button randomStartButton;
        [SerializeField] private Button geneticButton;
        [SerializeField] private GameObject randomStartPanel;
        [SerializeField] private GameObject geneticPanel;

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

        private List<int[,]> boards = new List<int[,]>();
        private int currentIndex = -1;
        private Color statusColor;
        private bool lastGeneratedWithGenetic = false;

        public int BoardCount => boards.Count;
        public int CurrentBoardIndex => currentIndex;
        public int? LastSeed { get; private set; }

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
            SelectRandomStart();
            if (geneticButton != null)
                geneticButton.interactable = true;
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

            RegisterGeneticListeners();

            UpdateBoardCountLabel(boardCountSlider.value);
            UpdateSeedInput(fixedSeedToggle.isOn);
            UpdateAllGeneticLabels();

            if (boards.Count == 0)
                SetStatus("Genera estados iniciales o ejecuta el algoritmo genético para comenzar.");
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

            UnregisterGeneticListeners();
        }

        public void Generate()
        {
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

            if (geneticPanel != null && geneticPanel.activeSelf)
            {
                GenerateGenetic(seed);
            }
            else
            {
                GenerateRandomStart(seed);
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
            lastGeneratedWithGenetic = false;
            seedInput.SetTextWithoutNotify(seed.ToString(CultureInfo.InvariantCulture));
            ShowCurrentBoard();
        }

        private void GenerateGenetic(int seed)
        {
            if (geneticAlgorithm == null)
            {
                geneticAlgorithm = FindObjectOfType<SudokuGeneticA>();
                if (geneticAlgorithm == null)
                    geneticAlgorithm = gameObject.AddComponent<SudokuGeneticA>();
            }

            if (gaPopulationSlider != null)
            {
                geneticAlgorithm.SetParameters(
                    Mathf.RoundToInt(gaPopulationSlider.value),
                    gaGenerationsSlider != null ? Mathf.RoundToInt(gaGenerationsSlider.value) : 80,
                    gaCrossoverSlider != null ? gaCrossoverSlider.value : 0.8f,
                    gaMutationSlider != null ? gaMutationSlider.value : 0.1f,
                    gaTournamentSlider != null ? Mathf.RoundToInt(gaTournamentSlider.value) : 3,
                    gaEliteSlider != null ? Mathf.RoundToInt(gaEliteSlider.value) : 2
                );
            }

            SudokuCandidate result = geneticAlgorithm.Generate(seed);
            if (result != null && result.board != null)
            {
                boards = new List<int[,]> { result.board };
                LastSeed = seed;
                currentIndex = 0;
                lastGeneratedWithGenetic = true;
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
            currentIndex--;
            ShowCurrentBoard();
        }

        public void NextBoard()
        {
            if (currentIndex < 0 || currentIndex >= boards.Count - 1) return;
            currentIndex++;
            ShowCurrentBoard();
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
            ShowParameterPanel(randomStartPanel);
        }

        public void SelectGeneticParameters()
        {
            ShowParameterPanel(geneticPanel);
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
            string state;
            if (evaluation.IsValid)
                state = "Solución válida";
            else if (lastGeneratedWithGenetic)
                state = "Algoritmo Genético";
            else
                state = "Estado inicial";

            SetStatus($"{state} {currentIndex + 1}/{boards.Count} | Semilla: {LastSeed}\n" +
                $"Conflictos: {evaluation.TotalConflicts} (filas: {evaluation.RowConflicts}, " +
                $"columnas: {evaluation.ColumnConflicts}, bloques: {evaluation.BlockConflicts})");
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
                BindControl(ref gaPopulationSlider, ref gaPopulationLabel, "GAPoblation");
                BindControl(ref gaGenerationsSlider, ref gaGenerationsLabel, "GAGenerations");
                BindControl(ref gaCrossoverSlider, ref gaCrossoverLabel, "GACrossover");
                BindControl(ref gaMutationSlider, ref gaMutationLabel, "GAMutacion");
                BindControl(ref gaTournamentSlider, ref gaTournamentLabel, "GATorneo");
                BindControl(ref gaEliteSlider, ref gaEliteLabel, "GAElitismo");
            }
        }

        private void BindControl(ref Slider slider, ref TMP_Text label, string childName)
        {
            Transform t = geneticPanel.transform.Find(childName);
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
            statusLabel.color = isError ? new Color(0.75f, 0.12f, 0.12f) : statusColor;
        }
    }
}
