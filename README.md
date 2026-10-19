# Generación procedural de Sudoku

Proyecto de la Unidad II de Generación Procedural de Contenido, desarrollado en **Unity 2022.3.62f3**. Genera tableros de Sudoku mediante **Random Start State**, los optimiza con un **algoritmo genético (GA)** y aplica **Simulated Annealing (SA)** al mejor candidato. Desde una solución válida también permite crear una partida y resolverla manualmente.

## Integrantes

| Integrante | GitHub |
|---|---|
| **Matías Ortega** | [TheMob009](https://github.com/TheMob009) |
| **Noriyuki Kato** | [NoriKato16](https://github.com/NoriKato16) |

## Enlaces

- [Repositorio del proyecto](https://github.com/TheMob009/Proyecto2-PCG).
- [Sudoku en la biblioteca de Ludii](https://ludii.games/details.php?keyword=Sudoku), referencia del puzzle seleccionado.
- [Jugar en itch.io: PCG Sudoku Map Generator](https://themob-00.itch.io/pcg-sudoku-map-generator).

## Cómo ejecutar

**En el navegador:** abre la [versión web en itch.io](https://themob-00.itch.io/pcg-sudoku-map-generator) y utiliza teclado y ratón para jugar.

**En el editor de Unity:**

1. Descarga o clona el repositorio y ábrelo con **Unity 2022.3.62f3**.
2. Abre **`Assets/Scenes/Proyecto2 V2.unity`** y presiona **Play**.
3. Ajusta los parámetros y pulsa **Generar** dentro del juego.

Las pestañas de técnicas muestran sus parámetros: **Generar siempre ejecuta el flujo completo Random + GA + SA**. Puedes comparar los resultados de GA y SA con **Anterior** y **Siguiente**.

## Puzzle y representación

Sudoku utiliza una cuadrícula de **9 × 9**, dividida en nueve bloques de **3 × 3**. Una solución debe contener los números del **1 al 9**, sin repeticiones en ninguna fila, columna o bloque.

El proyecto representa cada tablero mediante una matriz **`int[9,9]`**:

- Durante la generación y la búsqueda, todas las posiciones contienen valores del 1 al 9.
- Cada fila es una permutación de esos números. Esta propiedad se conserva al cruzar o modificar candidatos.
- Un `SudokuCandidate` contiene el tablero y su costo de conflictos.
- En una partida, **0** representa una casilla vacía y una matriz `bool[9,9]` identifica las pistas que el jugador no puede modificar.

Esta representación permite acceder directamente a filas, columnas y bloques, copiar candidatos y definir operadores que mantengan las filas válidas. Así, la búsqueda se concentra en resolver los conflictos restantes de columnas y bloques.

Su limitación es que conservar filas válidas no garantiza que el tablero completo sea una solución. Además, la representación de búsqueda trabaja con tableros completos; las casillas vacías se introducen después, al crear una partida.

Una alternativa sería representar cada bloque 3 × 3 como una permutación de 1 a 9 y modificar valores dentro de un mismo bloque. En ese caso se conservarían los bloques válidos y la evaluación guiaría la corrección de filas y columnas. Cambiar la unidad que se conserva también cambia los operadores de variación y el vecindario de búsqueda.

## Técnicas PCG e integración

En la escena principal, una ejecución sigue estas etapas:

**Random Start State → población inicial → GA → mejor candidato → SA → resultado final.**

Todas las técnicas contribuyen al mismo objetivo: obtener un tablero completo con **cero conflictos**. Comparten la representación y la función de evaluación.

### 1. Random Start State

Se generan tantos candidatos como indique el parámetro **Población** de GA. Cada fila se inicializa con los números del 1 al 9 y se mezcla mediante **Fisher–Yates**. Esto produce estados iniciales distintos, con filas válidas, aunque pueden existir conflictos en columnas y bloques.

La semilla controla la generación aleatoria. Mantener la misma semilla y configuración permite repetir una ejecución; desactivar la semilla fija produce una nueva en cada generación y muestra el valor utilizado.

### 2. Algoritmo genético

Cada individuo es un tablero completo. El algoritmo utiliza:

- **Selección por torneo:** compara candidatos tomados de la población y elige el de menor costo.
- **Cruce por filas:** combina las primeras filas de un padre con las restantes del otro, utilizando un punto de corte.
- **Mutación:** para cada fila, según la probabilidad configurada, intercambia dos posiciones de esa fila.
- **Elitismo:** copia los mejores individuos a la siguiente generación.

El cruce y la mutación conservan las permutaciones de las filas. El algoritmo se detiene al encontrar un candidato con costo cero o alcanzar el límite de generaciones, y entrega el mejor candidato disponible.

### 3. Simulated Annealing

SA recibe el mejor tablero producido por GA. Un vecino se construye intercambiando **dos columnas distintas de una fila elegida al azar**, conservando la validez de esa fila.

Los vecinos que mejoran el costo o lo mantienen se aceptan. Un empeoramiento de magnitud `Δ` puede aceptarse con probabilidad `exp(-Δ / T)`, donde `T` es la temperatura actual. Después de cada paso, la temperatura se multiplica por el factor de enfriamiento.

Esta aceptación probabilística permite explorar fuera de un mínimo local, en lugar de aceptar solamente mejoras inmediatas. El resultado conserva el **mejor tablero visitado**, aunque el estado actual de la búsqueda haya empeorado temporalmente.

La búsqueda termina al encontrar costo cero, alcanzar la temperatura mínima o consumir el máximo de pasos. Si GA ya resolvió el tablero, SA registra ese estado y termina sin realizar movimientos.

### Función de evaluación

El costo suma los **pares de valores iguales** dentro de cada fila, columna y bloque:

`costo = conflictos de filas + conflictos de columnas + conflictos de bloques`

Por ejemplo, tres apariciones del mismo número dentro de una unidad aportan tres pares en conflicto. Un par puede contribuir a más de una restricción si comparte, por ejemplo, columna y bloque.

**Menor costo significa un mejor candidato; cero identifica una solución válida en un tablero completo.** La evaluación guía los torneos y la conservación de individuos en GA, así como la aceptación de vecinos y el registro del mejor estado en SA. También se muestra en pantalla para comparar resultados.

## Parámetros configurables

Los siguientes valores corresponden a los controles de **Proyecto2 V2** y pueden modificarse durante la ejecución.

### Inicio

| Parámetro | Valor inicial | Efecto |
|---|---|---|
| Usar semilla fija | Activado | Permite repetir una ejecución con la misma configuración. |
| Semilla | `12345` | Entero que controla las secuencias aleatorias. |

### Algoritmo genético

| Parámetro | Rango de la interfaz | Valor inicial | Efecto |
|---|---|---|---|
| Población | 20–100 | 100 | Cantidad de tableros de la población inicial y de cada generación. |
| Generaciones | 20–150 | 150 | Máximo de generaciones de GA. |
| Crossover | 0,5–1 | 0,8 | Probabilidad de cruzar dos padres al producir un hijo. |
| Mutación | 0,01–0,3 | 0,2 | Probabilidad de intentar un intercambio en cada fila. |
| Torneo | 2–5 | 5 | Cantidad de participantes en cada selección por torneo. |
| Elitismo | 0–5 | 5 | Cantidad de mejores individuos conservados entre generaciones. |

### Simulated Annealing

| Parámetro | Rango de la interfaz | Valor inicial | Efecto |
|---|---|---|---|
| Temperatura inicial | 0,1–50 | 0,2 | Controla la aceptación inicial de empeoramientos. |
| Factor de enfriamiento | 0,9–0,9999 | 0,9999 | Multiplicador de la temperatura por paso; cerca de 1 enfría más lentamente. |
| Temperatura mínima | 0,0001–0,1 | 0,0001 | Umbral de detención por temperatura. |
| Pasos máximos | 1–50.000 | 50.000 | Límite de vecinos explorados. |

La temperatura inicial debe ser mayor que la mínima, ambas positivas, y el factor de enfriamiento debe estar entre 0 y 1 sin incluir los extremos. El torneo no puede superar la población y el elitismo debe ser menor que ella. La interfaz informa las configuraciones inválidas antes de iniciar la búsqueda.

## Uso y controles

| Acción | Control |
|---|---|
| Ajustar una etapa | Abrir su pestaña y modificar los controles. |
| Ejecutar Random + GA + SA | **Generar**. Cada pulsación inicia una ejecución nueva. |
| Comparar etapas | **Anterior** y **Siguiente** alternan entre el resultado de GA y el resultado final de SA. |
| Crear una partida | **Jugar**, desde un tablero con cero conflictos. |
| Seleccionar una casilla | Clic sobre la casilla. |
| Introducir un número | Teclas **1–9**, de la fila numérica o del teclado numérico. |
| Borrar una respuesta | **Retroceso**, **Suprimir** o **0**. |

Al jugar se vacían **40 casillas** de la solución. Las pistas permanecen fijas, los números introducidos se distinguen por color y los conflictos se resaltan. Al completar el tablero sin conflictos se muestra **«¡Sudoku resuelto! Felicitaciones.»**.

La interfaz utiliza la fuente **Monogram** y líneas más gruesas para distinguir los bloques 3 × 3.

## Limitaciones actuales

- GA y SA pueden terminar con conflictos si agotan sus límites de búsqueda. En ese caso, **Jugar** rechaza el tablero y se puede generar otro o ajustar los parámetros.
- La eliminación de casillas conserva al menos la solución de origen, pero **no verifica que el puzzle tenga una única solución**.
- La cantidad de casillas eliminadas no constituye una medida de dificultad lógica; no hay clasificación automática de dificultad.
- Las búsquedas se ejecutan de forma síncrona; la interfaz espera hasta que termine la generación.
- El juego requiere teclado y ratón; no incluye controles táctiles para introducir números.

## Créditos

### Graphics

| Asset | Author | Link |
|---|---|---|
| Fantasy UI Borders | **Kenney** | [kenney.nl](https://kenney.nl/assets/fantasy-ui-borders) |

### Fonts

| Font | Author | Link |
|---|---|---|
| Monogram | **Datagoblin** | [itch.io](https://datagoblin.itch.io/monogram) |
