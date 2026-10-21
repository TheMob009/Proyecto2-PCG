using System;

namespace PCG.Sudoku
{
    /// <summary>Restricciones y unidades compartidas por los verificadores de Sudoku.</summary>
    internal static class SudokuConstraints
    {
        internal const int AllDigits = 0x3FE;
        internal static readonly int[][] Units = CreateUnits();

        internal static int[] Flatten(int[,] board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (board.GetLength(0) != 9 || board.GetLength(1) != 9)
                throw new ArgumentException("El tablero debe tener dimensiones 9 x 9.", nameof(board));
            var values = new int[81];
            for (int i = 0; i < 81; i++)
            {
                int value = board[i / 9, i % 9];
                if (value < 0 || value > 9)
                    throw new ArgumentException("Los valores deben estar entre 0 y 9.", nameof(board));
                values[i] = value;
            }
            return values;
        }

        internal static int[,] Expand(int[] values)
        {
            var board = new int[9, 9];
            for (int i = 0; i < 81; i++) board[i / 9, i % 9] = values[i];
            return board;
        }

        internal static int Block(int index) => (index / 27) * 3 + index % 9 / 3;

        internal static int BitCount(int mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1) count++;
            return count;
        }

        internal static int Digit(int bit)
        {
            for (int digit = 1; digit <= 9; digit++)
                if (bit == (1 << digit)) return digit;
            throw new ArgumentException("Se requiere exactamente un candidato.", nameof(bit));
        }

        internal static bool SharesUnit(int a, int b) =>
            a / 9 == b / 9 || a % 9 == b % 9 || Block(a) == Block(b);

        internal static bool Contains(int[] unit, int index) => Array.IndexOf(unit, index) >= 0;

        private static int[][] CreateUnits()
        {
            var units = new int[27][];
            for (int u = 0; u < 27; u++)
            {
                units[u] = new int[9];
                for (int p = 0; p < 9; p++)
                    units[u][p] = u < 9 ? u * 9 + p : u < 18 ? p * 9 + u - 9 :
                        ((u - 18) / 3 * 3 + p / 3) * 9 + (u - 18) % 3 * 3 + p % 3;
            }
            return units;
        }
    }
}
