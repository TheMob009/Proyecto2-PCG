using System;
using System.Collections.Generic;

namespace PCG.Sudoku
{
    public enum SudokuSolutionStatus { None, Unique, Multiple, Unknown }

    /// <summary>Cuenta hasta dos soluciones con MRV y presupuesto determinista de nodos.</summary>
    public sealed class SudokuSolutionCounter
    {
        private readonly int[] values;
        private readonly int[] rows = new int[9];
        private readonly int[] columns = new int[9];
        private readonly int[] blocks = new int[9];
        private readonly int nodeLimit;
        private readonly bool consistent;
        private bool exhausted;
        private bool started;

        public int NodesVisited { get; private set; }
        public int SolutionsFound { get; private set; }
        public SudokuSolutionStatus Status { get; private set; } = SudokuSolutionStatus.Unknown;

        public SudokuSolutionCounter(int[,] board, int nodeLimit = 20000)
        {
            if (nodeLimit < 1) throw new ArgumentOutOfRangeException(nameof(nodeLimit));
            this.nodeLimit = nodeLimit;
            values = SudokuConstraints.Flatten(board);
            consistent = true;
            for (int i = 0; i < 81; i++)
            {
                if (values[i] == 0) continue;
                int bit = 1 << values[i], r = i / 9, c = i % 9, b = SudokuConstraints.Block(i);
                if (((rows[r] | columns[c] | blocks[b]) & bit) != 0) consistent = false;
                rows[r] |= bit;
                columns[c] |= bit;
                blocks[b] |= bit;
            }
        }

        public static SudokuSolutionStatus Count(int[,] board, int nodeLimit = 20000)
        {
            var counter = new SudokuSolutionCounter(board, nodeLimit);
            foreach (bool pause in counter.Run()) { }
            return counter.Status;
        }

        /// <summary>Cede cada 128 nodos para permitir ejecución incremental en Unity y WebGL.</summary>
        public IEnumerable<bool> Run()
        {
            if (started) throw new InvalidOperationException("El conteo solo puede ejecutarse una vez.");
            started = true;
            if (consistent)
                foreach (bool pause in Search()) yield return pause;
            Status = SolutionsFound >= 2 ? SudokuSolutionStatus.Multiple : exhausted ? SudokuSolutionStatus.Unknown :
                SolutionsFound == 1 ? SudokuSolutionStatus.Unique : SudokuSolutionStatus.None;
        }

        private IEnumerable<bool> Search()
        {
            if (NodesVisited >= nodeLimit) { exhausted = true; yield break; }
            NodesVisited++;
            if (NodesVisited % 128 == 0) yield return true;
            int selected = -1, options = 0, minimum = 10;
            for (int i = 0; i < 81; i++)
            {
                if (values[i] != 0) continue;
                int mask = SudokuConstraints.AllDigits & ~(rows[i / 9] | columns[i % 9] | blocks[SudokuConstraints.Block(i)]);
                int count = SudokuConstraints.BitCount(mask);
                if (count == 0) yield break;
                if (count < minimum) { selected = i; options = mask; minimum = count; }
            }
            if (selected < 0) { SolutionsFound++; yield break; }
            int r = selected / 9, c = selected % 9, b = SudokuConstraints.Block(selected);
            for (int digit = 1; digit <= 9; digit++)
            {
                int bit = 1 << digit;
                if ((options & bit) == 0) continue;
                values[selected] = digit;
                rows[r] |= bit;
                columns[c] |= bit;
                blocks[b] |= bit;
                foreach (bool pause in Search()) yield return pause;
                values[selected] = 0;
                rows[r] &= ~bit;
                columns[c] &= ~bit;
                blocks[b] &= ~bit;
                if (SolutionsFound >= 2 || exhausted) yield break;
            }
        }
    }
}
