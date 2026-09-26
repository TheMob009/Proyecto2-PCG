using System;
using System.Collections.Generic;
using UnityEngine;

public class EvolutionaryStrategy : MonoBehaviour
{
    [Header("(mu + lambda) Evolution Strategy")]

    /*
     * mu = cantidad de padres que sobreviven.
     *
     * mu = 15
     * parents.Count = 15
     *
     * parents = [
     *   [25, 131, 94, 212, 135, 445],
     *   [6, 150, 197, 376, 59, 248],
     *   ...
     *   [...] // padre 14
     * ]
     */
    [Min(1)]
    [SerializeField] private int mu = 15;

    /*
     * lambda = cantidad de hijos generados en cada generación.
     *
     * lambda = 45
     * offspring.Count = 45
     *
     * Cada hijo se obtiene copiando un padre y aplicando mutación.
     */
    [Min(1)]
    [SerializeField] private int lambda = 45;

    [Min(1)]
    [SerializeField] private int generations = 80;

    /*
     * mutationRate = probabilidad de mutar cada gen.
     *
     * mutationRate = 0.20
     *
     * Cada una de las 6 posiciones tiene 20% de probabilidad
     * de ser reemplazada por otro Pokémon.
     */
    [Range(0f, 1f)]
    [SerializeField] private float mutationRate = 0.20f;

    [Header("Debug")]
    [Tooltip("Si está activo, muestra una línea por generación en la consola.")]
    [SerializeField] private bool logProgress = true;

