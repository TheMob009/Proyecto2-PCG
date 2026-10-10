using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Un individuo representa un equipo; cada gen guarda el índice de un Pokémon en el dataset.
// El algoritmo evoluciona una población mediante selección, cruce, mutación y elitismo.
public class GeneticAlgorithm : MonoBehaviour
{
    [Header("Genetic Algorithm")]

    // Cantidad de equipos que compiten en cada generación.
    // El tamaño de cada equipo se define por separado en config.teamSize.
    [Min(2)]
    [SerializeField] private int populationSize = 50;

    // Cantidad de generaciones que se construyen después de crear la población inicial.
    // Cada generación conserva élites y completa los espacios restantes con hijos.
    [Min(1)]
    [SerializeField] private int generations = 80;

    // Probabilidad de cruzar dos padres para producir un hijo.
    // Cuando no se realiza el cruce, el hijo es una copia del primer padre.
    [Range(0f, 1f)]
    [SerializeField] private float crossoverRate = 0.80f;

    // Probabilidad independiente de reemplazar cada gen del hijo.
    // Puede cambiar ninguno, uno o varios integrantes en una misma mutación.
    [Range(0f, 1f)]
    [SerializeField] private float mutationRate = 0.12f;

    // Cantidad de participantes sorteados en cada torneo para elegir un padre.
    // Aumentarla favorece la selección de los mejores y tiende a reducir la variedad.
    [Min(2)]
    [SerializeField] private int tournamentSize = 3;

    // Número de mejores individuos que pasan intactos a la siguiente generación.
    // Si abarca toda la población, no quedan espacios para producir hijos.
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

    // Crear y evolucionar una población de equipos según los parámetros configurados.
    // Al finalizar, seleccionar un equipo considerando el umbral de fitness de salida.
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

        // Compartir una fuente aleatoria entre todas las etapas de esta ejecución.
        // NextDouble devuelve valores en [0, 1) para decidir cruces y mutaciones.
        System.Random random = new System.Random();

        // Crear la población inicial asignando a cada equipo integrantes elegidos al azar.
        List<PokemonTeamCandidate> population =
            CreateInitialPopulation(dataset.Count, config, random);

        // Calcular el fitness de todos los individuos antes de compararlos y seleccionar padres.
        EvaluatePopulation(population, dataset, config);

        // Ordenar de mayor a menor fitness para situar al mejor en population[0].
        // Las primeras posiciones serán las que se conserven mediante elitismo.
        population.Sort((a, b) => b.fitness.CompareTo(a.fitness));

        // Limitar la cantidad de élites al tamaño de la población.
        // Los lugares restantes se completarán con hijos.
        int eliteCount = Mathf.Clamp(elitism, 0, population.Count);

