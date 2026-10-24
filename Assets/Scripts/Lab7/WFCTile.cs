using System;
using UnityEngine;

namespace Laboratorios.Lab7.WaveFunctionCollapse
{
    public enum WFCDirection { North, East, South, West }

    [Serializable]
    public class WFCTile
    {
        [Header("Datos del patron")]
        public string id;
        public Sprite sprite;
        [Min(0.01f)] public float weight = 1f;
        public bool available = true;

        [Header("Conexiones (true = abierto)")]
        public bool north, east, south, west;

        // Consulta si esta pieza tiene una abertura en una direccion.
        public bool HasOpening(WFCDirection direction)
        {
            switch (direction)
            {
                case WFCDirection.North: return north;
                case WFCDirection.East: return east;
                case WFCDirection.South: return south;
                case WFCDirection.West: return west;
                default: return false;
            }
        }

        /*
         * COMPATIBILIDAD ENTRE PIEZAS
         *
         * Dos piezas pueden ser vecinas cuando sus conexiones
         * enfrentadas son compatibles.
         *
         * Ejemplo:
         *
         * Pieza A: East = true
         * Pieza B: West = true
         *
         * Ambas pueden conectarse horizontalmente.
         *
         * Si A.East = true y B.West = false, la conexion
         * no es valida.
         *
         * Las conexiones cerradas tambien deben coincidir.
         * La direccion indica donde se ubica B respecto de A.
         */

        public bool IsCompatible(WFCTile neighbor, WFCDirection direction)
        {
            // Para saber si "this" (la pieza A) y "neighbor" (la pieza B) pueden
            // ser vecinas, hay que comparar la apertura de A hacia "direction"
            // contra la apertura de B hacia la direccion OPUESTA. Esto es porque,
            // desde el punto de vista de B, A queda ubicada en la direccion
            // contraria a la que usamos para ubicar a B respecto de A.
            //
            // Ejemplo: si direction = East (B esta al Este de A), entonces desde
            // B, A esta al Oeste. Por eso comparamos A.East contra B.West.
            WFCDirection opuesta;
            switch (direction)
            {
                case WFCDirection.North:
                    opuesta = WFCDirection.South;
                    break;
                case WFCDirection.East:
                    opuesta = WFCDirection.West;
                    break;
                case WFCDirection.South:
                    opuesta = WFCDirection.North;
                    break;
                case WFCDirection.West:
                   opuesta = WFCDirection.East;
                    break;
                default:
                    // Caso defensivo: con los 4 valores del enum nunca se llega
                    // aca, pero el compilador exige que "opuesta" quede asignada
                    // en TODOS los caminos posibles antes de usarla mas abajo.
                    opuesta = direction;
                    break;
            }

            // Dos piezas son compatibles solo si AMBAS aperturas enfrentadas
            // coinciden exactamente: abierto-abierto (se puede pasar de una a
            // otra) o cerrado-cerrado (las dos bloquean el paso por igual).
            // Si una esta abierta y la otra cerrada, no pueden ser vecinas.
            return this.HasOpening(direction) == neighbor.HasOpening(opuesta);
        }
    }
}
