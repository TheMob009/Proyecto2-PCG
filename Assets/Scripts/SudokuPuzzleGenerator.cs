using System;
using System.Collections.Generic;

namespace PCG.Sudoku
{
    public enum AnnealingStopReason { TargetsReached, MinimumTemperature, StepLimit }

    [Serializable]
    public sealed class SudokuPuzzleSettings
    {
        public int TargetClues = 41;
        public SudokuDifficulty TargetDifficulty = SudokuDifficulty.Easy;
        public double InitialTemperature = 0.05;
        public double CoolingRate = 0.999;
        public double MinimumTemperature = 0.0001;
        public int MaxSteps = 5000;
        public int MaxRestarts = 2;
        public double ClueWeight = 1;
        public double DifficultyWeight = 1;
        public int SolutionNodeLimit = 20000;

        public SudokuPuzzleSettings Copy() => (SudokuPuzzleSettings)MemberwiseClone();

        public void Validate()
        {
            if (TargetClues < 17 || TargetClues > 80) throw new ArgumentOutOfRangeException(nameof(TargetClues));
            if (TargetDifficulty < SudokuDifficulty.Easy || TargetDifficulty > SudokuDifficulty.Hard)
                throw new ArgumentOutOfRangeException(nameof(TargetDifficulty));
            if (!Positive(MinimumTemperature) || !Positive(InitialTemperature) || InitialTemperature <= MinimumTemperature)
                throw new ArgumentException("La temperatura inicial debe superar a la mínima, ambas positivas y finitas.");
            if (!Positive(CoolingRate) || CoolingRate >= 1) throw new ArgumentOutOfRangeException(nameof(CoolingRate));
            if (MaxSteps < 1 || MaxRestarts < 0 || MaxRestarts > 20 || SolutionNodeLimit < 1)
                throw new ArgumentException("Revisa pasos, reinicios y presupuesto de verificación.");
            if (!Positive(ClueWeight) || !Positive(DifficultyWeight))
                throw new ArgumentException("Los pesos deben ser positivos y finitos.");
        }

        private static bool Positive(double value) => value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public sealed class SudokuPuzzleResult
    {
        private readonly int[,] puzzle;
        public int[,] Puzzle => (int[,])puzzle.Clone();
        public bool[,] Givens
        {
            get
            {
                var mask = new bool[9, 9];
                for (int i = 0; i < 81; i++) mask[i / 9, i % 9] = puzzle[i / 9, i % 9] != 0;
                return mask;
            }
        }
        public SudokuLogicalEvaluation Logic { get; internal set; }
        public int ClueCount { get; internal set; }
        public int TargetClues { get; internal set; }
        public SudokuDifficulty TargetDifficulty { get; internal set; }
        public bool TargetsReached => ClueCount == TargetClues && Logic.IsSolved && Logic.Difficulty == TargetDifficulty;
        public bool CanPlay => Logic.IsSolved && ClueCount < 81;
        public double InitialCost { get; internal set; }
        public double BestCost { get; internal set; }
        public int StepsPerformed { get; internal set; }
        public int RestartsPerformed { get; internal set; }
        public int AcceptedWorseMoves { get; internal set; }
        public int RejectedNonUnique { get; internal set; }
        public int UnverifiedCandidates { get; internal set; }
        public long VerificationNodes { get; internal set; }
        public double FinalTemperature { get; internal set; }
        public AnnealingStopReason StopReason { get; internal set; }

        internal SudokuPuzzleResult(int[] values) { puzzle = SudokuConstraints.Expand(values); }
    }

    public readonly struct SudokuGenerationProgress
    {
        public readonly int Steps;
        public readonly int Clues;
        public readonly double Cost;
        public readonly string Phase;
        internal SudokuGenerationProgress(int steps, int clues, double cost, string phase)
        { Steps = steps; Clues = clues; Cost = cost; Phase = phase; }
    }

