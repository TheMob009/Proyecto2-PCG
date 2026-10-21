using System;
using System.Diagnostics;
using PCG.Sudoku;

internal static class Program
{
    private const string Solved = "534678912672195348198342567859761423426853791713924856961537284287419635345286179";
    private const string Easy = "530070000600195000098000060800060003400803001700020006060000280000419005000080079";
    private static int assertions;

    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--tune-ga") { GeneticTuning.Run(args); return; }
        if (args.Length > 0 && args[0] == "--tune-sa") { AnnealingTuning.Run(args); return; }
        var solution = Board(Solved);
        Check(SudokuSolutionCounter.Count(solution) == SudokuSolutionStatus.Unique, "complete grid is unique");
        Check(SudokuSolutionCounter.Count(Board(Easy)) == SudokuSolutionStatus.Unique, "known puzzle is unique");
        Check(SudokuSolutionCounter.Count(new int[9, 9]) == SudokuSolutionStatus.Multiple, "empty grid has multiple solutions");
        var invalid = Board(Solved); invalid[0, 0] = invalid[0, 1];
        Check(SudokuSolutionCounter.Count(invalid) == SudokuSolutionStatus.None, "duplicate givens rejected");
        var impossible = Board(Easy); impossible[0, 2] = 1;
        Check(SudokuSolutionCounter.Count(impossible) == SudokuSolutionStatus.None, "locally consistent unsatisfiable puzzle rejected");
        Check(SudokuSolutionCounter.Count(Board(Easy), 1) == SudokuSolutionStatus.Unknown, "node limit never certifies uniqueness");
        var counter = new SudokuSolutionCounter(Board(Easy));
        foreach (bool pause in counter.Run()) { }
        Check(counter.Status == SudokuSolutionStatus.Unique, "incremental verifier agrees");
        var easy = Board(Easy);
        var logic = SudokuLogicalEvaluator.Evaluate(easy);
        Check(logic.IsSolved && logic.Difficulty == SudokuDifficulty.Easy, "singles solve the easy fixture");
        Check(Text(easy) == Easy, "logical evaluation does not mutate input");
        Check(SudokuLogicalEvaluator.Evaluate(solution).Difficulty == SudokuDifficulty.Trivial, "full grid is trivial");
        Check(!SudokuLogicalEvaluator.Evaluate(new int[9, 9]).IsSolved, "stalled grid is not labelled hard");
        Check(!SudokuLogicalEvaluator.Evaluate(invalid).IsSolved, "inconsistent grid is not logically solved");
        var oneHole = Board(Solved); oneHole[0, 0] = 0;
        Check(SudokuLogicalEvaluator.Evaluate(oneHole).NakedSingleSteps == 1, "single missing cell uses one deduction");
        var settings = new SudokuPuzzleSettings();
        foreach (var target in new[] { SudokuDifficulty.Easy, SudokuDifficulty.Medium, SudokuDifficulty.Hard })
        {
            var preset = SudokuDifficultyPresets.Create(target);
            preset.Validate();
            var generated = SudokuPuzzleGenerator.Generate(solution, preset, unchecked((12345 * 397) ^ 0x5341));
            Check(generated.TargetsReached, "difficulty preset reaches exact targets");
            Check(SudokuSolutionCounter.Count(generated.Puzzle) == SudokuSolutionStatus.Unique, "preset output independently unique");
            preset.TargetClues = 17;
            Check(SudokuDifficultyPresets.Create(target).TargetClues != 17, "presets return independent settings");
        }
        var first = SudokuPuzzleGenerator.Generate(solution, settings, 12345);
        var second = SudokuPuzzleGenerator.Generate(solution, settings, 12345);
        Check(first.TargetsReached && first.ClueCount == 41, "default targets reached");
        Check(Text(first.Puzzle) == Text(second.Puzzle) && first.StepsPerformed == second.StepsPerformed, "seed reproduces result and effort");
        Check(SudokuSolutionCounter.Count(first.Puzzle) == SudokuSolutionStatus.Unique, "generated puzzle independently rechecked");
        Check(Text(solution) == Solved, "generation preserves solution");
        var copy = first.Puzzle; copy[0, 0] = 99;
        Check(first.Puzzle[0, 0] != 99, "result protects its puzzle from mutation");
        var puzzle = first.Puzzle; var givens = first.Givens;
        for (int i = 0; i < 81; i++)
            Check(givens[i / 9, i % 9] == (puzzle[i / 9, i % 9] != 0) &&
                (puzzle[i / 9, i % 9] == 0 || puzzle[i / 9, i % 9] == solution[i / 9, i % 9]), "clues match mask and solution");
        var limited = new SudokuPuzzleSettings { MaxSteps = 1, TargetDifficulty = SudokuDifficulty.Hard, MaxRestarts = 0 };
        var partial = SudokuPuzzleGenerator.Generate(solution, limited, 1);
        Check(!partial.TargetsReached && partial.StepsPerformed == 1 && partial.StopReason == AnnealingStopReason.StepLimit,
            "budget exhaustion is explicit");
        Check(SudokuSolutionCounter.Count(partial.Puzzle) == SudokuSolutionStatus.Unique, "approximate result remains unique");
        var unknown = SudokuPuzzleGenerator.Generate(solution, new SudokuPuzzleSettings { SolutionNodeLimit = 1, MaxSteps = 20 }, 7);
        Check(unknown.UnverifiedCandidates > 0 && !unknown.TargetsReached, "unverified candidates are rejected");
        Check(SudokuPuzzleGenerator.CalculateCost(41, logic, settings) == 0, "both objectives produce zero cost");
        foreach (var target in new[] { SudokuDifficulty.Medium, SudokuDifficulty.Hard })
        {
            var result = SudokuPuzzleGenerator.Generate(solution,
                new SudokuPuzzleSettings { TargetClues = 30, TargetDifficulty = target }, 1);
            Check(result.TargetsReached, "advanced logical target reached");
            var weaker = target == SudokuDifficulty.Medium ? SudokuLogicalTechnique.HiddenSingle : SudokuLogicalTechnique.LockedCandidates;
            Check(!SudokuLogicalEvaluator.Evaluate(result.Puzzle, weaker).IsSolved, "advanced level requires its additional techniques");
            Check(result.AcceptedWorseMoves > 0, "annealing explores temporary objective regressions");
        }
        ExpectInvalid(() => SudokuPuzzleGenerator.Generate(invalid, settings, 1));
        ExpectInvalid(() => new SudokuPuzzleSettings { DifficultyWeight = 0 }.Validate());
        ExpectInvalid(() => new SudokuPuzzleSettings { TargetDifficulty = SudokuDifficulty.Unrated }.Validate());
        ExpectInvalid(() => new SudokuPuzzleSettings { TargetClues = 16 }.Validate());
        ExpectInvalid(() => new SudokuPuzzleSettings { InitialTemperature = double.NaN }.Validate());
        GeneticTuning.CheckDefaults();
        if (args.Length > 0 && args[0] == "--benchmark") Benchmark(solution);
        Console.WriteLine($"PASS: {assertions} assertions.");
    }

    private static void Benchmark(int[,] solution)
    {
        foreach (var target in new[] { SudokuDifficulty.Easy, SudokuDifficulty.Medium, SudokuDifficulty.Hard })
        {
            int hits = 0, worse = 0;
            var watch = Stopwatch.StartNew();
            for (int seed = 1; seed <= 5; seed++)
            {
                var result = SudokuPuzzleGenerator.Generate(solution,
                    new SudokuPuzzleSettings { TargetClues = target == SudokuDifficulty.Easy ? 41 : 30, TargetDifficulty = target }, seed);
                Check(SudokuSolutionCounter.Count(result.Puzzle) == SudokuSolutionStatus.Unique, "benchmark output remains unique");
                if (result.TargetsReached) hits++;
                worse += result.AcceptedWorseMoves;
                Console.WriteLine($"{target} seed={seed}: clues={result.ClueCount}, level={result.Logic.Difficulty}, steps={result.StepsPerformed}, worse={result.AcceptedWorseMoves}");
            }
            Console.WriteLine($"{target}: {hits}/5 targets, {worse} uphill acceptances, {watch.ElapsedMilliseconds} ms");
        }
    }

    private static int[,] Board(string text)
    {
        var board = new int[9, 9];
        for (int i = 0; i < 81; i++) board[i / 9, i % 9] = text[i] - '0';
        return board;
    }

    private static string Text(int[,] board)
    {
        var text = new char[81];
        for (int i = 0; i < 81; i++) text[i] = (char)('0' + board[i / 9, i % 9]);
        return new string(text);
    }

    private static void ExpectInvalid(Action action)
    {
        try { action(); } catch (ArgumentException) { assertions++; return; }
        throw new Exception("Invalid input was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        assertions++;
    }
}
