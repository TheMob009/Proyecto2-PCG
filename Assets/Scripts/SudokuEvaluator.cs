using System;

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
    }
}
