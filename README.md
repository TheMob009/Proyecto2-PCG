# Generación procedural de Sudoku

Proyecto de la Unidad II de Generación Procedural de Contenido, desarrollado en **Unity 2022.3.62f3**. Combina **Random Start State**, **algoritmo genético (GA)** y **Simulated Annealing (SA)** para generar Sudoku jugables con cantidad de pistas y dificultad lógica objetivo.

## Integrantes

| Integrante | GitHub |
|---|---|
| Matías Ortega | [TheMob009](https://github.com/TheMob009) |
| Noriyuki Kato | [NoriKato16](https://github.com/NoriKato16) |

## Ejecutar

1. Abrir el proyecto con **Unity 2022.3.62f3**.
2. Abrir **Assets/Scenes/Proyecto2 V2.unity** y presionar **Play**.
3. Abrir la pestaña de SA para seleccionar **Pistas objetivo** y **Dificultad**.
4. Pulsar **Generar**. Las pestañas muestran parámetros del mismo flujo completo.
5. Comparar la solución de GA y el puzzle de SA con **Anterior** y **Siguiente**.
6. Pulsar **Jugar** desde el puzzle generado.

El menú **Tools → PCG → Configurar controles de pistas** permite configurar los controles de ambas escenas del proyecto. Este comando guarda esas escenas; debe utilizarse fuera de Play.

## Flujo de generación

**Random Start → población de GA → solución completa válida → SA de pistas → partida.**

Random Start mezcla los dígitos 1–9 de cada fila mediante Fisher–Yates. GA utiliza selección por torneo, cruce por filas, intercambios dentro de filas y elitismo. Su costo suma pares de dígitos iguales en filas, columnas y bloques; cero identifica una solución completa válida.

Si GA agota sus generaciones con conflictos, la interfaz informa el problema y no inicia la selección de pistas. Se debe ajustar GA o utilizar otra semilla.

### Simulated Annealing: cantidad y dificultad de las pistas

SA recibe una solución completa válida y busca qué casillas mostrar. Cada estado equivale a una máscara de 81 pistas: el valor de una pista siempre procede de la solución original, que se conserva sin cambios.

La preparación elimina pistas en orden aleatorio, comprobando unicidad, hasta acercarse a la cantidad solicitada. SA explora tres movimientos:

- **Eliminar** una pista.
- **Reponer** una pista de la solución original.
- **Trasladar** una pista: ocultar una y revelar otra, manteniendo la cantidad.

Los estados sin solución única comprobada se rechazan siempre. En los demás, SA acepta mejoras y empates; acepta un aumento de costo `Δ` con probabilidad `exp(-Δ / T)`. La temperatura se multiplica por el enfriamiento en cada propuesta de SA, incluidos los vecinos rechazados por unicidad.

El presupuesto de intentos es global: incluye preparación, vecinos rechazados y reinicios. Cada reinicio reconstruye un conjunto de pistas y restablece la temperatura; el mejor estado se conserva durante toda la ejecución.

### Verificación de unicidad

`SudokuSolutionCounter` utiliza backtracking y selecciona la casilla con menos candidatos (MRV). Se detiene al encontrar dos soluciones, ya que solo necesita distinguir **cero, una o varias**.

El límite de nodos es determinista. Si se agota antes de certificar el resultado, devuelve **desconocido** y SA rechaza el candidato. Encontrar una solución sin completar la comprobación no demuestra unicidad.

### Dificultad lógica

`SudokuLogicalEvaluator` trabaja sobre una copia y resuelve sin adivinar. Agota las técnicas más sencillas antes de avanzar:

| Clasificación | Técnicas utilizadas |
|---|---|
| Trivial | Tablero ya completo. |
| Fácil | Candidato único por casilla y posición única de un dígito en una fila, columna o bloque. |
| Media | Las anteriores y candidatos bloqueados, tanto pointing como claiming. |
| Difícil | Las anteriores y pares desnudos. |
| Fuera del repertorio lógico | El evaluador no pudo completar el puzzle con esas técnicas. |

El informe conserva la técnica más avanzada utilizada, los pasos por técnica y las casillas pendientes. La clasificación es una convención del proyecto basada en su repertorio y orden de resolución; requiere calibración con jugadores para representar dificultad humana. Un puzzle bloqueado no se etiqueta automáticamente como difícil.

### Función de costo de las pistas

Para estados con solución única:

```text
errorPistas = abs(pistasActuales - pistasObjetivo) / 81
errorDificultad = (abs(rangoActual - rangoObjetivo) / 4 + casillasPendientes / 81) / 2
costo = pesoPistas * errorPistas + pesoDificultad * errorDificultad
```

Los rangos son: trivial=0, fácil=1, media=2, difícil=3 y fuera del repertorio=4. Los pesos deben ser positivos. Los estados fuera del repertorio tienen costo finito para que SA pueda atravesarlos, pero no cumplen el objetivo ni habilitan Jugar.

El éxito requiere **cantidad exacta de pistas, dificultad solicitada y solución única**. Si se agotan los límites, se muestra un resultado aproximado con los objetivos y valores alcanzados. Un resultado aproximado solo habilita Jugar si el evaluador lógico pudo resolverlo; su unicidad ya está comprobada.

## Parámetros

### Inicio y GA

La semilla fija y la misma configuración reproducen la ejecución en el mismo entorno. Desactivar la semilla fija genera y muestra otra semilla. La secuencia de SA se deriva de la semilla principal y es independiente de la de GA.

| Parámetro de GA | Rango de interfaz | Inicial |
|---|---|---|
| Población | 20–200 | 200 |
| Generaciones | 20–10.000 | 5.000 |
| Crossover | 0–1 | 0,30 |
| Mutación | 0,01–0,3 | 0,15 |
| Torneo | 2–10 | 5 |
| Elitismo | 0–10 | 5 |

Los valores predeterminados se calibraron conservando los operadores de GA. Reducir el cruce permite conservar más combinaciones de filas y aumentar población y generaciones ofrece más oportunidades de encontrar una solución. GA se detiene en cuanto alcanza cero conflictos; 5.000 es el máximo de generaciones.

La validación en **Unity 2022.3.62f3, sin renderizado**, resolvió **100 de 100 semillas** con cero conflictos. En el equipo de prueba, el tiempo medio fue **239 ms**, la mediana **100 ms**, el percentil 95 **944 ms** y el máximo **1.592 ms**. La muestra incluye la semilla inicial `12345`, cero, semillas negativas y los extremos de `int`. Este resultado es empírico: GA conserva un presupuesto finito y puede no resolver otras semillas.

Los componentes y los seis controles de ambas escenas usan los mismos valores. El menú **Tools → PCG → Restablecer parámetros predeterminados de GA** permite restaurarlos; guarda ambas escenas y debe utilizarse fuera de Play.

### SA de pistas

| Control | Rango de interfaz | Inicial | Función |
|---|---|---|---|
| Pistas objetivo | 17–80 | 41 | Cantidad exacta de casillas visibles solicitada. |
| Perfil de dificultad | Fácil, Media, Difícil | Fácil | Carga pistas y parámetros recomendados al cambiar de nivel. |
| Temperatura inicial | 0,001–0,2 | 0,05 | Aceptación inicial de empeoramientos. |
| Enfriamiento | 0,9–0,9999 | 0,999 | Reducción de temperatura por propuesta. |
| Temperatura mínima | 0,00001–0,001 | 0,0001 | Umbral de detención de cada recorrido. |
| Intentos totales | 100–30.000 | 5.000 | Presupuesto global de propuestas. |
| Reinicios máximos | 0–10 | 2 | Nuevos recorridos conservando el mejor resultado. |
| Peso de pistas | 0,1–5 | 1 | Importancia relativa de la cantidad. |
| Peso de dificultad | 0,1–5 | 1 | Importancia relativa del nivel lógico. |

En el Inspector de **SudokuSimulatedAnnealing** también se configura **Solution Node Limit** (inicial: 20.000), el máximo de nodos por comprobación de unicidad. Aumentarlo permite verificar candidatos más costosos y aumenta el tiempo de búsqueda.

Los perfiles seleccionan una cantidad de pistas compatible con la búsqueda de cada nivel:

| Parámetro | Fácil | Media | Difícil |
|---|---|---|---|
| Pistas | 41 | 30 | 28 |
| Temperatura inicial | 0,05 | 0,025 | 0,025 |
| Enfriamiento | 0,999 | 0,999 | 0,999 |
| Temperatura mínima | 0,0001 | 0,0001 | 0,0001 |
| Intentos totales | 5.000 | 30.000 | 30.000 |
| Reinicios máximos | 2 | 5 | 5 |
| Peso de pistas | 1 | 3 | 3 |
| Peso de dificultad | 1 | 1 | 1 |

En **Pistas · SA**, cambiar **Perfil de dificultad** carga todos esos valores. Después se pueden modificar los controles individualmente; volver a cambiar de nivel vuelve a cargar su perfil. La opción **Use Difficulty Presets** del Inspector permite desactivar esta carga automática y conservar los ajustes manuales al cambiar la dificultad. El límite de nodos configurado en el Inspector se conserva. Los valores guardados en la escena también se conservan al iniciar Play, hasta que se elija otro nivel.

Los perfiles están definidos en `SudokuDifficultyPresets`. El mayor peso de pistas penaliza desviaciones de la cantidad solicitada; los niveles avanzados disponen de seis recorridos y un presupuesto global mayor. La búsqueda termina antes si alcanza ambos objetivos.

Validación del 21 de octubre de 2026 en Unity 2022.3.62f3, sin renderizado: 100 soluciones de GA diferentes, cada una utilizada para los tres perfiles, con la misma derivación de semilla que el juego. Se volvió a comprobar la unicidad y que media y difícil no se resolvieran con las técnicas del nivel anterior.

| Perfil | Cantidad y nivel exactos | Tiempo medio de SA | Máximo observado |
|---|---|---|---|
| Fácil | 100/100 | 1,6 ms | 6,0 ms |
| Media | 100/100 | 34,8 ms | 124,2 ms |
| Difícil | 100/100 | 99,0 ms | 650,2 ms |

Estos tiempos solo incluyen SA; excluyen GA, renderizado y la comprobación independiente posterior. Son una medición local, y la tasa observada no garantiza éxito con cualquier semilla o ajuste manual. **Tools → PCG → Verificar perfiles de dificultad de SA** reproduce esta validación fuera de Play y escribe `.utmp/sa-consistency.csv`. La comparación desde .NET se ejecuta con `dotnet run --configuration Release --project Tests/Sudoku.CoreChecks.csproj -- --tune-sa 100 presets`.

La temperatura inicial debe superar a la mínima y el enfriamiento debe estar estrictamente entre cero y uno. No todas las combinaciones de cantidad, dificultad y solución de origen son alcanzables.

## Juego y presentación

El tablero usa `int[9,9]`, con cero para una casilla vacía, y una máscara `bool[9,9]` para las pistas. **Jugar utiliza exactamente el puzzle mostrado y evaluado por SA**.

La interfaz se distribuye para una vista de **960 × 540**. Los parámetros de GA y SA aparecen en una sola columna, con etiquetas y valores separados y sin saltos de línea. Los controles y resultados usan Liberation Sans; el título conserva Monogram y los bordes del proyecto. El resumen ocupa un área independiente del tablero y los botones de navegación. **Tools → PCG → Ajustar legibilidad de la interfaz** aplica esta distribución a ambas escenas, conservando los parámetros configurados.

| Acción | Control |
|---|---|
| Seleccionar casilla | Clic. |
| Introducir respuesta | 1–9, fila numérica o teclado numérico. |
| Borrar respuesta | Retroceso, Suprimir o 0. |

Las pistas permanecen fijas, las respuestas tienen otro color y los conflictos se resaltan. Al completar el tablero sin conflictos se muestra el mensaje de victoria.

La selección de pistas se ejecuta por bloques de trabajo y cede control durante la comprobación de unicidad. GA sigue ejecutándose de forma síncrona. Las búsquedas tienen límites y no garantizan alcanzar los objetivos dentro del presupuesto.

## Verificaciones

Con **.NET SDK 10** instalado, las comprobaciones del núcleo se ejecutan sin Unity ni GPU y no necesitan paquetes externos:

```powershell
dotnet run --project Tests/Sudoku.CoreChecks.csproj
dotnet run --project Tests/Sudoku.CoreChecks.csproj -- --benchmark
dotnet run --configuration Release --project Tests/Sudoku.CoreChecks.csproj -- --tune-ga 30 defaults
./Tests/Run-UnityChecks.ps1
```

Cubren unicidad, límites de verificación, clasificación lógica, reproducibilidad, conservación de las pistas y detenciones sin éxito. La comparación de semillas informa cantidad y dificultad alcanzadas y movimientos de empeoramiento aceptados; no constituye una garantía de rendimiento para otros tableros.

Las comprobaciones de integración de Unity también están disponibles en **Tools → PCG → Verificar generación de pistas**. El script de PowerShell las ejecuta sin renderizado, requiere cerrar el editor que tenga abierto este proyecto y escribe el registro en `.utmp/scene-checks.log`.

**Tools → PCG → Verificar parámetros predeterminados de GA** ejecuta la validación de 100 semillas con los parámetros guardados en la escena principal. El comando `--tune-ga` permite comparar configuraciones del GA de producción desde .NET; sus resultados pueden variar respecto de Unity por las diferencias entre runtimes.

## Enlaces y créditos

- [Repositorio](https://github.com/TheMob009/Proyecto2-PCG).
- [Sudoku en Ludii](https://ludii.games/details.php?keyword=Sudoku).
- [Página de publicación en itch.io](https://themob-00.itch.io/pcg-sudoku-map-generator).
- Fantasy UI Borders: [Kenney](https://kenney.nl/assets/fantasy-ui-borders).
- Monogram: [Datagoblin](https://datagoblin.itch.io/monogram).
