# Laboratorio 7 - Wave Function Collapse

Proyecto de Unity **2022.3.62f3** dedicado a Simple Tiled WFC.

## Ejecutar

En Unity, abrir `Assets/Scenes/WFC.unity` y pulsar Play. Para Windows, abrir `WFC_Lab7.exe` dentro de la carpeta completa de la compilacion. El ejecutable necesita las carpetas y bibliotecas que lo acompanan; distribuir el ZIP completo.

## Controles

- `Ancho` y `Alto`: dimensiones entre 1 y 32; aplicar con `Inicializar / reiniciar`.
- `Cambiar tileset`: alterna entre los caminos y los tuneles de cueva; despues inicializar.
- `Observar` / `Propagar`: avanza una etapa por pulsacion. La primera propagacion valida las restricciones iniciales.
- `Reproducir` / `Pausar`: ejecucion automatica con intervalo ajustable.
- `Limpiar`: descarta los dominios actuales.
- Clic en una celda: consulta los patrones que todavia puede contener.

Las miniaturas muestran las posibilidades restantes; el numero indica su cantidad. Amarillo identifica la observacion, azul las reducciones de propagacion y rojo una contradiccion. El sistema informa cuando completa el mapa o cuando debe reiniciarse.

## Contenido

- `Assets/Scripts/Lab7`: generador, patrones, vista y comprobaciones.
- `Assets/Scenes/WFC.unity`: unica escena incluida en la compilacion.
- `Assets/Sprites/Tileset1`: 16 patrones de caminos.
- `Assets/Sprites/Tileset2`: 7 patrones de roca y tuneles con dos conexiones.
- `Documentation/Lab7`: configuracion, prompts de las imagenes de cueva, capturas y resultados de verificacion.

Se conservo **todo el contenido preexistente de `Assets/Sprites`**, incluidos los recursos adicionales de interfaz y fuente. Los cuatro archivos de shader/include restantes de TextMesh Pro son dependencias del material de esa fuente. Se retiraron las escenas, scripts, prefabs, datos y compilaciones de los laboratorios anteriores.

## Compilar Windows x64

El metodo `Laboratorios.Lab7.WaveFunctionCollapse.Editor.WFCBuild.BuildWindowsBatch` genera una compilacion Release con Mono y solamente la escena WFC. Se puede invocar desde Unity en modo batch usando `-executeMethod`, `-buildTarget Win64` y un `-projectPath` que no este abierto en otra instancia. Las opciones adicionales `-wfcBuildOutput` y `-wfcBuildReport` permiten indicar las rutas del ejecutable y del informe.

## Verificacion del ejecutable

La compilacion final de Windows x64 se completo sin errores ni advertencias. El propio ejecutable supero **725 comprobaciones**. El informe y las capturas estan en [Documentation/Lab7/PruebasEjecutable](Documentation/Lab7/PruebasEjecutable/LEEME.md).

El propio ejecutable admite `-wfc-self-test -wfc-test-output <carpeta>` para comprobar su escena, controles con eventos de puntero y deteccion de clics, ambos tilesets, adyacencias, reproduccion, pausa, reinicio, dimensiones y contradicciones. Este modo escribe capturas y un informe JSON, y termina con codigo 0 si las comprobaciones pasan. Sin esa opcion se abre la aplicacion normal.

Los bordes exteriores son libres. No se exige conectividad global ni se implementa backtracking: una contradiccion se informa y el usuario puede reiniciar.