    /*
     * ============================================================
     * (mu + lambda) EVOLUTION STRATEGY
     * ============================================================
     *
     * DIFERENCIA PRINCIPAL CON GA
     * ------------------------------------------------------------
     * - NO utilizamos crossover.
     * - Los hijos se crean desde un padre.
     * - La principal fuente de variación es la mutación.
     * - Padres e hijos compiten juntos.
     *
     * PSEUDOCÓDIGO GENERAL
     * ------------------------------------------------------------
     * 1: create mu random parents
     * 2: evaluate parents
     *
     * 3: repeat for N generations:
     * 4:      create empty offspring
     *
     * 5:      repeat lambda times:
     * 6:          choose random parent
     * 7:          child = copy(parent)
     * 8:          mutate(child)
     * 9:          evaluate(child)
     * 10:         add child to offspring
     *
     * 11:     combined = parents + offspring
     * 12:     sort combined by fitness
     * 13:     keep best mu individuals as new parents
     *
     * 14: select generated content from final parents
     */
    public PokemonTeamCandidate Generate(
        IReadOnlyList<PokemonData> dataset,
        PokemonTeamGenerationConfig config)
    {
        // Si esta línea NO aparece en la consola, el problema está en el
        // controlador o en la UI: nunca se llamó a este método.
        Debug.Log("[ES] ===== Generate() llamado =====");

        if (dataset == null || dataset.Count == 0)
        {
            Debug.LogError("[ES] Dataset nulo o vacío. No se puede generar.");
            return null;
        }

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        System.Random random = new System.Random();

        Debug.Log(
            $"[ES] Parámetros -> mu: {mu}, lambda: {lambda}, generations: {generations}, " +
            $"mutationRate: {mutationRate:0.00}\n" +
            $"[ES] Config -> teamSize: {config.teamSize}, " +
            $"BST objetivo: {config.minAverageBST}-{config.maxAverageBST}, " +
            $"tipos objetivo: {config.targetUniqueTypes}, " +
            $"umbral fitness: {config.outputFitnessThreshold:0.00}\n" +
            $"[ES] Dataset: {dataset.Count} Pokémon"
        );

        // 1: Crear "mu" padres aleatorios.
        List<PokemonTeamCandidate> parents = CreateInitialParents(mu, dataset.Count, config, random);
        Debug.Log($"[ES] Padres iniciales creados: {parents.Count} (esperado: {mu})");

        // 2: Evaluar padres.
        EvaluatePopulation(parents, dataset, config);
        LogPopulationStats("Padres iniciales", parents);
        LogBestTeam("Mejor padre inicial", parents, dataset);

        float initialBest = GetBestFitness(parents);

        // 3: Repetir durante "generations".
        for (int i = 0; i < generations; i++)
        {
            List<PokemonTeamCandidate> offsprings = new List<PokemonTeamCandidate>();
            int totalChangedGenes = 0;

            for (int j = 0; j < lambda; j++)
            {
                int randomParent = random.Next(parents.Count);
                PokemonTeamCandidate chosenParent = parents[randomParent];
                PokemonTeamCandidate child = chosenParent.Clone();
                Mutate(child, dataset.Count, random);

                // Verifica que la mutación realmente haya cambiado algo
                int changedGenes = CountChangedGenes(chosenParent, child);
                totalChangedGenes += changedGenes;

                if (changedGenes == 0)
                    Debug.LogError($"[ES] Gen {i + 1}, hijo {j}: la mutación no cambió ningún gen.");

                PokemonTeamFitness.Evaluate(child, dataset, config);
                offsprings.Add(child);
            }

            List<PokemonTeamCandidate> combined = new List<PokemonTeamCandidate>();
            combined.AddRange(parents);
            combined.AddRange(offsprings);

            if (combined.Count != mu + lambda)
            {
                Debug.LogError(
                    $"[ES] Gen {i + 1}: combined tiene {combined.Count} elementos, " +
                    $"se esperaban {mu + lambda}."
                );
            }

            combined.Sort((a, b) => b.fitness.CompareTo(a.fitness));

            List<PokemonTeamCandidate> bestCandidates = new List<PokemonTeamCandidate>();
            for (int k = 0; k < mu; k++)
            {
                bestCandidates.Add(combined[k]);
            }

            // Cuántos de los nuevos padres son hijos de esta generación.
            // Si siempre es 0, los hijos nunca superan a los padres.
            HashSet<PokemonTeamCandidate> offspringSet = new HashSet<PokemonTeamCandidate>(offsprings);
            int survivingChildren = 0;
            foreach (PokemonTeamCandidate candidate in bestCandidates)
            {
                if (offspringSet.Contains(candidate))
                    survivingChildren++;
            }

            parents = bestCandidates;

            if (logProgress)
            {
                Debug.Log(
                    $"[ES] Gen {i + 1}/{generations} | " +
                    $"mejor: {parents[0].fitness:0.000} | " +
                    $"promedio: {GetAverageFitness(parents):0.000} | " +
                    $"peor: {parents[parents.Count - 1].fitness:0.000} | " +
                    $"hijos que sobrevivieron: {survivingChildren}/{mu} | " +
                    $"genes mutados por hijo: {(float)totalChangedGenes / lambda:0.00}"
                );
            }
        }

        // Resumen final de la evolución
        LogPopulationStats("Padres finales", parents);
        LogBestTeam("Mejor padre final", parents, dataset);

        float finalBest = GetBestFitness(parents);
        Debug.Log(
            $"[ES] Mejora del mejor fitness: {initialBest:0.000} -> {finalBest:0.000} " +
            $"(diferencia: {finalBest - initialBest:0.000})"
        );

        int aboveThreshold = 0;
        foreach (PokemonTeamCandidate candidate in parents)
        {
            if (candidate.fitness >= config.outputFitnessThreshold)
                aboveThreshold++;
        }
        Debug.Log(
            $"[ES] Padres que superan el umbral {config.outputFitnessThreshold:0.00}: " +
            $"{aboveThreshold}/{parents.Count}"
        );

        // 14: Seleccionar el contenido generado.
        PokemonTeamCandidate result = PokemonTeamFitness.SelectGeneratedContent(parents, config, random);

        stopwatch.Stop();

        if (result == null)
            Debug.LogError("[ES] SelectGeneratedContent devolvió null.");
        else
            LogTeamDetails("Equipo entregado a la UI", result, dataset);

        Debug.Log($"[ES] ===== Generate() terminado en {stopwatch.ElapsedMilliseconds} ms =====");

        return result;
    }

    /*
     * ============================================================
     * CREAR PADRES INICIALES
     * ============================================================
     *
     * PSEUDOCÓDIGO
     * ------------------------------------------------------------
     * 1: create empty parents
     * 2: repeat parentCount times:
     * 3:      create candidate with teamSize genes
     * 4:      assign random dataset index to every gene
     * 5:      add candidate to parents
     * 6: return parents
     */
    private List<PokemonTeamCandidate> CreateInitialParents(
        int parentCount,
        int datasetSize,
        PokemonTeamGenerationConfig config,
        System.Random random)
    {
        List<PokemonTeamCandidate> parents = new List<PokemonTeamCandidate>();
        for (int i = 0; i < parentCount; i++)
        {
            PokemonTeamCandidate candidate = new PokemonTeamCandidate(config.teamSize);

            for (int j = 0; j < candidate.genes.Length; j++)
            {
                int randomIndex = random.Next(0, datasetSize);
                candidate.genes[j] = randomIndex;
            }
            parents.Add(candidate);
        }
        return parents;
    }

