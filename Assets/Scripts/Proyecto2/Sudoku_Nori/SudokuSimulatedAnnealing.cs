using PCG.Sudoku;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.PlasticSCM.Editor.WebApi;
using Unity.VisualScripting;
using UnityEngine;

public class SudokuSimulatedAnnealing : MonoBehaviour
{
    [SerializeField] private float initialTemperature = 10f;
    [SerializeField] private float coolingRate = 0.99f;
    [SerializeField] private float minTemperature = 0.001f;//debe ser un valor cercano a 0
    //no es 0 para evitar la división por 0
    [SerializeField] private int maxSteps; //para evitar un bucle infinito 


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }

    private SudokuCandidate SimulatedAnnealing(SudokuCandidate candidateInicial, int seed)
    {
        System.Random random = new System.Random(seed);
        var current = candidateInicial.Clone();
        var best = current.Clone();
        float temperature = initialTemperature;

        //Se calcula el número de conflictos para evitar que el default sea 0 y el algoritmo piense que no tiene conflictos
        current.cost = SudokuEvaluator.Evaluate(current.board).TotalConflicts;
        //el mejo tablero ya que current puede tener tableros peores
        best.cost = current.cost;
        while (temperature > minTemperature)
        {
            //el sudoko no tiene conflictos
            if (current.cost == 0)
            {
                break;
            }

           SudokuCandidate next = GetSucessor(current, random);

            //hay que minimizar los conflictos asi que menos es mejor
            double deltaE = current.cost - next.cost;
            if (deltaE > 0)
            {
                //si el vecino tiene menos conflicto se guarda
                current = next;
            }
            else
            {
                //para evitar quedar atrapado en un minimo local
                //acá delta es un número negativo muy chico por ejemplo -2/20 = -0.1
                //a más alta la temperatura y más chico el valor mayor es la exploración del espacio de búsqueda
                //el algoritmo tiene más probabilidad de aceptar empeorar si la temperatura es muy alta
                //si la temperatura es muy baja el algoritmo solo acepta mejoras.
                double rate = System.Math.Exp(deltaE / temperature);
                if(random.NextDouble() < rate)
                {
                    current = next;
                }
            }

            //se guarda el mejor 
            if (current.cost < best.cost)
            {
                best = current.Clone();
            }
            //se multiplica la temperatura por el coolingrate para ""enfiarla
            temperature *= coolingRate;


        }

        return best;

    }

    private SudokuCandidate GetSucessor(SudokuCandidate current, System.Random random)
    {
        current = current.Clone();
        //se elije una fila al azar
        var row = random.Next(0, 9);
        //se elijen dos columnas al azar
        var columA = random.Next(0, 9);
        var columB = random.Next(0, 9);

        //Mientras sean iguales se vuelve a tirar la columna B
        while (columA == columB)
        {
            columB = random.Next(0, 9);
        }

        //se intercambia los valores 
        int aux = current.board[row, columA];
        current.board[row, columA] = current.board[row, columB];
        current.board[row, columB] = aux;

        //Se calculan los conflictos del nuevo tablero
        current.cost = SudokuEvaluator.Evaluate(current.board).TotalConflicts;

        return current;
    }
}
