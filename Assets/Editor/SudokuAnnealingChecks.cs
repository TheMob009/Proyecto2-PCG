using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PCG.Sudoku.Editor
{
    /// <summary>Valida los perfiles en el runtime real de Unity con soluciones distintas de GA.</summary>
    public static class SudokuAnnealingChecks
    {
        [MenuItem("Tools/PCG/Verificar perfiles de dificultad de SA")]
        public static void RunIsolated()
        {
            const int samples = 100;
            var owner = new GameObject("SA profile validation");
            var rows = new List<string> { "difficulty,seed,hit,clues,actual,steps,milliseconds" };
            var failures = new List<string>();
            try
            {
                var algorithm = owner.AddComponent<SudokuGeneticA>();
                var solutions = new int[samples][,];
                for (int i = 0; i < samples; i++)
                {
                    solutions[i] = algorithm.Generate(Seed(i)).board;
                    if (!SudokuEvaluator.Evaluate(solutions[i]).IsValid)
                        throw new InvalidOperationException("GA no obtuvo una solución válida: " + Seed(i));
                }
                foreach (var difficulty in new[] { SudokuDifficulty.Easy, SudokuDifficulty.Medium, SudokuDifficulty.Hard })
                {
                    int hits = 0;
                    double total = 0, maximum = 0;
                    for (int i = 0; i < samples; i++)
                    {
                        var settings = SudokuDifficultyPresets.Create(difficulty);
                        var watch = Stopwatch.StartNew();
                        var result = SudokuPuzzleGenerator.Generate(solutions[i], settings, unchecked((Seed(i) * 397) ^ 0x5341));
                        watch.Stop();
                        double ms = watch.Elapsed.TotalMilliseconds;
                        total += ms;
                        maximum = Math.Max(maximum, ms);
                        if (SudokuSolutionCounter.Count(result.Puzzle, 1000000) != SudokuSolutionStatus.Unique)
                            throw new InvalidOperationException("El puzzle no es único.");
                        if (result.TargetsReached)
                        {
                            hits++;
                            var weaker = difficulty == SudokuDifficulty.Hard ? SudokuLogicalTechnique.LockedCandidates : SudokuLogicalTechnique.HiddenSingle;
                            if (difficulty != SudokuDifficulty.Easy && SudokuLogicalEvaluator.Evaluate(result.Puzzle, weaker).IsSolved)
                                throw new InvalidOperationException("El puzzle se resuelve con técnicas de un nivel inferior.");
                        }
                        else failures.Add(difficulty + ":" + Seed(i));
                        rows.Add(string.Join(",", difficulty, Seed(i), result.TargetsReached, result.ClueCount,
                            result.Logic.Difficulty, result.StepsPerformed, ms.ToString("F3", CultureInfo.InvariantCulture)));
                        if (!result.TargetsReached || (i + 1) % 10 == 0)
                            Debug.Log($"SA {difficulty} {i + 1}/{samples}: hit={result.TargetsReached}, clues={result.ClueCount}, actual={result.Logic.Difficulty}, steps={result.StepsPerformed}");
                    }
                    Debug.Log($"SA PROFILE SUMMARY {difficulty}: {hits}/{samples}; mean={total / samples:F1}ms; max={maximum:F1}ms");
                }
                Directory.CreateDirectory(".utmp");
                File.WriteAllLines(".utmp/sa-consistency.csv", rows);
                if (failures.Count > 0) throw new InvalidOperationException("Objetivos pendientes: " + string.Join(", ", failures));
                Debug.Log("PASS SA profiles: 300/300 exact targets, independently unique, advanced techniques required.");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        private static int Seed(int index)
        {
            int[] reference = { 12345, 0, -1, int.MinValue, int.MaxValue, 1, 2, 42, 2026, 987654321 };
            return index < reference.Length ? reference[index] : unchecked(-7654321 + index * 104729);
        }
    }
}
