using System;
using System.Collections.Generic;

namespace PCG.Sudoku
{
    public readonly struct SudokuEvaluation
    {
        public int RowConflicts { get; }
        public int ColumnConflicts { get; }
        public int BlockConflicts { get; }
        public int TotalConflicts => RowConflicts + ColumnConflicts + BlockConflicts;
        public bool IsValid => TotalConflicts == 0;

        public SudokuEvaluation(int rows, int columns, int blocks)
        {
            RowConflicts = rows;
            ColumnConflicts = columns;
            BlockConflicts = blocks;
        }
    }

    /// <summary>Cuenta pares iguales por unidad. En Random Start State las filas aportan cero.</summary>
    public static class SudokuEvaluator
    {
        public static SudokuEvaluation Evaluate(int[,] board)
        {
            const int size = SudokuRandomStartState.Size;
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (board.GetLength(0) != size || board.GetLength(1) != size)
                throw new ArgumentException("El tablero debe tener dimensiones 9 x 9.", nameof(board));

            int[,] rows = new int[size, size + 1];
            int[,] columns = new int[size, size + 1];
            int[,] blocks = new int[size, size + 1];
            int rowConflicts = 0;
            int columnConflicts = 0;
            int blockConflicts = 0;

            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    int value = board[row, column];
                    if (value < 1 || value > size)
                        throw new ArgumentException("La evaluación requiere un tablero completo con valores de 1 a 9.", nameof(board));
                    int block = (row / 3) * 3 + column / 3;
                    // Cada aparición nueva forma un par con cada aparición anterior del dígito.
                    rowConflicts += rows[row, value]++;
                    columnConflicts += columns[column, value]++;
                    blockConflicts += blocks[block, value]++;
                }
            }
            return new SudokuEvaluation(rowConflicts, columnConflicts, blockConflicts);
        }

        /// <summary>Evalúa un tablero parcialmente lleno (con ceros = celda vacía), contando conflictos solo entre celdas ocupadas.</summary>
        public static SudokuEvaluation EvaluatePartial(int[,] board)
        {
            const int size = SudokuRandomStartState.Size;
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (board.GetLength(0) != size || board.GetLength(1) != size)
                throw new ArgumentException("El tablero debe tener dimensiones 9 x 9.", nameof(board));

            int[,] rows = new int[size, size + 1];
            int[,] columns = new int[size, size + 1];
            int[,] blocks = new int[size, size + 1];
            int rowConflicts = 0;
            int columnConflicts = 0;
            int blockConflicts = 0;

            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    int value = board[row, column];
                    if (value == 0) continue;
                    if (value < 0 || value > size)
                        throw new ArgumentException("Los valores deben estar entre 0 y 9.", nameof(board));
                    int block = (row / 3) * 3 + column / 3;
                    rowConflicts += rows[row, value]++;
                    columnConflicts += columns[column, value]++;
                    blockConflicts += blocks[block, value]++;
                }
            }
            return new SudokuEvaluation(rowConflicts, columnConflicts, blockConflicts);
        }

        /// <summary>Marca qué celdas (con valor) repiten su número dentro de su fila, columna o bloque. Las celdas vacías son false.</summary>
        public static bool[,] FindConflictingCells(int[,] board)
        {
            const int size = SudokuRandomStartState.Size;
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (board.GetLength(0) != size || board.GetLength(1) != size)
                throw new ArgumentException("El tablero debe tener dimensiones 9 x 9.", nameof(board));

            bool[,] conflicts = new bool[size, size];

            for (int row = 0; row < size; row++)
                MarkDuplicates(conflicts, ValuePositions(row, true, size, board));
            for (int column = 0; column < size; column++)
                MarkDuplicates(conflicts, ValuePositions(column, false, size, board));
            for (int blockRow = 0; blockRow < 3; blockRow++)
                for (int blockColumn = 0; blockColumn < 3; blockColumn++)
                    MarkDuplicates(conflicts, BlockPositions(blockRow, blockColumn, board));

            return conflicts;
        }

        private static Dictionary<int, List<(int row, int column)>> ValuePositions(int index, bool isRow, int size, int[,] board)
        {
            var seen = new Dictionary<int, List<(int row, int column)>>();
            for (int other = 0; other < size; other++)
            {
                int row = isRow ? index : other;
                int column = isRow ? other : index;
                int value = board[row, column];
                if (value == 0) continue;
                if (!seen.TryGetValue(value, out var positions))
                {
                    positions = new List<(int, int)>();
                    seen[value] = positions;
                }
                positions.Add((row, column));
            }
            return seen;
        }

        private static Dictionary<int, List<(int row, int column)>> BlockPositions(int blockRow, int blockColumn, int[,] board)
        {
            var seen = new Dictionary<int, List<(int row, int column)>>();
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int row = blockRow * 3 + r;
                    int column = blockColumn * 3 + c;
                    int value = board[row, column];
                    if (value == 0) continue;
                    if (!seen.TryGetValue(value, out var positions))
                    {
                        positions = new List<(int, int)>();
                        seen[value] = positions;
                    }
                    positions.Add((row, column));
                }
            }
            return seen;
        }

        private static void MarkDuplicates(bool[,] conflicts, Dictionary<int, List<(int row, int column)>> positionsByValue)
        {
            foreach (var positions in positionsByValue.Values)
            {
                if (positions.Count <= 1) continue;
                foreach (var (row, column) in positions)
                    conflicts[row, column] = true;
            }
        }
    }
}
