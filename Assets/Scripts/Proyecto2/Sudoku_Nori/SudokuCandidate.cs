using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SudokuCandidate
{

    public int[,] board;
    private int x = 9;
    private int y = 9;
    public int cost = 0;//total de conflictos

    public SudokuCandidate(int[,] initialBoard)
    {
        board = (int[,])initialBoard.Clone();
        cost = 0;
    }

    public SudokuCandidate()
    {
        board = new int[9, 9];
        cost = 0;
    }

    public SudokuCandidate Clone()
    {
        var copia = new SudokuCandidate(board);
        copia.cost = cost;
        return copia;

    }

}
