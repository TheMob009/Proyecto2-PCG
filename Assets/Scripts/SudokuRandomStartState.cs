using System;
using System.Collections.Generic;

namespace PCG.Sudoku
{
    /// <summary>Genera estados completos aleatorios con cada fila válida por construcción.</summary>
    public static class SudokuRandomStartState
    {
        public const int Size = 9;
        public const int MaxBoardCount = 200;

        public static List<int[,]> Generate(int boardCount, int seed)
        {
            if (boardCount < 1 || boardCount > MaxBoardCount)
                throw new ArgumentOutOfRangeException(nameof(boardCount),
                    $"La cantidad debe estar entre 1 y {MaxBoardCount}.");

            // La semilla identifica la colección completa, no solamente su primer tablero.
            Random random = new Random(seed);
            List<int[,]> boards = new List<int[,]>(boardCount);
            for (int index = 0; index < boardCount; index++)
                boards.Add(CreateBoard(random));
            return boards;
        }

        private static int[,] CreateBoard(Random random)
        {
            int[,] board = new int[Size, Size];
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                    board[row, column] = column + 1;

                // Fisher-Yates: mezclar cada fila sin agregar ni eliminar números.
                for (int column = Size - 1; column > 0; column--)
                {
                    int other = random.Next(column + 1);
                    int temporary = board[row, column];
                    board[row, column] = board[row, other];
                    board[row, other] = temporary;
                }
            }
            return board;
        }
    }
}
