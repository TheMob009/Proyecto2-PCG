using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PCG.Sudoku.Editor
{
    public static class SudokuInterfaceLayout
    {
        private static TMP_FontAsset readableFont;

        [MenuItem("Tools/PCG/Ajustar legibilidad de la interfaz")]
        public static void ApplyToScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Detén Play y guarda la escena antes de ajustar su legibilidad.");
            string original = SceneManager.GetActiveScene().path;
            foreach (string path in new[] { "Assets/Scenes/Proyecto2 V2.unity", "Assets/Scenes/Proyecto2.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Configure(Object.FindObjectOfType<SudokuUIController>(true));
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(original) ? "Assets/Scenes/Proyecto2 V2.unity" : original, OpenSceneMode.Single);
            Inspect();
            Debug.Log("Legibilidad ajustada: paneles en una columna, valores sin saltos y resumen ampliado.");
        }

        internal static void Configure(SudokuUIController controller)
        {
            readableFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (readableFont == null) throw new System.InvalidOperationException("Falta la fuente de la interfaz.");
            var fields = new SerializedObject(controller);
            var canvas = controller.GetComponentInParent<Canvas>();
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform background = (RectTransform)canvas.transform.Find("Background");
            var titlePanel = (RectTransform)background.Find("AlgLabelPanel");
            Box(titlePanel, background, 32, 14, 360, 38);
            var title = titlePanel.GetComponentInChildren<TMP_Text>(true);
            title.text = "Parámetros";
            title.fontSize = 72;
            title.enableAutoSizing = false;
            title.enableWordWrapping = false;
            Stretch(title.rectTransform, Vector2.zero, Vector2.one);

            var tabs = (RectTransform)background.Find("AlgButtonLayout");
            RemoveLayout(tabs);
            Box(tabs, background, 32, 60, 360, 40);
            ConfigureButton(Field<Button>(fields, "randomStartButton"), tabs, 0, 0, 116, 40, "Inicio");
            ConfigureButton(Field<Button>(fields, "geneticButton"), tabs, 122, 0, 116, 40, "Genético");
            ConfigureButton(Field<Button>(fields, "annealingButton"), tabs, 244, 0, 116, 40, "Pistas · SA");

            var parameters = (RectTransform)background.Find("ParametersBackground");
            RemoveLayout(parameters);
            Box(parameters, background, 32, 110, 360, 350);
            var genetic = Field<GameObject>(fields, "geneticPanel").GetComponent<RectTransform>();
            ConfigureRows(genetic, new[] { "GAPoblation", "GAGenerations", "GACrossover", "GAMutacion", "GATorneo", "GAElitismo" }, 54);
            var annealing = Field<GameObject>(fields, "annealingPanel").GetComponent<RectTransform>();
            ConfigureRows(annealing, new[] { "SA_TargetClues", "SA_TargetDifficulty", "SA_InitialTemperature", "SA_CooldownFactor",
                "SA_MinimumTemperature", "SA_MaxSteps", "SA_Restarts", "SA_ClueWeight", "SA_DifficultyWeight" }, 38);
            ConfigureStart(fields);

            var footer = (RectTransform)background.Find("GenerateLayout");
            RemoveLayout(footer);
            Box(footer, background, 32, 480, 360, 40);
            ConfigureButton(Field<Button>(fields, "generateButton"), footer, 0, 0, 174, 40, "Generar");
            ConfigureButton(Field<Button>(fields, "playButton"), footer, 186, 0, 174, 40, "Jugar");

            var board = Field<SudokuBoardView>(fields, "boardView");
            var boardRoot = board.GetComponent<RectTransform>();
            RemoveLayout(boardRoot);
            Box(boardRoot, background, 432, 20, 496, 500);
            var boardArea = (RectTransform)boardRoot.Find("BoardArea");
            Box(boardArea, boardRoot, 80, 16, 336, 336);
            foreach (TMP_Text cell in boardArea.GetComponentsInChildren<TMP_Text>(true))
            {
                Typography(cell, 56, new Color(0.12f, 0.15f, 0.2f), TextAlignmentOptions.Center);
                cell.fontStyle = FontStyles.Normal;
            }
            var results = Field<TMP_Text>(fields, "statusLabel").transform.parent.GetComponent<RectTransform>();
            RemoveLayout(results);
            Box(results, boardRoot, 0, 365, 496, 135);
            var status = Field<TMP_Text>(fields, "statusLabel");
            Box(status.rectTransform, results, 6, 0, 484, 88);
            Typography(status, 36, Color.white, TextAlignmentOptions.TopLeft);
            status.enableWordWrapping = true;
            status.text = "Genera una solución con GA y sus pistas con SA.\nAjusta cantidad y dificultad en el panel Pistas · SA.";
            ConfigureButton(Field<Button>(fields, "previousButton"), results, 90, 98, 145, 32, "Anterior");
            ConfigureButton(Field<Button>(fields, "nextButton"), results, 260, 98, 145, 32, "Siguiente");
            Canvas.ForceUpdateCanvases();
        }

        private static void ConfigureRows(RectTransform panel, string[] names, float height)
        {
            RemoveLayout(panel);
            Stretch(panel, Vector2.zero, Vector2.one);
            for (int i = 0; i < names.Length; i++)
            {
                var row = (RectTransform)panel.Find(names[i]);
                if (row == null) throw new System.InvalidOperationException("Falta el control " + names[i]);
                RemoveLayout(row);
                Box(row, panel, 0, i * height, 360, height - 3);
                TMP_Text quantity = row.Find("Cantidad").GetComponent<TMP_Text>();
                TMP_Text label = row.GetComponentsInChildren<TMP_Text>(true).First(text => text != quantity);
                Stretch(label.rectTransform, new Vector2(0.03f, 0.35f), new Vector2(0.72f, 1));
                Stretch(quantity.rectTransform, new Vector2(0.73f, 0.35f), new Vector2(0.97f, 1));
                Typography(label, 40, new Color(0.07f, 0.08f, 0.1f), TextAlignmentOptions.MidlineLeft);
                Typography(quantity, 40, new Color(0.07f, 0.08f, 0.1f), TextAlignmentOptions.MidlineRight);
                quantity.fontStyle = FontStyles.Bold;
                Slider slider = row.GetComponentInChildren<Slider>(true);
                Stretch(slider.GetComponent<RectTransform>(), new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.32f));
                if (slider.handleRect != null) slider.handleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 28);
                var track = slider.transform.Find("Background");
                if (track != null) track.GetComponent<Image>().color = new Color(0.35f, 0.4f, 0.46f);
                if (slider.fillRect != null) slider.fillRect.GetComponent<Image>().color = new Color(0.18f, 0.46f, 0.68f);
                if (slider.handleRect != null) slider.handleRect.GetComponent<Image>().color = new Color(0.85f, 0.93f, 1f);
            }
        }

        private static void ConfigureStart(SerializedObject fields)
        {
            var panel = Field<GameObject>(fields, "randomStartPanel").GetComponent<RectTransform>();
            RemoveLayout(panel);
            Stretch(panel, Vector2.zero, Vector2.one);
            int index = 0;
            foreach (RectTransform row in panel)
            {
                if (!row.gameObject.activeSelf) continue;
                RemoveLayout(row);
                Box(row, panel, 0, index++ * 64, 360, 56);
                foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
                    Typography(text, 40, new Color(0.07f, 0.08f, 0.1f), TextAlignmentOptions.MidlineLeft);
            }
            var seed = Field<TMP_InputField>(fields, "seedInput");
            var seedRow = seed.transform.parent.GetComponent<RectTransform>();
            Stretch(seed.GetComponent<RectTransform>(), new Vector2(0.34f, 0.15f), new Vector2(0.97f, 0.85f));
            foreach (TMP_Text text in seedRow.GetComponentsInChildren<TMP_Text>(true))
                if (!text.transform.IsChildOf(seed.transform)) Stretch(text.rectTransform, new Vector2(0.03f, 0.15f), new Vector2(0.32f, 0.85f));
            var toggle = Field<Toggle>(fields, "fixedSeedToggle");
            RectTransform toggleRow = toggle.transform.parent.GetComponent<RectTransform>();
            Stretch(toggle.GetComponent<RectTransform>(), new Vector2(0.86f, 0.16f), new Vector2(0.97f, 0.84f));
            foreach (TMP_Text text in toggleRow.GetComponentsInChildren<TMP_Text>(true))
                Stretch(text.rectTransform, new Vector2(0.03f, 0.15f), new Vector2(0.83f, 0.85f));
            var checkbox = toggle.transform.Find("Background").GetComponent<RectTransform>();
            checkbox.anchorMin = checkbox.anchorMax = checkbox.pivot = new Vector2(0.5f, 0.5f);
            checkbox.anchoredPosition = Vector2.zero;
            checkbox.sizeDelta = new Vector2(44, 44);
            if (toggle.graphic != null)
            {
                Stretch(toggle.graphic.rectTransform, Vector2.zero, Vector2.one);
                toggle.graphic.rectTransform.offsetMin = new Vector2(6, 6);
                toggle.graphic.rectTransform.offsetMax = new Vector2(-6, -6);
                toggle.graphic.color = new Color(0.07f, 0.08f, 0.1f);
            }
        }

        private static void ConfigureButton(Button button, RectTransform parent, float x, float y, float width, float height, string label)
        {
            Box(button.GetComponent<RectTransform>(), parent, x, y, width, height);
            var text = button.GetComponentInChildren<TMP_Text>(true);
            text.text = label;
            Stretch(text.rectTransform, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f));
            Typography(text, 40, new Color(0.07f, 0.08f, 0.1f), TextAlignmentOptions.Center);
        }

        private static T Field<T>(SerializedObject fields, string name) where T : Object =>
            (T)fields.FindProperty(name).objectReferenceValue;

        private static void Typography(TMP_Text text, float size, Color color, TextAlignmentOptions alignment)
        {
            text.font = readableFont;
            text.fontSharedMaterial = readableFont.material;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = alignment;
            text.margin = Vector4.zero;
            text.color = color;
            text.raycastTarget = false;
        }

        private static void RemoveLayout(RectTransform rect)
        {
            foreach (LayoutGroup layout in rect.GetComponents<LayoutGroup>()) Object.DestroyImmediate(layout);
            foreach (ContentSizeFitter fitter in rect.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(fitter);
        }

        private static void Box(RectTransform rect, RectTransform parent, float x, float y, float width, float height)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x * 2, -y * 2);
            rect.sizeDelta = new Vector2(width * 2, height * 2);
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        [MenuItem("Tools/PCG/Inspeccionar legibilidad")]
        public static void Inspect()
        {
            var controller = Object.FindObjectOfType<SudokuUIController>(true);
            var canvas = controller.GetComponentInParent<Canvas>();
            var output = new StringBuilder();
            var scaler = canvas.GetComponent<CanvasScaler>();
            Canvas.ForceUpdateCanvases();
            output.AppendLine($"Canvas: {canvas.name}; reference={scaler.referenceResolution}; scale={canvas.scaleFactor}; root={canvas.GetComponent<RectTransform>().rect}");
            foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(rect, canvas.transform);
                if (path.Contains("SudokuBoard/") && !path.Contains("BoardArea")) continue;
                if (path.Contains("/Handle") || path.Contains("/Fill") || path.Contains("/Background")) continue;
                var text = rect.GetComponent<TMP_Text>();
                output.AppendLine($"{path} | rect={rect.rect.width:F1}x{rect.rect.height:F1}, anchor={rect.anchorMin}..{rect.anchorMax}, pos={rect.anchoredPosition}, size={rect.sizeDelta}");
                if (text != null)
                    output.AppendLine($"  TEXT {text.text.Replace("\n", "\\n")} | font={text.font.name} size={text.fontSize} auto={text.enableAutoSizing} range={text.fontSizeMin}..{text.fontSizeMax}, wrap={text.enableWordWrapping}, color={text.color}");
            }
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/interface-layout.txt", output.ToString());
            Debug.Log("Inspección de legibilidad guardada en .utmp/interface-layout.txt");
        }
    }
}
