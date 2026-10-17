using System;

namespace PCG.Sudoku
{
    /// <summary>A partir de un tablero resuelto (cero conflictos), oculta celdas al azar para crear un puzzle jugable.</summary>
    public static class SudokuPuzzleGenerator
    {
        public const int Size = SudokuRandomStartState.Size;
        public const int CellCount = Size * Size;

        /// <summary>Devuelve el puzzle (con ceros en las celdas vaciadas) y, por salida, qué celdas quedaron fijas (pistas).</summary>
        public static int[,] CreatePuzzle(int[,] solvedBoard, int cellsToRemove, int seed, out bool[,] isGiven)
        {
            if (solvedBoard == null)
                throw new ArgumentNullException(nameof(solvedBoard));
            if (solvedBoard.GetLength(0) != Size || solvedBoard.GetLength(1) != Size)
                throw new ArgumentException("El tablero debe tener dimensiones 9 x 9.", nameof(solvedBoard));
            if (!SudokuEvaluator.Evaluate(solvedBoard).IsValid)
                throw new ArgumentException("El tablero debe estar resuelto (cero conflictos) para generar un puzzle.", nameof(solvedBoard));

            if (cellsToRemove < 0) cellsToRemove = 0;
            if (cellsToRemove > CellCount) cellsToRemove = CellCount;

            int[,] puzzle = (int[,])solvedBoard.Clone();
            isGiven = new bool[Size, Size];
            for (int row = 0; row < Size; row++)
                for (int column = 0; column < Size; column++)
                    isGiven[row, column] = true;

            // Fisher-Yates sobre las 81 posiciones: elige sin repetir cuáles vaciar.
            int[] positions = new int[CellCount];
            for (int i = 0; i < CellCount; i++)
                positions[i] = i;

            var random = new Random(seed);
            for (int i = CellCount - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int temporary = positions[i];
                positions[i] = positions[j];
                positions[j] = temporary;
            }

            for (int i = 0; i < cellsToRemove; i++)
            {
                int row = positions[i] / Size;
                int column = positions[i] % Size;
                puzzle[row, column] = 0;
                isGiven[row, column] = false;
            }

            return puzzle;
        }
    }
}