    /// <summary>Selecciona pistas con SA; solo admite estados con unicidad comprobada.</summary>
    public sealed class SudokuPuzzleGenerator
    {
        private sealed class Candidate
        {
            internal int[] Values;
            internal int Clues;
            internal SudokuLogicalEvaluation Logic;
            internal double Cost;
        }

        private readonly int[] solution;
        private readonly SudokuPuzzleSettings settings;
        private readonly Random random;
        private readonly Dictionary<string, Candidate> cache = new Dictionary<string, Candidate>();
        private Candidate evaluated;
        private Candidate best;
        private bool started;
        private int steps, restarts, acceptedWorse, rejectedNonUnique, unverified;
        private long verificationNodes;
        private double temperature, initialCost;

        public SudokuPuzzleResult Result { get; private set; }

        public SudokuPuzzleGenerator(int[,] solvedBoard, SudokuPuzzleSettings settings, int seed)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            if (!SudokuEvaluator.Evaluate(solvedBoard).IsValid)
                throw new ArgumentException("La generación de pistas requiere una solución completa válida.", nameof(solvedBoard));
            solution = SudokuConstraints.Flatten(solvedBoard);
            this.settings = settings.Copy();
            random = new Random(seed);
        }

        public static SudokuPuzzleResult Generate(int[,] solvedBoard, SudokuPuzzleSettings settings, int seed)
        {
            var generator = new SudokuPuzzleGenerator(solvedBoard, settings, seed);
            foreach (SudokuGenerationProgress progress in generator.Run()) { }
            return generator.Result;
        }

        public static double CalculateCost(int clues, SudokuLogicalEvaluation logic, SudokuPuzzleSettings settings)
        {
            if (logic == null) throw new ArgumentNullException(nameof(logic));
            double clueError = Math.Abs(clues - settings.TargetClues) / 81.0;
            double difficultyError = (Math.Abs((int)logic.Difficulty - (int)settings.TargetDifficulty) / 4.0 +
                logic.RemainingCells / 81.0) / 2.0;
            return settings.ClueWeight * clueError + settings.DifficultyWeight * difficultyError;
        }

        public IEnumerable<SudokuGenerationProgress> Run()
        {
            if (started) throw new InvalidOperationException("La generación solo puede ejecutarse una vez.");
            started = true;
            best = BuildCandidate((int[])solution.Clone());
            initialCost = best.Cost;
            var reason = AnnealingStopReason.StepLimit;
            for (int attempt = 0; attempt <= settings.MaxRestarts && steps < settings.MaxSteps; attempt++)
            {
                restarts = attempt;
                temperature = settings.InitialTemperature;
                int remainingRuns = settings.MaxRestarts - attempt + 1;
                int runLimit = steps + Math.Max(1, (settings.MaxSteps - steps) / remainingRuns);
                Candidate current = BuildCandidate((int[])solution.Clone());
                int[] positions = ShuffledPositions();
                foreach (int position in positions)
                {
                    if (current.Clues <= settings.TargetClues || steps >= runLimit) break;
                    int[] values = (int[])current.Values.Clone();
                    values[position] = 0;
                    steps++;
                    foreach (bool pause in Evaluate(values)) yield return Progress("Verificando pistas");
                    if (evaluated != null) { current = evaluated; KeepBest(current); }
                    yield return Progress("Preparando pistas");
                    if (Reached(best)) break;
                }
                if (attempt == 0) initialCost = current.Cost;
                while (!Reached(best) && steps < runLimit && temperature > settings.MinimumTemperature)
                {
                    steps++;
                    int[] values = Neighbor(current);
                    foreach (bool pause in Evaluate(values)) yield return Progress("Verificando unicidad");
                    if (evaluated != null)
                    {
                        double delta = evaluated.Cost - current.Cost;
                        if (delta <= 0 || random.NextDouble() < Math.Exp(-delta / temperature))
                        {
                            if (delta > 0) acceptedWorse++;
                            current = evaluated;
                            KeepBest(current);
                        }
                    }
                    temperature *= settings.CoolingRate;
                    yield return Progress("Buscando cantidad y dificultad");
                }
                if (Reached(best)) { reason = AnnealingStopReason.TargetsReached; break; }
                reason = steps >= settings.MaxSteps ? AnnealingStopReason.StepLimit :
                    temperature <= settings.MinimumTemperature ? AnnealingStopReason.MinimumTemperature : AnnealingStopReason.StepLimit;
            }
            Result = new SudokuPuzzleResult(best.Values)
            {
                Logic = best.Logic, ClueCount = best.Clues, TargetClues = settings.TargetClues,
                TargetDifficulty = settings.TargetDifficulty, InitialCost = initialCost, BestCost = best.Cost,
                StepsPerformed = steps, RestartsPerformed = restarts, AcceptedWorseMoves = acceptedWorse,
                RejectedNonUnique = rejectedNonUnique, UnverifiedCandidates = unverified,
                VerificationNodes = verificationNodes, FinalTemperature = temperature, StopReason = reason
            };
        }

