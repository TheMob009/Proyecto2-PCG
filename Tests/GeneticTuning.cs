using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using PCG.Sudoku;

internal static class GeneticTuning
{
    internal static void CheckDefaults()
    {
        foreach (int seed in new[] { 12345, 42, -1 })
        {
            var candidate = new SudokuGeneticA().Generate(seed);
            if (!SudokuEvaluator.Evaluate(candidate.board).IsValid)
                throw new Exception("GA default parameters left conflicts for seed " + seed);
        }
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        foreach (string name in new[] { "Proyecto2 V2.unity", "Proyecto2.unity" })
        {
            string scene = File.ReadAllText(Path.Combine(root, "Assets/Scenes", name));
            CheckControl(scene, "gaPopulation", "populationSize", SudokuGeneticA.DefaultPopulationSize);
            CheckControl(scene, "gaGenerations", "generations", SudokuGeneticA.DefaultGenerations);
            CheckControl(scene, "gaCrossover", "crossoverRate", SudokuGeneticA.DefaultCrossoverRate);
            CheckControl(scene, "gaMutation", "mutationRate", SudokuGeneticA.DefaultMutationRate);
            CheckControl(scene, "gaTournament", "tournamentSize", SudokuGeneticA.DefaultTournamentSize);
            CheckControl(scene, "gaElite", "eliteCount", SudokuGeneticA.DefaultEliteCount);
        }
        Console.WriteLine("PASS GA defaults: three regression seeds and component/slider/label consistency in both scenes.");
    }

    private static void CheckControl(string scene, string field, string parameter, double expected)
    {
        string sliderId = Regex.Match(scene, field + @"Slider: \{fileID: (\d+)\}").Groups[1].Value;
        string labelId = Regex.Match(scene, field + @"Label: \{fileID: (\d+)\}").Groups[1].Value;
        string slider = Regex.Match(scene, @"(?ms)^--- !u!114 &" + sliderId + @"\r?\n.*?(?=^--- |\z)").Value;
        string label = Regex.Match(scene, @"(?ms)^--- !u!114 &" + labelId + @"\r?\n.*?(?=^--- |\z)").Value;
        double value = Number(slider, "m_Value"), minimum = Number(slider, "m_MinValue"), maximum = Number(slider, "m_MaxValue");
        if (Math.Abs(value - expected) > 0.00001 || expected < minimum || expected > maximum ||
            Math.Abs(Number(label, "m_text") - expected) > 0.00001 || Math.Abs(Number(scene, parameter) - expected) > 0.00001)
            throw new Exception("Serialized GA setting is inconsistent: " + field);
    }

    private static double Number(string text, string field)
    {
        string value = Regex.Match(text, @"(?m)^  " + field + @": ([^\r\n]+)").Groups[1].Value;
        return double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static void Run(string[] arguments)
    {
        int count = arguments.Length > 1 ? int.Parse(arguments[1]) : 3;
        int[] referenceSeeds = { 12345, 1, 2, 3, 4, 5, 42, 2026, -1, 987654321 };
        string filter = arguments.Length > 2 ? arguments[2] : "";
        var configurations = new[]
        {
            (Name: "baseline", Population: 100, Generations: 150, Cross: 0.8f, Mutation: 0.2f, Tournament: 5, Elites: 5),
            (Name: "larger-budget", Population: 200, Generations: 5000, Cross: 0.8f, Mutation: 0.2f, Tournament: 5, Elites: 5),
            (Name: "gentle", Population: 200, Generations: 5000, Cross: 0.3f, Mutation: 0.05f, Tournament: 3, Elites: 2),
            (Name: "balanced", Population: 200, Generations: 5000, Cross: 0.6f, Mutation: 0.1f, Tournament: 3, Elites: 2),
            (Name: "diverse", Population: 200, Generations: 5000, Cross: 0.8f, Mutation: 0.3f, Tournament: 2, Elites: 2),
            (Name: "selective", Population: 200, Generations: 5000, Cross: 0.3f, Mutation: 0.15f, Tournament: 5, Elites: 5),
            (Name: "defaults", Population: SudokuGeneticA.DefaultPopulationSize, Generations: SudokuGeneticA.DefaultGenerations,
                Cross: SudokuGeneticA.DefaultCrossoverRate, Mutation: SudokuGeneticA.DefaultMutationRate,
                Tournament: SudokuGeneticA.DefaultTournamentSize, Elites: SudokuGeneticA.DefaultEliteCount)
        };
        foreach (var configuration in configurations)
        {
            if (filter.Length > 0 && Array.IndexOf(filter.Split(','), configuration.Name) < 0) continue;
            var watch = Stopwatch.StartNew();
            int solved = 0, conflicts = 0;
            for (int i = 0; i < count; i++)
            {
                int seed = i < referenceSeeds.Length ? referenceSeeds[i] : unchecked(123456789 + i * 7919);
                var algorithm = new SudokuGeneticA();
                algorithm.SetParameters(configuration.Population, configuration.Generations, configuration.Cross,
                    configuration.Mutation, configuration.Tournament, configuration.Elites);
                var result = algorithm.Generate(seed);
                int actual = SudokuEvaluator.Evaluate(result.board).TotalConflicts;
                if (actual == 0) solved++;
                conflicts += actual;
                if (actual != 0 || i < 3 || (i + 1) % 10 == 0)
                    Console.WriteLine($"{configuration.Name}: case={i + 1}, seed={seed}, conflicts={actual}, elapsed={watch.ElapsedMilliseconds}ms");
            }
            Console.WriteLine($"RESULT {configuration.Name}: {solved}/{count}, sumConflicts={conflicts}, elapsed={watch.ElapsedMilliseconds}ms");
        }
    }
}
