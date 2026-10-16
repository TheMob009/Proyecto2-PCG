using System;
using PCG.Sudoku;
using UnityEngine;

public enum AnnealingStopReason
{
    AlreadySolved,
    SolutionFound,
    MinimumTemperature,
    StepLimit
}

/// <summary>Mejora un candidato existente conservando el mejor tablero visitado.</summary>
[DisallowMultipleComponent]
public sealed class SudokuSimulatedAnnealing : MonoBehaviour
{
    [SerializeField] private float initialTemperature = 10f;
    [SerializeField] private float coolingRate = 0.99f;
    [SerializeField] private float minTemperature = 0.001f;
    [SerializeField] private int maxSteps = 10000;

    public int StepsPerformed { get; private set; }
    public int InitialCost { get; private set; }
    public int BestCost { get; private set; }
    public float FinalTemperature { get; private set; }
    public AnnealingStopReason StopReason { get; private set; }

    public void SetParameters(float initial, float cooling, float minimum, int steps)
    {
        ValidateParameters(initial, cooling, minimum, steps);
        initialTemperature = initial;
        coolingRate = cooling;
        minTemperature = minimum;
        maxSteps = steps;
    }

    public SudokuCandidate Improve(SudokuCandidate initialCandidate, int seed)
    {
        ValidateParameters(initialTemperature, coolingRate, minTemperature, maxSteps);
        if (initialCandidate == null)
            throw new ArgumentNullException(nameof(initialCandidate));

        // Evaluar la matriz antes de clonar también valida tamaño y valores permitidos.
        int initialCost = SudokuEvaluator.Evaluate(initialCandidate.board).TotalConflicts;
        var random = new System.Random(seed);
        var current = initialCandidate.Clone();
        current.cost = initialCost;
        var best = current.Clone();
        float temperature = initialTemperature;
        StepsPerformed = 0;
        InitialCost = initialCost;

        while (current.cost > 0 && temperature > minTemperature && StepsPerformed < maxSteps)
        {
            SudokuCandidate next = GetSuccessor(current, random);
            double delta = current.cost - next.cost;
            // Un empeoramiento produce un exponente negativo; los empates se aceptan.
            if (delta >= 0 || random.NextDouble() < Math.Exp(delta / temperature))
                current = next;

            if (current.cost < best.cost)
                best = current.Clone();

            StepsPerformed++;
            temperature *= coolingRate;
        }

        BestCost = best.cost;
        FinalTemperature = temperature;
        StopReason = initialCost == 0 ? AnnealingStopReason.AlreadySolved :
            best.cost == 0 ? AnnealingStopReason.SolutionFound :
            StepsPerformed >= maxSteps ? AnnealingStopReason.StepLimit :
            AnnealingStopReason.MinimumTemperature;
        return best;
    }

    private static void ValidateParameters(float initial, float cooling, float minimum, int steps)
    {
        if (!IsFinite(minimum) || minimum <= 0f)
            throw new ArgumentOutOfRangeException(nameof(minimum), "La temperatura mínima debe ser positiva y finita.");
        if (!IsFinite(initial) || initial <= minimum)
            throw new ArgumentOutOfRangeException(nameof(initial), "La temperatura inicial debe superar a la mínima.");
        if (!IsFinite(cooling) || cooling <= 0f || cooling >= 1f)
            throw new ArgumentOutOfRangeException(nameof(cooling), "El enfriamiento debe estar entre 0 y 1, sin incluirlos.");
        if (steps < 1)
            throw new ArgumentOutOfRangeException(nameof(steps), "La cantidad de pasos debe ser positiva.");
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static SudokuCandidate GetSuccessor(SudokuCandidate current, System.Random random)
    {
        var neighbor = current.Clone();
        int row = random.Next(9);
        int columnA = random.Next(9);
        int columnB;
        do { columnB = random.Next(9); } while (columnA == columnB);

        int temporary = neighbor.board[row, columnA];
        neighbor.board[row, columnA] = neighbor.board[row, columnB];
        neighbor.board[row, columnB] = temporary;
        neighbor.cost = SudokuEvaluator.Evaluate(neighbor.board).TotalConflicts;
        return neighbor;
    }
}
