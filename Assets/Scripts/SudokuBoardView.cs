using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PCG.Sudoku
{
    /// <summary>Presenta datos de Sudoku; no genera ni modifica sus soluciones.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SudokuBoardView : MonoBehaviour
    {
        public const int Size = 9;
        public const int CellCount = Size * Size;

        /// <summary>Color de texto por defecto de las celdas (igual al configurado originalmente en la escena).</summary>
        public static readonly Color DefaultTextColor = new Color(0.12f, 0.15f, 0.2f);

        // Orden por filas: índice = fila * 9 + columna, desde la esquina superior izquierda.
        [SerializeField] private TMP_Text[] cells = new TMP_Text[CellCount];
        private readonly int[] values = new int[CellCount];
        private Image[] cellBackgrounds;
        private Color[] originalCellBackgrounds;
        private bool interactionEnabled;

        /// <summary>Se invoca cuando el jugador hace clic en una celda, una vez habilitada la interacción.</summary>
        public event Action<int, int> CellClicked;

        public void SetBoard(int[,] board)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (board.GetLength(0) != Size || board.GetLength(1) != Size)
                throw new ArgumentException("El tablero debe tener dimensiones 9 x 9.", nameof(board));

            // Validar todo antes de actualizar para no dejar un tablero parcialmente cambiado.
            ValidateCells();
            for (int row = 0; row < Size; row++)
                for (int column = 0; column < Size; column++)
                    ValidateValue(board[row, column]);

            for (int row = 0; row < Size; row++)
                for (int column = 0; column < Size; column++)
                    WriteCell(row * Size + column, board[row, column]);
        }

        /// <summary>Recibe 81 valores en orden por filas. Cero se muestra vacío.</summary>
        public void SetValues(IReadOnlyList<int> board)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (board.Count != CellCount)
                throw new ArgumentException("El tablero debe contener 81 valores.", nameof(board));

            ValidateCells();
            for (int index = 0; index < CellCount; index++)
                ValidateValue(board[index]);
            for (int index = 0; index < CellCount; index++)
                WriteCell(index, board[index]);
        }

        public void SetCell(int row, int column, int value)
        {
            ValidateCoordinates(row, column);
            ValidateValue(value);
            ValidateCells();
            WriteCell(row * Size + column, value);
        }

        public int GetCell(int row, int column)
        {
            ValidateCoordinates(row, column);
            return values[row * Size + column];
        }

        public void ClearBoard()
        {
            ValidateCells();
            for (int index = 0; index < CellCount; index++)
                WriteCell(index, 0);
        }

        /// <summary>Habilita el clic sobre las 81 celdas (idempotente). Necesario para poder jugar el Sudoku.</summary>
        public void EnableInteraction()
        {
            ValidateCells();
            if (interactionEnabled) return;
            interactionEnabled = true;

            cellBackgrounds = new Image[CellCount];
            originalCellBackgrounds = new Color[CellCount];

            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    int index = row * Size + column;
                    int capturedRow = row;
                    int capturedColumn = column;
                    GameObject cellObject = CellContainer(index);

                    Image image = cellObject.GetComponent<Image>();
                    if (image != null)
                    {
                        image.raycastTarget = true;
                        cellBackgrounds[index] = image;
                        originalCellBackgrounds[index] = image.color;
                    }

                    Button button = cellObject.GetComponent<Button>();
                    if (button == null) button = cellObject.AddComponent<Button>();
                    button.onClick.AddListener(() => CellClicked?.Invoke(capturedRow, capturedColumn));
                }
            }
        }

        /// <summary>Cambia el color del texto de una celda (por ejemplo, para distinguir pistas, respuestas y conflictos).</summary>
        public void SetCellColor(int row, int column, Color color)
        {
            ValidateCoordinates(row, column);
            ValidateCells();
            cells[row * Size + column].color = color;
        }

        /// <summary>Resalta (o restaura, con null) el fondo de una celda. Requiere haber llamado a <see cref="EnableInteraction"/>.</summary>
        public void SetCellBackground(int row, int column, Color? color)
        {
            ValidateCoordinates(row, column);
            if (cellBackgrounds == null) return;
            int index = row * Size + column;
            Image image = cellBackgrounds[index];
            if (image == null) return;
            image.color = color ?? originalCellBackgrounds[index];
        }

        /// <summary>Restaura el color de texto y de fondo por defecto de las 81 celdas.</summary>
        public void ResetCellStyles()
        {
            ValidateCells();
            for (int index = 0; index < CellCount; index++)
            {
                cells[index].color = DefaultTextColor;
                if (cellBackgrounds != null && cellBackgrounds[index] != null)
                    cellBackgrounds[index].color = originalCellBackgrounds[index];
            }
        }

        private GameObject CellContainer(int index)
        {
            Transform parent = cells[index].transform.parent;
            return parent != null ? parent.gameObject : cells[index].gameObject;
        }

        private void WriteCell(int index, int value)
        {
            values[index] = value;
            cells[index].text = value == 0 ? string.Empty : value.ToString();
        }

        private void ValidateCells()
        {
            if (cells == null || cells.Length != CellCount)
                throw new InvalidOperationException("SudokuBoardView requiere 81 referencias de texto.");
            for (int index = 0; index < CellCount; index++)
                if (cells[index] == null)
                    throw new InvalidOperationException($"Falta la referencia de la celda {index}.");
        }

        private static void ValidateValue(int value)
        {
            if (value < 0 || value > Size)
                throw new ArgumentOutOfRangeException(nameof(value), "Los valores deben estar entre 0 y 9.");
        }

        private static void ValidateCoordinates(int row, int column)
        {
            if (row < 0 || row >= Size)
                throw new ArgumentOutOfRangeException(nameof(row), "La fila debe estar entre 0 y 8.");
            if (column < 0 || column >= Size)
                throw new ArgumentOutOfRangeException(nameof(column), "La columna debe estar entre 0 y 8.");
        }
    }
}
