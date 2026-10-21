using PCG.Sudoku;
using UnityEngine;

/// <summary>Configura SA para seleccionar pistas con cantidad y dificultad lógica objetivo.</summary>
[DisallowMultipleComponent]
public sealed class SudokuSimulatedAnnealing : MonoBehaviour
{
    [Header("Perfiles de dificultad")]
    [Tooltip("Al cambiar Dificultad en la interfaz carga las pistas y los parámetros recomendados. Desactívalo para conservar ajustes manuales.")]
    [SerializeField] private bool useDifficultyPresets = true;
    public bool UseDifficultyPresets => useDifficultyPresets;

    [Header("Objetivos del puzzle")]
    [SerializeField, Range(17, 80)] private int targetClues = 41;
    [SerializeField] private SudokuDifficulty targetDifficulty = SudokuDifficulty.Easy;
    [SerializeField, Min(0.001f)] private float clueWeight = 1f;
    [SerializeField, Min(0.001f)] private float difficultyWeight = 1f;

    [Header("Exploración de pistas")]
    [SerializeField] private float initialTemperature = 0.05f;
    [SerializeField] private float coolingRate = 0.999f;
    [SerializeField] private float minTemperature = 0.0001f;
    [SerializeField, Min(1)] private int maxSteps = 5000;
    [SerializeField, Range(0, 20)] private int maxRestarts = 2;
    [SerializeField, Min(1)] private int solutionNodeLimit = 20000;

    public SudokuPuzzleSettings Settings => new SudokuPuzzleSettings
    {
        TargetClues = targetClues, TargetDifficulty = targetDifficulty, ClueWeight = clueWeight,
        DifficultyWeight = difficultyWeight, InitialTemperature = initialTemperature,
        CoolingRate = coolingRate, MinimumTemperature = minTemperature, MaxSteps = maxSteps,
        MaxRestarts = maxRestarts, SolutionNodeLimit = solutionNodeLimit
    };

    public void SetParameters(SudokuPuzzleSettings settings)
    {
        settings.Validate();
        targetClues = settings.TargetClues;
        targetDifficulty = settings.TargetDifficulty;
        clueWeight = (float)settings.ClueWeight;
        difficultyWeight = (float)settings.DifficultyWeight;
        initialTemperature = (float)settings.InitialTemperature;
        coolingRate = (float)settings.CoolingRate;
        minTemperature = (float)settings.MinimumTemperature;
        maxSteps = settings.MaxSteps;
        maxRestarts = settings.MaxRestarts;
        solutionNodeLimit = settings.SolutionNodeLimit;
    }

    public SudokuPuzzleGenerator CreateGenerator(int[,] solvedBoard, int seed) =>
        new SudokuPuzzleGenerator(solvedBoard, Settings, seed);
}
