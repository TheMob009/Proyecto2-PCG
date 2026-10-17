using System;
using UnityEngine;

namespace PCG.Sudoku
{
    /// <summary>Gestiona una partida jugable: pistas fijas, entrada del jugador, conflictos en vivo y victoria.</summary>
    [DisallowMultipleComponent]
    public sealed class SudokuPlayController : MonoBehaviour
    {
        private static readonly Color GivenColor = SudokuBoardView.DefaultTextColor;
        private static readonly Color PlayerColor = new Color(0.1f, 0.35f, 0.75f);
        private static readonly Color ConflictColor = new Color(0.75f, 0.12f, 0.12f);
        private static readonly Color SelectedBackground = new Color(1f, 0.93f, 0.65f);

        [SerializeField] private SudokuBoardView boardView;

        private int[,] current;
        private bool[,] isGiven;
        private int selectedRow = -1;
        private int selectedColumn = -1;

        public bool IsActive { get; private set; }

        /// <summary>Mensaje legible listo para mostrar en la interfaz (conflictos, celdas vacías, victoria, etc.).</summary>
        public event Action<string> StatusChanged;

        private void Awake()
        {
            if (boardView == null)
                boardView = GetComponent<SudokuBoardView>();
            if (boardView == null)
                boardView = FindObjectOfType<SudokuBoardView>();
        }

        private void OnEnable()
        {
            if (boardView == null) return;
            boardView.EnableInteraction();
            boardView.CellClicked += OnCellClicked;
        }

        private void OnDisable()
        {
            if (boardView != null)
                boardView.CellClicked -= OnCellClicked;
        }

        /// <summary>Empieza una partida nueva a partir de un tablero resuelto (cero conflictos), vaciando <paramref name="cellsToRemove"/> celdas.</summary>
        public void StartPuzzle(int[,] solvedBoard, int cellsToRemove, int seed)
        {
            if (boardView == null)
            {
                StatusChanged?.Invoke("No se encontró SudokuBoardView en la escena; no se puede jugar.");
                return;
            }

            current = SudokuPuzzleGenerator.CreatePuzzle(solvedBoard, cellsToRemove, seed, out isGiven);
            IsActive = true;
            selectedRow = -1;
            selectedColumn = -1;
            boardView.EnableInteraction();
            Render();
            StatusChanged?.Invoke(
                $"Modo juego: se vaciaron {cellsToRemove} celdas. Haz clic en una celda vacía y escribe un número (1-9). " +
                "Retroceso/Suprimir la borra.");
        }

        /// <summary>Sale del modo juego y restaura el aspecto normal del tablero (lo usa la UI antes de generar otro tablero).</summary>
        public void StopPuzzle()
        {
            if (!IsActive) return;
            IsActive = false;
            selectedRow = -1;
            selectedColumn = -1;
            if (boardView != null)
                boardView.ResetCellStyles();
        }

        private void Update()
        {
            if (!IsActive || selectedRow < 0) return;
            if (isGiven[selectedRow, selectedColumn]) return;

            for (int digit = 1; digit <= 9; digit++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + digit) || Input.GetKeyDown(KeyCode.Keypad0 + digit))
                {
                    SetValue(selectedRow, selectedColumn, digit);
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete) ||
                Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
            {
                SetValue(selectedRow, selectedColumn, 0);
            }
        }

        private void OnCellClicked(int row, int column)
        {
            if (!IsActive) return;
            selectedRow = row;
            selectedColumn = column;
            Render();
        }

        private void SetValue(int row, int column, int value)
        {
            current[row, column] = value;
            Render();

            bool complete = true;
            for (int r = 0; r < SudokuBoardView.Size; r++)
            {
                for (int c = 0; c < SudokuBoardView.Size; c++)
                {
                    if (current[r, c] == 0)
                    {
                        complete = false;
                        break;
                    }
                }
                if (!complete) break;
            }

            SudokuEvaluation evaluation = SudokuEvaluator.EvaluatePartial(current);
            if (complete && evaluation.IsValid)
                StatusChanged?.Invoke("¡Sudoku resuelto! Felicitaciones.");
            else
                StatusChanged?.Invoke($"Modo juego | Conflictos: {evaluation.TotalConflicts} | Celdas vacías: {CountEmpty()}");
        }

        private int CountEmpty()
        {
            int empty = 0;
            for (int r = 0; r < SudokuBoardView.Size; r++)
                for (int c = 0; c < SudokuBoardView.Size; c++)
                    if (current[r, c] == 0) empty++;
            return empty;
        }

        private void Render()
        {
            boardView.SetBoard(current);
            bool[,] conflicts = SudokuEvaluator.FindConflictingCells(current);
            for (int r = 0; r < SudokuBoardView.Size; r++)
            {
                for (int c = 0; c < SudokuBoardView.Size; c++)
                {
                    Color color = conflicts[r, c] ? ConflictColor : isGiven[r, c] ? GivenColor : PlayerColor;
                    boardView.SetCellColor(r, c, color);
                    bool isSelected = r == selectedRow && c == selectedColumn;
                    boardView.SetCellBackground(r, c, isSelected ? SelectedBackground : (Color?)null);
                }
            }
        }
    }
}