    /*
     * ============================================================
     * MUTACIÓN
     * ============================================================
     *
     * PSEUDOCÓDIGO
     * ------------------------------------------------------------
     * 1: mutated = false
     * 2: for each gene:
     * 3:      if random < mutationRate:
     * 4:          replace gene with another random Pokemon
     * 5:          mutated = true
     *
     * 6: if no gene was mutated:
     * 7:      choose one random gene
     * 8:      replace it with another Pokemon
     */
    private void Mutate(
        PokemonTeamCandidate candidate,
        int datasetSize,
        System.Random random)
    {
        bool mutated = false;

        for (int i = 0; i < candidate.genes.Length; i++)
        {
            double randomMutation = random.NextDouble();
            if (randomMutation < mutationRate)
            {
                candidate.genes[i] = RandomDifferentPokemon(candidate.genes[i], datasetSize, random);
                mutated = true;
            }
        }

        if (!mutated)
        {
            int randomGene = random.Next(0, candidate.genes.Length);
            candidate.genes[randomGene] = RandomDifferentPokemon(candidate.genes[randomGene], datasetSize, random);
        }
    }

    /*
     * Retorna un índice diferente al actual,
     * entre 0 y datasetSize - 1.
     */
    private int RandomDifferentPokemon(
        int current,
        int datasetSize,
        System.Random random)
    {
        int randomIndex = random.Next(0, datasetSize);
        while (randomIndex == current)
        {
            randomIndex = random.Next(0, datasetSize);
        }
        return randomIndex;
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

    /*
     * ============================================================
     * MÉTODOS DE DEBUG
     * ============================================================
     */

    private float GetBestFitness(List<PokemonTeamCandidate> population)
    {
        float best = float.MinValue;
        foreach (PokemonTeamCandidate candidate in population)
        {
            if (candidate.fitness > best)
                best = candidate.fitness;
        }
        return best;
    }

    private float GetAverageFitness(List<PokemonTeamCandidate> population)
    {
        if (population.Count == 0)
            return 0f;

        float sum = 0f;
        foreach (PokemonTeamCandidate candidate in population)
        {
            sum += candidate.fitness;
        }
        return sum / population.Count;
    }

    private int CountChangedGenes(PokemonTeamCandidate before, PokemonTeamCandidate after)
    {
        int changed = 0;
        for (int i = 0; i < before.genes.Length; i++)
        {
            if (before.genes[i] != after.genes[i])
                changed++;
        }
        return changed;
    }

    private void LogPopulationStats(string label, List<PokemonTeamCandidate> population)
    {
        if (population == null || population.Count == 0)
        {
            Debug.LogWarning($"[ES] {label}: población vacía.");
            return;
        }

        float best = float.MinValue;
        float worst = float.MaxValue;
        foreach (PokemonTeamCandidate candidate in population)
        {
            if (candidate.fitness > best) best = candidate.fitness;
            if (candidate.fitness < worst) worst = candidate.fitness;
        }

        Debug.Log(
            $"[ES] {label} ({population.Count}) -> " +
            $"mejor: {best:0.000} | promedio: {GetAverageFitness(population):0.000} | peor: {worst:0.000}"
        );
    }

    private void LogBestTeam(
        string label,
        List<PokemonTeamCandidate> population,
        IReadOnlyList<PokemonData> dataset)
    {
        if (population == null || population.Count == 0)
            return;

        PokemonTeamCandidate best = population[0];
        foreach (PokemonTeamCandidate candidate in population)
        {
            if (candidate.fitness > best.fitness)
                best = candidate;
        }

        LogTeamDetails(label, best, dataset);
    }

    private void LogTeamDetails(
        string label,
        PokemonTeamCandidate team,
        IReadOnlyList<PokemonData> dataset)
    {
        List<string> names = new List<string>();
        foreach (int gene in team.genes)
        {
            if (gene >= 0 && gene < dataset.Count)
                names.Add(dataset[gene].name);
            else
                names.Add($"<índice inválido {gene}>");
        }

        Debug.Log(
            $"[ES] {label}:\n" +
            $"  Genes: [{string.Join(", ", team.genes)}]\n" +
            $"  Pokémon: {string.Join(", ", names)}\n" +
            $"  Fitness: {team.fitness:0.000} | Poder: {team.powerScore:0.000} | Diversidad: {team.diversityScore:0.000}\n" +
            $"  BST promedio: {team.averageBST:0.0} | Únicos: {team.uniquePokemon}/{team.genes.Length} | " +
            $"Tipos: {team.uniqueTypes} | Roles: {team.uniqueRoles}/3"
        );
    }
}