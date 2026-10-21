using System;

namespace PCG.Sudoku
{
    /// <summary>Configuraciones calibradas para cantidad exacta y dificultad lógica.</summary>
    public static class SudokuDifficultyPresets
    {
        public static SudokuPuzzleSettings Create(SudokuDifficulty difficulty)
        {
            if (difficulty < SudokuDifficulty.Easy || difficulty > SudokuDifficulty.Hard)
                throw new ArgumentOutOfRangeException(nameof(difficulty));
            var settings = new SudokuPuzzleSettings { TargetDifficulty = difficulty };
            if (difficulty == SudokuDifficulty.Easy) return settings;
            settings.TargetClues = difficulty == SudokuDifficulty.Medium ? 30 : 28;
            // Literales float: la calibración utiliza la misma precisión que los controles de Unity.
            settings.InitialTemperature = 0.025f;
            settings.CoolingRate = 0.999f;
            settings.MaxSteps = 30000;
            settings.MaxRestarts = 5;
            settings.ClueWeight = 3;
            return settings;
        }
    }
}
