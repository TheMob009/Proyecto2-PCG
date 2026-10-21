using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PCG.Sudoku.Editor
{
    /// <summary>Valida la consistencia del GA real en el runtime de Unity, sin renderizado.</summary>
    public static class SudokuGeneticChecks
    {
        [MenuItem("Tools/PCG/Verificar parámetros predeterminados de GA")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Ejecuta las comprobaciones fuera de Play.");
            EditorSceneManager.OpenScene("Assets/Scenes/Proyecto2 V2.unity", OpenSceneMode.Single);
            var algorithm = UnityEngine.Object.FindObjectOfType<SudokuGeneticA>(true);
            if (algorithm == null) throw new InvalidOperationException("Falta el GA en la escena.");
            Validate(algorithm);
        }

        public static void RunIsolated()
        {
            var owner = new GameObject("GA consistency check");
            try { Validate(owner.AddComponent<SudokuGeneticA>()); }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        public static void RunPipeline()
        {
            var owner = new GameObject("GA to SA check");
            try
            {
                var algorithm = owner.AddComponent<SudokuGeneticA>();
                SudokuCandidate solution = algorithm.Generate(12345);
                if (!SudokuEvaluator.Evaluate(solution.board).IsValid)
                    throw new InvalidOperationException("GA no obtuvo una solución válida para el flujo completo.");
                var puzzle = SudokuPuzzleGenerator.Generate(solution.board, new SudokuPuzzleSettings(), unchecked((12345 * 397) ^ 0x5341));
                if (!puzzle.TargetsReached || SudokuSolutionCounter.Count(puzzle.Puzzle) != SudokuSolutionStatus.Unique)
                    throw new InvalidOperationException("El flujo GA → SA no alcanzó los objetivos con unicidad comprobada.");
                Debug.Log($"PASS GA to SA pipeline: zero conflicts, {puzzle.ClueCount} clues, {puzzle.Logic.Difficulty}, unique solution.");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        private static void Validate(SudokuGeneticA algorithm)
        {
            const int sampleSize = 100;
            var elapsed = new List<double>();
            var failed = new List<int>();
            var parameters = new SerializedObject(algorithm);
            Debug.Log($"GA parameters: population={parameters.FindProperty("populationSize").intValue}, " +
                $"generations={parameters.FindProperty("generations").intValue}, " +
                $"cross={parameters.FindProperty("crossoverRate").floatValue}, " +
                $"mutation={parameters.FindProperty("mutationRate").floatValue}, " +
                $"tournament={parameters.FindProperty("tournamentSize").intValue}, " +
                $"elites={parameters.FindProperty("eliteCount").intValue}");
            int[] referenceSeeds = { 12345, 0, -1, int.MinValue, int.MaxValue, 1, 2, 42, 2026, 987654321 };
            for (int i = 0; i < sampleSize; i++)
            {
                int seed = i < referenceSeeds.Length ? referenceSeeds[i] : unchecked(-184736254 + i * 104729);
                var watch = Stopwatch.StartNew();
                SudokuCandidate candidate = algorithm.Generate(seed);
                watch.Stop();
                elapsed.Add(watch.Elapsed.TotalMilliseconds);
                int cost = SudokuEvaluator.Evaluate(candidate.board).TotalConflicts;
                if (cost != 0) failed.Add(seed);
                if (cost != 0 || (i + 1) % 10 == 0)
                    Debug.Log($"GA validation {i + 1}/{sampleSize}: seed={seed}, conflicts={cost}, elapsed={watch.Elapsed.TotalMilliseconds:F1}ms");
            }
            SudokuCandidate first = algorithm.Generate(12345);
            SudokuCandidate repeated = algorithm.Generate(12345);
            for (int i = 0; i < 81; i++)
                if (first.board[i / 9, i % 9] != repeated.board[i / 9, i % 9])
                    throw new InvalidOperationException("La semilla no reproduce el tablero de GA.");
            elapsed.Sort();
            double total = 0;
            foreach (double value in elapsed) total += value;
            Debug.Log($"GA SUMMARY: {sampleSize - failed.Count}/{sampleSize} solved; mean={total / sampleSize:F1}ms; " +
                $"median={elapsed[sampleSize / 2]:F1}ms; p95={elapsed[(int)(sampleSize * 0.95) - 1]:F1}ms; max={elapsed[sampleSize - 1]:F1}ms");
            if (failed.Count > 0) throw new InvalidOperationException("GA dejó conflictos en las semillas: " + string.Join(", ", failed));
            Debug.Log("PASS GA default consistency: 100 seeds, zero conflicts, reproducible default seed.");
        }
    }
}
