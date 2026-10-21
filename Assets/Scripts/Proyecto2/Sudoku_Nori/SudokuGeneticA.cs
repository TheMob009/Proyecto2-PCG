using PCG.Sudoku;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SudokuGeneticA : MonoBehaviour
{
    public const int DefaultPopulationSize = 200;
    public const int DefaultGenerations = 5000;
    public const float DefaultCrossoverRate = 0.3f;
    public const float DefaultMutationRate = 0.15f;
    public const int DefaultTournamentSize = 5;
    public const int DefaultEliteCount = 5;

    [SerializeField] private int populationSize = DefaultPopulationSize;
    [SerializeField] private int generations = DefaultGenerations;
    [SerializeField] private float crossoverRate = DefaultCrossoverRate;
    [SerializeField] private float mutationRate = DefaultMutationRate;
    [SerializeField] private int tournamentSize = DefaultTournamentSize;
    [SerializeField] private int eliteCount = DefaultEliteCount;


    public void SetParameters(int popSize, int gens, float crossRate, float mutRate, int tourSize, int elite)
    {
        populationSize = popSize;
        generations = gens;
        crossoverRate = crossRate;
        mutationRate = mutRate;
        tournamentSize = tourSize;
        eliteCount = elite;
    }

    public SudokuCandidate Generate(int seed)
    {
        System.Random random = new System.Random(seed);

        List<SudokuCandidate> population = new List<SudokuCandidate>();
        var initialBoard = SudokuRandomStartState.Generate(populationSize, seed);

        for(int i = 0; i < initialBoard.Count;i++)
        {
            SudokuCandidate candidate = new SudokuCandidate(initialBoard[i]);
            candidate.cost = SudokuEvaluator.Evaluate(candidate.board).TotalConflicts;
            population.Add(candidate);

        }

        population.Sort((a,b) => a.cost.CompareTo(b.cost));

        for(int i = 0; i < generations; i++)
        {
            if (population[0].cost == 0)
            {
                break;
            }

            List<SudokuCandidate> nextPopulation = new List<SudokuCandidate>(populationSize);
            for(int j = 0; j < eliteCount; j++)
            {
                nextPopulation.Add(population[j].Clone());
            }

            while (nextPopulation.Count < population.Count)
            {
                SudokuCandidate parentA = TournamentSelection(population, random);
                SudokuCandidate parentB = TournamentSelection(population, random);

                SudokuCandidate child;
                if(random.NextDouble() < crossoverRate)
                {
                    child = Crossover(parentA, parentB, random);
                }
                else
                {
                    child = parentA.Clone();
                }

                Mutate(child, random);
                child.cost = SudokuEvaluator.Evaluate(child.board).TotalConflicts;
                nextPopulation.Add(child);
            }

            population = nextPopulation;
            population.Sort((a, b) => a.cost.CompareTo(b.cost));

        }

        return population[0];
    }

    private SudokuCandidate TournamentSelection(List<SudokuCandidate> population, System.Random random)
    {
        SudokuCandidate best = null;
        for (int i = 0; i < tournamentSize; i++)
        {
           var participante = population[random.Next(population.Count)];

            if(best == null || participante.cost < best.cost)
            {
                best = participante;
            }

        }

        return best;
    }

    private SudokuCandidate Crossover(SudokuCandidate parentA, SudokuCandidate parentB, System.Random random)
    {
        SudokuCandidate child = new SudokuCandidate();
        int crossoverPoint = random.Next(1, 9);

        for (int i = 0; i < 9; i++)
        {
            for (int j = 0; j < 9; j++)
            {
                if (i < crossoverPoint)
                {
                    //para cada fila desde 0 hasta crossover point, el hijo toma los valores del padre A
                    child.board[i, j] = parentA.board[i, j];
                }
                else
                {
                    //para cada fila desde crossover point hasta 9, el hijo toma los valores del padre B
                    child.board[i, j] = parentB.board[i, j];
                }
            }
        }

        return child;
    }

    private void Mutate(SudokuCandidate candidate, System.Random random)
    {
        for(int i = 0; i < 9; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                int columnaA = random.Next(9);
                int columnaB = random.Next(9);

                var aux = candidate.board[i, columnaA];
                candidate.board[i, columnaA] = candidate.board[i, columnaB];
                candidate.board[i, columnaB] = aux;

            }

        }
    }
}
