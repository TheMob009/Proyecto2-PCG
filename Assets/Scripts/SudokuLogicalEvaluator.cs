using System;

namespace PCG.Sudoku
{
    public enum SudokuDifficulty { Trivial, Easy, Medium, Hard, Unrated }
    public enum SudokuLogicalTechnique { None, NakedSingle, HiddenSingle, LockedCandidates, NakedPair }

    public sealed class SudokuLogicalEvaluation
    {
        public SudokuDifficulty Difficulty { get; internal set; }
        public SudokuLogicalTechnique HardestTechnique { get; internal set; }
        public int RemainingCells { get; internal set; }
        public bool IsSolved => RemainingCells == 0 && Difficulty != SudokuDifficulty.Unrated;
        public int NakedSingleSteps { get; internal set; }
        public int HiddenSingleSteps { get; internal set; }
        public int LockedCandidateSteps { get; internal set; }
        public int NakedPairSteps { get; internal set; }
    }

    /// <summary>Resuelve sin adivinar, agotando las técnicas sencillas antes de avanzar.</summary>
    public static class SudokuLogicalEvaluator
    {
        public static SudokuLogicalEvaluation Evaluate(int[,] board,
            SudokuLogicalTechnique maximumTechnique = SudokuLogicalTechnique.NakedPair)
        {
            if (maximumTechnique < SudokuLogicalTechnique.None || maximumTechnique > SudokuLogicalTechnique.NakedPair)
                throw new ArgumentOutOfRangeException(nameof(maximumTechnique));
            var values = SudokuConstraints.Flatten(board);
            var result = new SudokuLogicalEvaluation();
            var masks = new int[81];
            bool valid = true;
            for (int i = 0; i < 81; i++)
            {
                if (values[i] == 0)
                {
                    result.RemainingCells++;
                    int used = 0;
                    for (int j = 0; j < 81; j++)
                        if (values[j] != 0 && SudokuConstraints.SharesUnit(i, j)) used |= 1 << values[j];
                    masks[i] = SudokuConstraints.AllDigits & ~used;
                }
                else
                    for (int j = i + 1; j < 81; j++)
                        if (values[i] == values[j] && SudokuConstraints.SharesUnit(i, j)) valid = false;
            }
            while (valid && result.RemainingCells > 0)
            {
                for (int i = 0; i < 81; i++)
                    if (values[i] == 0 && masks[i] == 0) valid = false;
                if (!valid) break;
                if (maximumTechnique >= SudokuLogicalTechnique.NakedSingle && ApplySingle(values, masks, result)) continue;
                if (maximumTechnique >= SudokuLogicalTechnique.HiddenSingle && ApplyHiddenSingle(values, masks, result)) continue;
                if (maximumTechnique >= SudokuLogicalTechnique.LockedCandidates && ApplyLockedCandidates(values, masks))
                {
                    result.LockedCandidateSteps++;
                    if (result.HardestTechnique < SudokuLogicalTechnique.LockedCandidates)
                        result.HardestTechnique = SudokuLogicalTechnique.LockedCandidates;
                    continue;
                }
                if (maximumTechnique >= SudokuLogicalTechnique.NakedPair && ApplyNakedPair(values, masks))
                {
                    result.NakedPairSteps++;
                    result.HardestTechnique = SudokuLogicalTechnique.NakedPair;
                    continue;
                }
                break;
            }
            result.Difficulty = !valid || result.RemainingCells != 0 ? SudokuDifficulty.Unrated :
                result.HardestTechnique == SudokuLogicalTechnique.None ? SudokuDifficulty.Trivial :
                result.HardestTechnique == SudokuLogicalTechnique.NakedPair ? SudokuDifficulty.Hard :
                result.HardestTechnique == SudokuLogicalTechnique.LockedCandidates ? SudokuDifficulty.Medium : SudokuDifficulty.Easy;
            return result;
        }

        public static string Label(SudokuDifficulty difficulty)
        {
            switch (difficulty)
            {
                case SudokuDifficulty.Trivial: return "Trivial";
                case SudokuDifficulty.Easy: return "Fácil";
                case SudokuDifficulty.Medium: return "Media";
                case SudokuDifficulty.Hard: return "Difícil";
                default: return "Fuera del repertorio lógico";
            }
        }

        private static void Assign(int index, int digit, int[] values, int[] masks, SudokuLogicalEvaluation result,
            SudokuLogicalTechnique technique)
        {
            values[index] = digit;
            masks[index] = 0;
            for (int j = 0; j < 81; j++)
                if (values[j] == 0 && SudokuConstraints.SharesUnit(index, j)) masks[j] &= ~(1 << digit);
            result.RemainingCells--;
            if (technique > result.HardestTechnique) result.HardestTechnique = technique;
        }

        private static bool ApplySingle(int[] values, int[] masks, SudokuLogicalEvaluation result)
        {
            for (int i = 0; i < 81; i++)
                if (values[i] == 0 && SudokuConstraints.BitCount(masks[i]) == 1)
                {
                    Assign(i, SudokuConstraints.Digit(masks[i]), values, masks, result, SudokuLogicalTechnique.NakedSingle);
                    result.NakedSingleSteps++;
                    return true;
                }
            return false;
        }

        private static bool ApplyHiddenSingle(int[] values, int[] masks, SudokuLogicalEvaluation result)
        {
            foreach (int[] unit in SudokuConstraints.Units)
                for (int digit = 1; digit <= 9; digit++)
                {
                    int found = -1, count = 0;
                    foreach (int i in unit)
                        if (values[i] == 0 && (masks[i] & (1 << digit)) != 0) { found = i; count++; }
                    if (count != 1) continue;
                    Assign(found, digit, values, masks, result, SudokuLogicalTechnique.HiddenSingle);
                    result.HiddenSingleSteps++;
                    return true;
                }
            return false;
        }

        private static bool ApplyLockedCandidates(int[] values, int[] masks)
        {
            foreach (int[] source in SudokuConstraints.Units)
                for (int digit = 1; digit <= 9; digit++)
                {
                    int bit = 1 << digit, count = 0;
                    foreach (int i in source) if ((masks[i] & bit) != 0) count++;
                    if (count < 2) continue;
                    foreach (int[] target in SudokuConstraints.Units)
                    {
                        if (ReferenceEquals(source, target)) continue;
                        bool contained = true;
                        foreach (int i in source)
                            if ((masks[i] & bit) != 0 && !SudokuConstraints.Contains(target, i)) { contained = false; break; }
                        if (!contained) continue;
                        bool changed = false;
                        foreach (int i in target)
                            if (values[i] == 0 && !SudokuConstraints.Contains(source, i) && (masks[i] & bit) != 0)
                            { masks[i] &= ~bit; changed = true; }
                        if (changed) return true;
                    }
                }
            return false;
        }

        private static bool ApplyNakedPair(int[] values, int[] masks)
        {
            foreach (int[] unit in SudokuConstraints.Units)
                foreach (int first in unit)
                {
                    int pair = masks[first];
                    if (values[first] != 0 || SudokuConstraints.BitCount(pair) != 2) continue;
                    int count = 0;
                    foreach (int i in unit) if (masks[i] == pair) count++;
                    if (count != 2) continue;
                    bool changed = false;
                    foreach (int i in unit)
                        if (values[i] == 0 && masks[i] != pair && (masks[i] & pair) != 0)
                        { masks[i] &= ~pair; changed = true; }
                    if (changed) return true;
                }
            return false;
        }
    }
}