        private IEnumerable<bool> Evaluate(int[] values)
        {
            string key = Key(values);
            if (cache.TryGetValue(key, out evaluated)) yield break;
            evaluated = null;
            var counter = new SudokuSolutionCounter(SudokuConstraints.Expand(values), settings.SolutionNodeLimit);
            foreach (bool pause in counter.Run()) yield return pause;
            verificationNodes += counter.NodesVisited;
            if (counter.Status == SudokuSolutionStatus.Unique) evaluated = BuildCandidate(values);
            else if (counter.Status == SudokuSolutionStatus.Unknown) unverified++;
            else rejectedNonUnique++;
            if (cache.Count >= 1024) cache.Clear();
            cache[key] = evaluated;
        }

        private Candidate BuildCandidate(int[] values)
        {
            var candidate = new Candidate { Values = values, Logic = SudokuLogicalEvaluator.Evaluate(SudokuConstraints.Expand(values)) };
            foreach (int value in values) if (value != 0) candidate.Clues++;
            candidate.Cost = CalculateCost(candidate.Clues, candidate.Logic, settings);
            return candidate;
        }

        private int[] Neighbor(Candidate current)
        {
            var values = (int[])current.Values.Clone();
            var givens = new List<int>();
            var empty = new List<int>();
            for (int i = 0; i < 81; i++) (values[i] == 0 ? empty : givens).Add(i);
            int move = random.Next(3);
            if (move == 2 && givens.Count > 0 && empty.Count > 0)
            {
                values[givens[random.Next(givens.Count)]] = 0;
                int position = empty[random.Next(empty.Count)];
                values[position] = solution[position];
            }
            else if ((move == 0 && givens.Count > 17) || empty.Count == 0)
                values[givens[random.Next(givens.Count)]] = 0;
            else
            {
                int position = empty[random.Next(empty.Count)];
                values[position] = solution[position];
            }
            return values;
        }

        private int[] ShuffledPositions()
        {
            var positions = new int[81];
            for (int i = 0; i < 81; i++) positions[i] = i;
            for (int i = 80; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int temporary = positions[i]; positions[i] = positions[j]; positions[j] = temporary;
            }
            return positions;
        }

        private bool Reached(Candidate candidate) => candidate.Clues == settings.TargetClues &&
            candidate.Logic.IsSolved && candidate.Logic.Difficulty == settings.TargetDifficulty;

        private void KeepBest(Candidate candidate)
        {
            if (Reached(candidate) || candidate.Cost < best.Cost) best = candidate;
        }

        private SudokuGenerationProgress Progress(string phase) => new SudokuGenerationProgress(steps, best.Clues, best.Cost, phase);

        private static string Key(int[] values)
        {
            var key = new char[81];
            for (int i = 0; i < 81; i++) key[i] = values[i] == 0 ? '0' : '1';
            return new string(key);
        }
    }
}
