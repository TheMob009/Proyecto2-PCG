using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCG.Sudoku.Editor
{
    /// <summary>Comprueba referencias, controles y transición del puzzle evaluado a la partida.</summary>
    public static class SudokuPuzzleChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int assertions;

        [MenuItem("Tools/PCG/Verificar generación de pistas")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Ejecuta las comprobaciones fuera de Play.");
            foreach (string path in new[] { "Assets/Scenes/Proyecto2 V2.unity", "Assets/Scenes/Proyecto2.unity" })
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var controller = UnityEngine.Object.FindObjectOfType<SudokuUIController>(true);
                Invoke(controller, "Awake");
                Invoke(controller, "OnEnable");
                try
                {
                    var settingsOwner = Field<SudokuSimulatedAnnealing>(controller, "simulatedAnnealing");
                    settingsOwner.Settings.Validate();
                    controller.SelectAnnealingParameters();
                    Check(Field<GameObject>(controller, "annealingPanel").activeSelf, "SA panel opens");
                    var clues = Field<Slider>(controller, "targetCluesSlider");
                    var difficulty = Field<Slider>(controller, "targetDifficultySlider");
                    Check(clues != null && difficulty != null, "target controls are assigned");
                    Check(Field<Button>(controller, "playButton") != null, "play button is connected");
                    difficulty.value = 3;
                    Check(Field<TMP_Text>(controller, "targetDifficultyLabel").text == "Difícil", "difficulty label responds");
                    Check(clues.value == 28 && Field<Slider>(controller, "saMaxStepsSlider").value == 30000,
                        "difficulty profile updates clue count and search budget without clamping");
                    clues.value = 30;
                    Check((bool)Invoke(controller, "ConfigureAnnealing"), "UI parameters validate");
                    Check(settingsOwner.Settings.TargetClues == 30 && settingsOwner.Settings.TargetDifficulty == SudokuDifficulty.Hard,
                        "UI objectives reach generator settings");
                    var solved = Board("534678912672195348198342567859761423426853791713924856961537284287419635345286179");
                    var clueRoutine = (IEnumerator)Invoke(controller, "GenerateCluesRoutine", solved, settingsOwner.Settings, 1);
                    while (clueRoutine.MoveNext()) { }
                    var result = Field<SudokuPuzzleResult>(controller, "puzzleResult");
                    Check(result.TargetsReached && controller.BoardCount == 2 && controller.CurrentBoardIndex == 1,
                        "Unity pipeline stores solution and puzzle with both targets reached");
                    var play = Field<SudokuPlayController>(controller, "playController");
                    Invoke(play, "Awake");
                    Field<Button>(controller, "playButton").onClick.Invoke();
                    Check(play.IsActive, "play button starts the generated puzzle");
                    var view = Field<SudokuBoardView>(controller, "boardView");
                    int[,] puzzle = result.Puzzle;
                    for (int i = 0; i < 81; i++)
                        Check(view.GetCell(i / 9, i % 9) == puzzle[i / 9, i % 9], "playing preserves every evaluated cell");
                    controller.PreviousBoard();
                    Check(!play.IsActive && controller.CurrentBoardIndex == 0, "navigation exits play");
                    controller.NextBoard();
                    Check(Field<Button>(controller, "playButton").interactable, "puzzle enables play again");
                    // La corrutina se recorre manualmente para verificar el flujo completo sin renderizar.
                    var routine = (IEnumerator)Invoke(controller, "GenerateRoutine", 12345, true);
                    while (routine.MoveNext()) { }
                    Check(controller.BoardCount == 2 && SudokuEvaluator.Evaluate(controller.GetBoardCopy(0)).IsValid,
                        "default GA produces a valid solution and hands it to SA");
                    Check(SudokuSolutionCounter.Count(controller.GetBoardCopy(1)) == SudokuSolutionStatus.Unique,
                        "full pipeline produces a unique puzzle");
                    Check(Field<Button>(controller, "generateButton").interactable, "generation restores controls");
                    Debug.Log("PASS scene integration: " + path + " | " + Field<TMP_Text>(controller, "statusLabel").text);
                }
                finally { Invoke(controller, "OnDisable"); }
            }
            // Descarta cambios de la comprobación; no modifica escenas guardadas.
            EditorSceneManager.OpenScene("Assets/Scenes/Proyecto2 V2.unity", OpenSceneMode.Single);
            Debug.Log("PASS Sudoku scene checks: " + assertions + " assertions.");
        }

        private static T Field<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
        private static object Invoke(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, Private).Invoke(target, arguments);

        private static int[,] Board(string text)
        {
            var board = new int[9, 9];
            for (int i = 0; i < 81; i++) board[i / 9, i % 9] = text[i] - '0';
            return board;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + message);
            assertions++;
        }
    }
}