        // Construir una nueva población en cada iteración hasta completar las generaciones configuradas.
        // Superar el umbral de fitness de salida no detiene este ciclo.
        for (int generation = 0; generation < generations; generation++)
        {
            // Crear una lista vacía con capacidad para la siguiente generación.
            // La población actual sigue siendo la fuente de padres hasta completar la nueva.
            List<PokemonTeamCandidate> nextPopulation =
                new List<PokemonTeamCandidate>(population.Count);

            // Copiar los mejores individuos, incluidos sus genes y métricas, sin cruzarlos ni mutarlos.
            // Cada clon tiene su propio arreglo de genes y conserva el fitness bajo la misma configuración.
            for (int i = 0; i < eliteCount; i++)
                nextPopulation.Add(population[i].Clone());

            // Producir hijos hasta completar la población, contando las élites ya incorporadas.
            // Así se mantiene el mismo número de individuos entre generaciones.
            while (nextPopulation.Count < population.Count)
            {
                // Elegir el primer padre mediante un torneo entre individuos de la población actual.
                PokemonTeamCandidate parentA = TournamentSelection(population, random);

                // Elegir el segundo padre con otro torneo independiente.
                // Ambos torneos pueden seleccionar al mismo individuo.
                PokemonTeamCandidate parentB = TournamentSelection(population, random);

                // Cruzar los padres si el sorteo es menor que crossoverRate; en caso contrario, clonar parentA.
                // Ambas alternativas crean un hijo con genes independientes de los padres.
                PokemonTeamCandidate child = random.NextDouble() < crossoverRate
                    ? Crossover(parentA, parentB, random)
                    : parentA.Clone();

                // Aplicar la posibilidad de mutación a cada gen, incluso si el hijo se obtuvo por clonación.
                Mutate(child, dataset.Count, random);

                // Recalcular el fitness después de mutar para evaluar los genes definitivos del hijo.
                // Esto también reemplaza cualquier puntuación heredada mediante clonación.
                PokemonTeamFitness.Evaluate(child, dataset, config);

                // Añadir el hijo evaluado aunque su fitness sea inferior al de sus padres.
                // La selección de generaciones posteriores favorecerá a los individuos de mayor calidad.
                nextPopulation.Add(child);
            }

            // Reemplazar la población actual por las élites conservadas y los nuevos hijos.
            // Los individuos anteriores que no se conservaron dejan de competir directamente.
            population = nextPopulation;

            // Ordenar la nueva población para preparar el elitismo y las comparaciones posteriores.
            // Si se conserva al menos una élite, el mejor fitness no disminuye entre generaciones.
            population.Sort((a, b) => b.fitness.CompareTo(a.fitness));

            // Mostrar el mejor fitness de la generación terminada; este registro no influye en la evolución.
            if (logProgress)
            {
                Debug.Log(
                    $"[GeneticAlgorithm] Generation {generation + 1}/{generations}: " +
                    $"best fitness = {population[0].fitness:0.000}"
                );
            }
        }

