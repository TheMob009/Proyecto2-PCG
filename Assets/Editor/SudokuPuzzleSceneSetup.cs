using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCG.Sudoku.Editor
{
    /// <summary>Configura los controles de generación de pistas en las escenas del proyecto.</summary>
    public static class SudokuPuzzleSceneSetup
    {
        [MenuItem("Tools/PCG/Configurar controles de pistas")]
        public static void ConfigureScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Detén Play antes de configurar las escenas.");
            foreach (string path in new[] { "Assets/Scenes/Proyecto2 V2.unity", "Assets/Scenes/Proyecto2.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var controller = UnityEngine.Object.FindObjectOfType<SudokuUIController>(true);
                if (controller == null) throw new InvalidOperationException("Falta SudokuUIController en " + path);
                var serialized = new SerializedObject(controller);
                ConfigureGeneticControls(serialized);
                var panel = (GameObject)serialized.FindProperty("annealingPanel").objectReferenceValue;
                var template = panel.transform.Find("SA_MaxSteps").gameObject;
                foreach (LayoutGroup group in panel.GetComponents<LayoutGroup>()) UnityEngine.Object.DestroyImmediate(group);
                foreach (ContentSizeFitter fitter in panel.GetComponents<ContentSizeFitter>()) UnityEngine.Object.DestroyImmediate(fitter);
                var panelRect = (RectTransform)panel.transform;
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = new Vector2(12, 12);
                panelRect.offsetMax = new Vector2(-12, -12);

                ConfigureControl(serialized, panel, template, 0, "SA_TargetClues", "Pistas objetivo", "targetClues", 17, 80, 41, true);
                ConfigureControl(serialized, panel, template, 1, "SA_TargetDifficulty", "Perfil de dificultad", "targetDifficulty", 1, 3, 1, true);
                ConfigureControl(serialized, panel, template, 2, "SA_InitialTemperature", "Temperatura inicial", "saInitialTemperature", 0.001f, 0.2f, 0.05f, false);
                ConfigureControl(serialized, panel, template, 3, "SA_CooldownFactor", "Enfriamiento", "saCooling", 0.9f, 0.9999f, 0.999f, false);
                ConfigureControl(serialized, panel, template, 4, "SA_MinimumTemperature", "Temperatura mínima", "saMinimumTemperature", 0.00001f, 0.001f, 0.0001f, false);
                ConfigureControl(serialized, panel, template, 5, "SA_MaxSteps", "Intentos totales", "saMaxSteps", 100, 30000, 5000, true);
                ConfigureControl(serialized, panel, template, 6, "SA_Restarts", "Reinicios máximos", "saRestarts", 0, 10, 2, true);
                ConfigureControl(serialized, panel, template, 7, "SA_ClueWeight", "Peso de pistas", "clueWeight", 0.1f, 5, 1, false);
                ConfigureControl(serialized, panel, template, 8, "SA_DifficultyWeight", "Peso de dificultad", "difficultyWeight", 0.1f, 5, 1, false);

                var buttons = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Button>(true));
                Button play = buttons.FirstOrDefault(button => button.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text.Trim() == "Jugar"));
                if (play == null) throw new InvalidOperationException("Falta el botón Jugar en " + path);
                serialized.FindProperty("playButton").objectReferenceValue = play;
                var playController = controller.GetComponent<SudokuPlayController>();
                if (playController == null) playController = controller.gameObject.AddComponent<SudokuPlayController>();
                serialized.FindProperty("playController").objectReferenceValue = playController;
                var playSerialized = new SerializedObject(playController);
                playSerialized.FindProperty("boardView").objectReferenceValue = serialized.FindProperty("boardView").objectReferenceValue;
                playSerialized.ApplyModifiedPropertiesWithoutUndo();
                var sa = (SudokuSimulatedAnnealing)serialized.FindProperty("simulatedAnnealing").objectReferenceValue;
                sa.SetParameters(new SudokuPuzzleSettings());
                var status = (TMP_Text)serialized.FindProperty("statusLabel").objectReferenceValue;
                status.text = "Generar: solución con GA y pistas con SA. Ajusta cantidad y dificultad.";
                status.enableAutoSizing = true;
                status.fontSizeMin = 12;
                status.fontSizeMax = 23;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                SudokuInterfaceLayout.Configure(controller);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/Proyecto2 V2.unity", OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("Controles de cantidad, dificultad y exploración de pistas configurados en ambas escenas.");
        }

        [MenuItem("Tools/PCG/Restablecer parámetros predeterminados de GA")]
        public static void ConfigureGeneticScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Detén Play antes de configurar las escenas.");
            foreach (string path in new[] { "Assets/Scenes/Proyecto2 V2.unity", "Assets/Scenes/Proyecto2.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var controller = UnityEngine.Object.FindObjectOfType<SudokuUIController>(true);
                ConfigureGeneticControls(new SerializedObject(controller));
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/Proyecto2 V2.unity", OpenSceneMode.Single);
            Debug.Log("Parámetros predeterminados de GA actualizados en componentes y controles de ambas escenas.");
        }

        private static void ConfigureGeneticControls(SerializedObject controller)
        {
            var algorithm = (SudokuGeneticA)controller.FindProperty("geneticAlgorithm").objectReferenceValue;
            algorithm.SetParameters(SudokuGeneticA.DefaultPopulationSize, SudokuGeneticA.DefaultGenerations,
                SudokuGeneticA.DefaultCrossoverRate, SudokuGeneticA.DefaultMutationRate,
                SudokuGeneticA.DefaultTournamentSize, SudokuGeneticA.DefaultEliteCount);
            ConfigureGeneticSlider(controller, "gaPopulation", 20, 200, SudokuGeneticA.DefaultPopulationSize, true);
            ConfigureGeneticSlider(controller, "gaGenerations", 20, 10000, SudokuGeneticA.DefaultGenerations, true);
            ConfigureGeneticSlider(controller, "gaCrossover", 0, 1, SudokuGeneticA.DefaultCrossoverRate, false);
            ConfigureGeneticSlider(controller, "gaMutation", 0.01f, 0.3f, SudokuGeneticA.DefaultMutationRate, false);
            ConfigureGeneticSlider(controller, "gaTournament", 2, 10, SudokuGeneticA.DefaultTournamentSize, true);
            ConfigureGeneticSlider(controller, "gaElite", 0, 10, SudokuGeneticA.DefaultEliteCount, true);
        }

        private static void ConfigureGeneticSlider(SerializedObject controller, string field,
            float minimum, float maximum, float value, bool integer)
        {
            var slider = (Slider)controller.FindProperty(field + "Slider").objectReferenceValue;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = integer;
            slider.SetValueWithoutNotify(value);
            var label = (TMP_Text)controller.FindProperty(field + "Label").objectReferenceValue;
            label.text = value.ToString(integer ? "F0" : "F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void ConfigureControl(SerializedObject controller, GameObject panel, GameObject template,
            int order, string name, string title, string field, float minimum, float maximum, float value, bool integer)
        {
            Transform existing = panel.transform.Find(name);
            GameObject row = existing != null ? existing.gameObject : UnityEngine.Object.Instantiate(template, panel.transform);
            row.name = name;
            var rect = (RectTransform)row.transform;
            int column = order % 2, line = order / 2;
            rect.anchorMin = new Vector2(column * 0.5f + 0.01f, 1 - (line + 1) * 0.2f + 0.01f);
            rect.anchorMax = new Vector2((column + 1) * 0.5f - 0.01f, 1 - line * 0.2f - 0.01f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            foreach (LayoutGroup group in row.GetComponents<LayoutGroup>()) UnityEngine.Object.DestroyImmediate(group);
            foreach (ContentSizeFitter fitter in row.GetComponents<ContentSizeFitter>()) UnityEngine.Object.DestroyImmediate(fitter);
            Slider slider = row.GetComponentInChildren<Slider>(true);
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = integer;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged = new Slider.SliderEvent();
            Stretch(slider.GetComponent<RectTransform>(), new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.42f));
            TMP_Text quantity = row.transform.Find("Cantidad").GetComponent<TMP_Text>();
            TMP_Text label = row.GetComponentsInChildren<TMP_Text>(true).First(text => text != quantity);
            label.text = title;
            quantity.text = field == "targetDifficulty" ? "Fácil" : value.ToString(integer ? "F0" : field == "saInitialTemperature" ? "F3" : "F4", System.Globalization.CultureInfo.InvariantCulture);
            foreach (TMP_Text text in new[] { label, quantity })
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = 12;
                text.fontSizeMax = 26;
                text.enableWordWrapping = false;
                text.raycastTarget = false;
            }
            Stretch(label.rectTransform, new Vector2(0.03f, 0.49f), new Vector2(0.72f, 0.98f));
            Stretch(quantity.rectTransform, new Vector2(0.72f, 0.49f), new Vector2(0.98f, 0.98f));
            controller.FindProperty(field + "Slider").objectReferenceValue = slider;
            controller.FindProperty(field + "Label").objectReferenceValue = quantity;
        }

        private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
