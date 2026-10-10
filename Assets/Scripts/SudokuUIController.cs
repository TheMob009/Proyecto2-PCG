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

        private List<int[,]> boards = new List<int[,]>();
        private int currentIndex = -1;
        private Color statusColor;

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
            UpdateBoardCountLabel(boardCountSlider.value);
            UpdateSeedInput(fixedSeedToggle.isOn);
            if (boards.Count == 0)
                SetStatus("Genera estados iniciales para comenzar.");
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
        }

        public void Generate()
        {
            float requestedCount = boardCountSlider.value;
            if (float.IsNaN(requestedCount) || float.IsInfinity(requestedCount) ||
                requestedCount < 1 || requestedCount > SudokuRandomStartState.MaxBoardCount)
            {
                SetStatus($"La cantidad debe estar entre 1 y {SudokuRandomStartState.MaxBoardCount}.", true);
                return;
            }

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

            boards = SudokuRandomStartState.Generate(Mathf.RoundToInt(requestedCount), seed);
            LastSeed = seed;
            currentIndex = 0;
            seedInput.SetTextWithoutNotify(seed.ToString(CultureInfo.InvariantCulture));
            ShowCurrentBoard();
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
            string state = evaluation.IsValid ? "Solución válida" : "Estado inicial";
            SetStatus($"{state} {currentIndex + 1}/{boards.Count} | Semilla: {LastSeed}\n" +
                $"Conflictos: {evaluation.TotalConflicts} (filas: {evaluation.RowConflicts}, " +
                $"columnas: {evaluation.ColumnConflicts}, bloques: {evaluation.BlockConflicts})");
            UpdateNavigation();
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
