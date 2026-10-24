# Verificacion del ejecutable final

Se ejecuto el archivo final `WFC_Lab7.exe` de Windows x64, version 1.0.0, en modo normal de Windows Player con la opcion de comprobacion automatica `-wfc-self-test`.

Resultado: **725 comprobaciones correctas, codigo de salida 0 y ninguna excepcion o error de Unity**. El informe detallado esta en [resultado.json](resultado.json). La compilacion Release se completo sin errores ni advertencias; su resumen esta en [compilacion.json](../compilacion.json).

Se comprobaron el arranque con una unica escena WFC, la ausencia de scripts de Sudoku/Pokemon en la aplicacion, clics detectados por la interfaz, observacion y propagacion separadas, detalle de celdas, reproduccion y pausa entre fotogramas, reinicio, limpieza, dimensiones validas e invalidas, reutilizacion de la grilla, cambio entre los dos tilesets y adyacencias de ambos resultados. Tambien se probo la interfaz a 960 x 600 y la generacion de una celda.

| Conjunto | Observacion | Propagacion | Resultado |
|---|---|---|---|
| Caminos | [Captura](caminos_observacion.png) | [Captura](caminos_propagacion.png) | [Captura](caminos_resultado.png) |
| Cueva | [Captura](cueva_observacion.png) | [Captura](cueva_propagacion.png) | [Captura](cueva_resultado.png) |

La [captura de contradiccion](contradiccion.png) corresponde a una configuracion artificial de prueba: un unico patron con salida superior que no puede ser vecino vertical de si mismo en una matriz 2 x 2. Se comprobo el dominio vacio, la indicacion roja, el estado de error, la detencion del avance y la posibilidad de reiniciar despues. Esta configuracion no modifica los tilesets de la escena entregada.
