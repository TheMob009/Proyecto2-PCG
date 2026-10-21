using System;
using System.Diagnostics;
using PCG.Sudoku;

internal static class AnnealingTuning
{
    internal static void Run(string[] args)
    {
        int count = args.Length > 1 ? int.Parse(args[1]) : 10;
        string filter = args.Length > 2 ? args[2] : "baseline";
        var configurations = new[]
        {
            (Name: "presets", Clues: 30, Steps: 30000, Restarts: 5, Temperature: .025, Cooling: .999, ClueWeight: 3.0, DifficultyWeight: 1.0),
            (Name: "baseline", Clues: 30, Steps: 5000, Restarts: 2, Temperature: .05, Cooling: .999, ClueWeight: 1.0, DifficultyWeight: 1.0),
            (Name: "budget30", Clues: 30, Steps: 30000, Restarts: 5, Temperature: .025, Cooling: .999, ClueWeight: 3.0, DifficultyWeight: 1.0),
            (Name: "budget28", Clues: 28, Steps: 30000, Restarts: 5, Temperature: .025, Cooling: .999, ClueWeight: 3.0, DifficultyWeight: 1.0),
            (Name: "budget26", Clues: 26, Steps: 30000, Restarts: 5, Temperature: .025, Cooling: .999, ClueWeight: 3.0, DifficultyWeight: 1.0),
            (Name: "long28", Clues: 28, Steps: 100000, Restarts: 9, Temperature: .02, Cooling: .9995, ClueWeight: 3.0, DifficultyWeight: 1.0)
        };
        var solutions = new int[count][,];
        for (int i = 0; i < count; i++)
        {
            int seed = Seed(i);
            solutions[i] = new SudokuGeneticA().Generate(seed).board;
            if (!SudokuEvaluator.Evaluate(solutions[i]).IsValid) throw new Exception("Invalid GA solution: " + seed);
        }
        foreach (var configuration in configurations)
        {
            if (Array.IndexOf(filter.Split(','), configuration.Name) < 0) continue;
            foreach (var difficulty in new[] { SudokuDifficulty.Easy, SudokuDifficulty.Medium, SudokuDifficulty.Hard })
            {
                int hits = 0;
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    var settings = new SudokuPuzzleSettings { TargetClues = difficulty == SudokuDifficulty.Easy ? 41 : configuration.Clues,
                        TargetDifficulty = difficulty, MaxSteps = configuration.Steps, MaxRestarts = configuration.Restarts,
                        InitialTemperature = configuration.Temperature, CoolingRate = configuration.Cooling,
                        ClueWeight = configuration.ClueWeight, DifficultyWeight = configuration.DifficultyWeight };
                    if (configuration.Name == "presets") settings = SudokuDifficultyPresets.Create(difficulty);
                    var sample = Stopwatch.StartNew();
                    var result = SudokuPuzzleGenerator.Generate(solutions[i], settings, unchecked((Seed(i) * 397) ^ 0x5341));
                    if (SudokuSolutionCounter.Count(result.Puzzle, 1000000) != SudokuSolutionStatus.Unique)
                        throw new Exception("Output was not unique");
                    if (result.TargetsReached)
                    {
                        hits++;
                        var weaker = difficulty == SudokuDifficulty.Hard ? SudokuLogicalTechnique.LockedCandidates : SudokuLogicalTechnique.HiddenSingle;
                        if (difficulty != SudokuDifficulty.Easy && SudokuLogicalEvaluator.Evaluate(result.Puzzle, weaker).IsSolved)
                            throw new Exception("Advanced profile was solvable with easier techniques");
                    }
                    Console.WriteLine($"SA {configuration.Name} {difficulty} seed={Seed(i)} hit={result.TargetsReached} clues={result.ClueCount} actual={result.Logic.Difficulty} steps={result.StepsPerformed} ms={sample.ElapsedMilliseconds}");
                }
                Console.WriteLine($"SUMMARY {configuration.Name} {difficulty}: {hits}/{count}, totalMs={watch.ElapsedMilliseconds}");
            }
        }
    }

    private static int Seed(int index)
    {
        int[] reference = { 12345, 0, -1, int.MinValue, int.MaxValue, 1, 2, 42, 2026, 987654321 };
        return index < reference.Length ? reference[index] : unchecked(20261021 + index * 7919);
    }
}
