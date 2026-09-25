using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GeneticAlgorithm : MonoBehaviour
{
    [Header("Genetic Algorithm")]

    /*
     * populationSize = cantidad de individuos de la población.
     *
     * Si populationSize = 50 y teamSize = 6:
     *
     * population = [
     *   [25, 131, 94, 212, 135, 445],   // individuo 0
     *   [6, 150, 197, 376, 59, 248],     // individuo 1
     *   [143, 448, 130, 214, 94, 445],   // individuo 2
     *   ...
     *   [...]                              // individuo 49
     * ]
     *
     * population.Count = 50
     * population[i].genes.Length = 6
     *
     * Cada gen es el índice de un Pokémon dentro del dataset.
     */
    [Min(2)]
    [SerializeField] private int populationSize = 50;

    /*
     * generations = cantidad de veces que repetimos el ciclo evolutivo.
     *
     * generations = 80
     *
     * Generation 0  -> población inicial
     * Generation 1  -> nueva población
     * ...
     * Generation 80 -> población final
     */
    [Min(1)]
    [SerializeField] private int generations = 80;

    /*
     * crossoverRate = probabilidad de cruzar dos padres.
     *
     * crossoverRate = 0.80  -> aproximadamente 80%.
     */
    [Range(0f, 1f)]
    [SerializeField] private float crossoverRate = 0.80f;

    /*
     * mutationRate = probabilidad de reemplazar cada gen.
     *
     * mutationRate = 0.12
     *
     * Cada una de las 6 posiciones del equipo tiene 12%
     * de probabilidad de ser reemplazada por otro Pokémon.
     */
    [Range(0f, 1f)]
    [SerializeField] private float mutationRate = 0.12f;

    /*
     * tournamentSize = cantidad de individuos que compiten
     * para seleccionar un padre.
     *
     * tournamentSize = 3
     *
     * candidatos:
     * A -> fitness 0.72
     * B -> fitness 0.91
     * C -> fitness 0.84
     *
     * seleccionado -> B
     */
    [Min(2)]
    [SerializeField] private int tournamentSize = 3;

    /*
     * elitism = mejores individuos que pasan directamente
     * a la siguiente generación.
     *
     * elitism = 2
     *
     * nextPopulation comienza con:
     * [bestIndividual, secondBestIndividual]
     */
    [Min(0)]
    [SerializeField] private int elitism = 2;

    [Header("Debug")]
    [SerializeField] private bool logProgress = true;

    [Header("Runtime UI (optional)")]
    [SerializeField] private Slider populationSizeSlider;
    [SerializeField] private Slider generationsSlider;
    [SerializeField] private Slider crossoverRateSlider;
    [SerializeField] private Slider mutationRateSlider;
    [SerializeField] private Slider tournamentSizeSlider;
    [SerializeField] private Slider elitismSlider;

    /*
     * ============================================================
     * ALGORITMO GENÉTICO
     * ============================================================
     *
     * PSEUDOCÓDIGO GENERAL
     * ------------------------------------------------------------
     * 1: create random population
     * 2: evaluate every individual
     *
     * 3: repeat for N generations:
     * 4:      sort population by fitness
     * 5:      create empty nextPopulation
     * 6:      copy elite individuals
     *
     * 7:      while nextPopulation is not full:
     * 8:          parentA = tournament selection
     * 9:          parentB = tournament selection
     *
     * 10:         if random < crossoverRate:
     * 11:             child = crossover(parentA, parentB)
     * 12:         else:
     * 13:             child = copy(parentA)
     *
     * 14:         mutate(child)
     * 15:         evaluate(child)
     * 16:         add child to nextPopulation
     *
     * 17:     population = nextPopulation
     *
     * 18: select generated content from final population
     *
     * EJEMPLO DE UNA GENERACIÓN
     * ------------------------------------------------------------
     * populationSize = 4
     *
     * A = [25, 131, 94, 212, 135, 445] fitness 0.92
     * B = [6, 150, 197, 376, 59, 248]   fitness 0.87
     * C = [1, 4, 7, 25, 39, 52]         fitness 0.63
     * D = [10, 11, 12, 13, 14, 15]      fitness 0.55
     *
     * elitism = 1
     *
     * nextPopulation empieza como [A]
     * y luego se completa con hijos hasta volver a tener 4 individuos.
     */
    public PokemonTeamCandidate Generate(
        IReadOnlyList<PokemonData> dataset,
        PokemonTeamGenerationConfig config)
    {
        if (dataset == null || dataset.Count == 0)
            return null;

        if (config == null || config.teamSize < 1)
        {
            Debug.LogError("[GeneticAlgorithm] A configuration with at least one team member is required.");
            return null;
        }

        // Leer la interfaz en cada ejecución; sin interfaz se usa el Inspector.
        if (populationSizeSlider != null)
            populationSize = Mathf.Max(2, Mathf.RoundToInt(populationSizeSlider.value));
        if (generationsSlider != null)
            generations = Mathf.Max(1, Mathf.RoundToInt(generationsSlider.value));
        if (crossoverRateSlider != null)
            crossoverRate = Mathf.Clamp01(crossoverRateSlider.value);
        if (mutationRateSlider != null)
            mutationRate = Mathf.Clamp01(mutationRateSlider.value);
        if (tournamentSizeSlider != null)
            tournamentSize = Mathf.Clamp(Mathf.RoundToInt(tournamentSizeSlider.value), 2, populationSize);
        if (elitismSlider != null)
            elitism = Mathf.Clamp(Mathf.RoundToInt(elitismSlider.value), 0, populationSize);

        System.Random random = new System.Random();

        List<PokemonTeamCandidate> population =
            CreateInitialPopulation(dataset.Count, config, random);
        EvaluatePopulation(population, dataset, config);
        population.Sort((a, b) => b.fitness.CompareTo(a.fitness));

        int eliteCount = Mathf.Clamp(elitism, 0, population.Count);

        for (int generation = 0; generation < generations; generation++)
        {
            List<PokemonTeamCandidate> nextPopulation =
                new List<PokemonTeamCandidate>(population.Count);

            // Copiar los mejores protege sus genes de las mutaciones de los hijos.
            for (int i = 0; i < eliteCount; i++)
                nextPopulation.Add(population[i].Clone());

            while (nextPopulation.Count < population.Count)
            {
                PokemonTeamCandidate parentA = TournamentSelection(population, random);
                PokemonTeamCandidate parentB = TournamentSelection(population, random);

                PokemonTeamCandidate child = random.NextDouble() < crossoverRate
                    ? Crossover(parentA, parentB, random)
                    : parentA.Clone();

                Mutate(child, dataset.Count, random);
                PokemonTeamFitness.Evaluate(child, dataset, config);
                nextPopulation.Add(child);
            }

            population = nextPopulation;
            population.Sort((a, b) => b.fitness.CompareTo(a.fitness));

            if (logProgress)
            {
                Debug.Log(
                    $"[GeneticAlgorithm] Generation {generation + 1}/{generations}: " +
                    $"best fitness = {population[0].fitness:0.000}"
                );
            }
        }

        // La selección entregada permite variar el contenido entre equipos válidos.
        return PokemonTeamFitness.SelectGeneratedContent(population, config, random);
    }

    /*
     * ============================================================
     * CREAR POBLACIÓN INICIAL
     * ============================================================
     *
     * populationSize = 50
     * teamSize = 6
     * datasetSize = 1032
     *
     * Un individuo posible:
     * [25, 131, 94, 212, 135, 445]
     *
     * Cada valor debe estar entre 0 y datasetSize - 1.
     *
     * Resultado:
     * population.Count = 50
     * population[i].genes.Length = 6
     *
     * PSEUDOCÓDIGO
     * ------------------------------------------------------------
     * 1: create empty population
     * 2: repeat populationSize times:
     * 3:      create candidate with teamSize genes
     * 4:      for each gene:
     * 5:          assign random dataset index
     * 6:      add candidate to population
     * 7: return population
     */
    private List<PokemonTeamCandidate> CreateInitialPopulation(
        int datasetSize,
        PokemonTeamGenerationConfig config,
        System.Random random)
    {
        int count = Mathf.Max(2, populationSize);
        List<PokemonTeamCandidate> population = new List<PokemonTeamCandidate>(count);

        for (int i = 0; i < count; i++)
        {
            PokemonTeamCandidate candidate = new PokemonTeamCandidate(config.teamSize);

            for (int gene = 0; gene < candidate.genes.Length; gene++)
                candidate.genes[gene] = random.Next(datasetSize);

            population.Add(candidate);
        }

        return population;
    }

    /*
     * ============================================================
     * TOURNAMENT SELECTION
     * ============================================================
     *
     * tournamentSize = 3
     *
     * candidate A -> fitness 0.62
     * candidate B -> fitness 0.91
     * candidate C -> fitness 0.78
     *
     * ganador -> candidate B
     *
     * PSEUDOCÓDIGO
     * ------------------------------------------------------------
     * 1: best = null
     * 2: repeat tournamentSize times:
     * 3:      choose random candidate from population
     * 4:      if best is null OR candidate fitness > best fitness:
     * 5:          best = candidate
     * 6: return best
     */
    private PokemonTeamCandidate TournamentSelection(
        List<PokemonTeamCandidate> population,
        System.Random random)
    {
        PokemonTeamCandidate best = null;

        for (int i = 0; i < Mathf.Max(2, tournamentSize); i++)
        {
            PokemonTeamCandidate candidate = population[random.Next(population.Count)];

            if (best == null || candidate.fitness > best.fitness)
                best = candidate;
        }

        return best;
    }

    /*
     * ============================================================
     * CROSSOVER
     * ============================================================
     *
     * parentA:
     * [25, 131, 94 | 212, 135, 445]
     *
     * parentB:
     * [6, 150, 197 | 376, 59, 248]
     *
     * crossoverPoint = 3
     *
     * child:
     * [25, 131, 94 | 376, 59, 248]
     *
     * PSEUDOCÓDIGO
     * ------------------------------------------------------------
     * 1: create empty child
     * 2: choose crossoverPoint between 1 and geneCount - 1
     * 3: for each gene position:
     * 4:      if position < crossoverPoint:
     * 5:          copy gene from parentA
     * 6:      else:
     * 7:          copy gene from parentB
     * 8: return child
     */
    private PokemonTeamCandidate Crossover(
        PokemonTeamCandidate parentA,
        PokemonTeamCandidate parentB,
        System.Random random)
    {
        int geneCount = parentA.genes.Length;

        // Un equipo de un solo integrante no tiene un punto de corte interno.
        if (geneCount < 2)
            return parentA.Clone();

        PokemonTeamCandidate child = new PokemonTeamCandidate(geneCount);
        int crossoverPoint = random.Next(1, geneCount);

        for (int i = 0; i < geneCount; i++)
        {
            child.genes[i] = i < crossoverPoint
                ? parentA.genes[i]
                : parentB.genes[i];
        }

        return child;
    }

    /*
     * ============================================================
     * MUTACIÓN
     * ============================================================
     *
     * mutationRate = 0.12
     *
     * Antes:
     * [25, 131, 94, 212, 135, 445]
     *
     * Si muta la posición 3:
     * [25, 131, 94, 700, 135, 445]
     *
     * PSEUDOCÓDIGO
     * ------------------------------------------------------------
     * 1: for each gene:
     * 2:      generate random value between 0 and 1
     * 3:      if random value < mutationRate:
     * 4:          replace gene with random dataset index
     */
    private void Mutate(
        PokemonTeamCandidate candidate,
        int datasetSize,
        System.Random random)
    {
        if (datasetSize < 2)
            return;

        for (int i = 0; i < candidate.genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                // Elegir otro índice sin reintentos y sin modificar a los padres.
                int replacement = random.Next(datasetSize - 1);
                if (replacement >= candidate.genes[i])
                    replacement++;

                candidate.genes[i] = replacement;
            }
        }
    }

    private void EvaluatePopulation(
        List<PokemonTeamCandidate> population,
        IReadOnlyList<PokemonData> dataset,
        PokemonTeamGenerationConfig config)
    {
        foreach (PokemonTeamCandidate candidate in population)
        {
            PokemonTeamFitness.Evaluate(candidate, dataset, config);
        }
    }
}