        // Devolver una copia elegida al azar entre quienes alcanzan el umbral, sin garantizar elegir al mejor.
        // Si nadie lo alcanza, sortear entre los mejores fallbackTopCandidates disponibles.
        return PokemonTeamFitness.SelectGeneratedContent(population, config, random);
    }

    // Crear individuos aleatorios sin exigir previamente un BST o una diversidad determinados.
    // Estas condiciones influirán después en su fitness y en la selección de padres.
    private List<PokemonTeamCandidate> CreateInitialPopulation(
        int datasetSize,
        PokemonTeamGenerationConfig config,
        System.Random random)
    {
        // Determinar cuántos individuos se crearán, con un mínimo de dos.
        int count = Mathf.Max(2, populationSize);

        // Reservar capacidad para los individuos; la lista permanece vacía hasta añadirlos.
        List<PokemonTeamCandidate> population = new List<PokemonTeamCandidate>(count);

        // Construir un individuo completo en cada iteración hasta alcanzar el tamaño de población.
        for (int i = 0; i < count; i++)
        {
            // Crear un arreglo con config.teamSize genes.
            // Cada posición representa un integrante del equipo mediante su índice en el dataset.
            PokemonTeamCandidate candidate = new PokemonTeamCandidate(config.teamSize);

            // Asignar a cada gen un índice aleatorio en [0, datasetSize), permitiendo repeticiones.
            // Los duplicados afectan la puntuación de diversidad cuando se evalúa el equipo.
            for (int gene = 0; gene < candidate.genes.Length; gene++)
                candidate.genes[gene] = random.Next(datasetSize);

            // Añadir el individuo completo a la población; su fitness se calculará posteriormente.
            population.Add(candidate);
        }

        // Entregar la población inicial a Generate para evaluarla y ordenarla antes de evolucionar.
        return population;
    }

    // Seleccionar un padre comparando una muestra aleatoria de la población.
    // El torneo favorece a individuos de mayor fitness sin elegir siempre al mejor global.
    private PokemonTeamCandidate TournamentSelection(
        List<PokemonTeamCandidate> population,
        System.Random random)
    {
        // Iniciar sin ganador; el primer participante sorteado será el mejor provisional.
        PokemonTeamCandidate best = null;

        // Realizar tournamentSize extracciones, con un mínimo de dos participantes sorteados.
        for (int i = 0; i < Mathf.Max(2, tournamentSize); i++)
        {
            // Sortear un individuo de la población completa sin retirarlo.
            // El mismo individuo puede participar varias veces en un torneo.
            PokemonTeamCandidate candidate = population[random.Next(population.Count)];

            // Actualizar el ganador si aún no existe o si el participante tiene mayor fitness.
            // En caso de empate, conservar al ganador provisional anterior.
            if (best == null || candidate.fitness > best.fitness)
                best = candidate;
        }

        // Devolver la referencia al ganador sin modificarlo.
        // La copia independiente del hijo se creará después mediante Crossover o Clone.
        return best;
    }

    // Combinar un segmento inicial del primer padre con el segmento final del segundo.
    // El cruce conserva la longitud del equipo y el orden de los genes heredados.
    private PokemonTeamCandidate Crossover(
        PokemonTeamCandidate parentA,
        PokemonTeamCandidate parentB,
        System.Random random)
    {
        // Obtener la cantidad de genes; ambos padres tienen la longitud definida por la misma configuración.
        int geneCount = parentA.genes.Length;

        // Un equipo de un solo integrante no tiene un punto de corte interno.
        if (geneCount < 2)
            return parentA.Clone();

        // Crear un hijo con su propio arreglo para que sus cambios no afecten a los padres.
        PokemonTeamCandidate child = new PokemonTeamCandidate(geneCount);

        // Sortear un corte entre 1 y geneCount - 1.
        // Así cada padre aporta al menos una posición al hijo.
        int crossoverPoint = random.Next(1, geneCount);

        // Recorrer todas las posiciones del hijo para determinar de qué padre se copia cada gen.
        for (int i = 0; i < geneCount; i++)
        {
            // Copiar de parentA antes del corte y de parentB desde el corte en adelante.
            // Se heredan índices de Pokémon completos, sin mezclar sus estadísticas.
            child.genes[i] = i < crossoverPoint
                ? parentA.genes[i]
                : parentB.genes[i];
        }

        // Entregar el hijo para mutarlo y evaluarlo posteriormente.
        // El cruce puede generar duplicados o reducir el fitness; no garantiza una mejora.
        return child;
    }

    // Introducir cambios aleatorios para explorar integrantes que el cruce podría no incorporar.
    private void Mutate(
        PokemonTeamCandidate candidate,
        int datasetSize,
        System.Random random)
    {
        if (datasetSize < 2)
            return;

        // Examinar cada gen de forma independiente, sin obligar a que alguno mute.
        for (int i = 0; i < candidate.genes.Length; i++)
        {
            // Reemplazar el gen únicamente si el sorteo es menor que mutationRate.
            // En caso contrario, conservar su valor actual.
            if (random.NextDouble() < mutationRate)
            {
                // Sortear entre datasetSize - 1 posibilidades para preparar la exclusión del índice actual.
                // El ajuste siguiente transforma este resultado en uno de los otros índices válidos.
                int replacement = random.Next(datasetSize - 1);

                // Desplazar los resultados desde el índice actual para saltarlo y evitar elegirlo otra vez.
                // Cada índice alternativo mantiene la misma probabilidad, sin repetir sorteos.
                if (replacement >= candidate.genes[i])
                    replacement++;

                // Guardar el nuevo índice, que puede coincidir con otro integrante del equipo.
                // El cambio puede mejorar o empeorar el fitness; Generate lo recalcula después.
                candidate.genes[i] = replacement;
            }
        }
    }

    // Evaluar la población inicial con las mismas reglas que se aplican a cada hijo.
    // El fitness combina 40% de poder y 60% de diversidad.
    private void EvaluatePopulation(
        List<PokemonTeamCandidate> population,
        IReadOnlyList<PokemonData> dataset,
        PokemonTeamGenerationConfig config)
    {
        // Evaluar todos los individuos para que cada uno pueda participar en las comparaciones.
        foreach (PokemonTeamCandidate candidate in population)
        {
            // Guardar fitness, poder y diversidad según el BST promedio, los Pokémon, los tipos y los roles distintos.
            // Las condiciones afectan la puntuación; estar fuera del intervalo BST no elimina al individuo.
            PokemonTeamFitness.Evaluate(candidate, dataset, config);
        }
    }
}
