using System;
using System.Collections.Generic;
using UnityEngine;

namespace Laboratorios.Lab7.WaveFunctionCollapse
{
    public enum WFCPhase
    {
        NotInitialized, ReadyToObserve, Propagating,
        Completed, Contradiction
    }

    public enum WFCOperation { None, Initialization, Observation, Propagation }

    public class WFCGenerator : MonoBehaviour
    {
        [Header("Tile Sets")]
        [SerializeField] private List<WFCTileSet> tileSets = new()
        {
            new WFCTileSet { id = "TileSet_01" },
            new WFCTileSet { id = "TileSet_02" }
        };
        [SerializeField, Min(0)] private int activeTileSetIndex = 0;

        [Header("Mapa")]
        [SerializeField, Min(1)] private int width = 16;
        [SerializeField, Min(1)] private int height = 12;
        [SerializeField, Min(0.1f)] private float cellSize = 1f;

        [Header("Generacion")]
        [SerializeField] private bool randomSeed = false;
        [SerializeField] private int seed = 12345;

        // --------------------------------------------------------
        // ESTRUCTURAS DEL ALGORITMO
        // --------------------------------------------------------

        /*
         * domains[x,y]:
         * Contiene los indices de los patrones que todavia pueden
         * ocupar la posicion (x,y).
         *
         * compatible[d,a,b]:
         * Indica si el patron B puede ubicarse en la direccion d
         * respecto del patron A.
         *
         * pendingArcs:
         * Relaciones entre celdas pendientes de comprobar
         * durante la propagacion.
         */

        private List<int>[,] domains;
        private bool[,,] compatible;
        private Queue<WFCArc> pendingArcs = new();
        private System.Random random;
        private readonly HashSet<Vector2Int> changedCells = new();

        private struct WFCArc
        {
            public int x, y, direction;

            public WFCArc(int x, int y, int direction)
            {
                this.x = x;
                this.y = y;
                this.direction = direction;
            }
        }

        private static readonly Vector2Int[] Offsets =
        {
            new Vector2Int(0, 1),   // North
            new Vector2Int(1, 0),   // East
            new Vector2Int(0, -1),  // South
            new Vector2Int(-1, 0)   // West
        };

        public WFCPhase Phase { get; private set; } = WFCPhase.NotInitialized;
        public string Status { get; private set; } = "Sin inicializar.";
        public WFCOperation LastOperation { get; private set; }
        public Vector2Int LastObservedCell { get; private set; } = new Vector2Int(-1, -1);
        public int ObservationCount { get; private set; }
        public int PropagationCount { get; private set; }
        public int ChangedCellCount => changedCells.Count;
        public int TileSetCount => tileSets == null ? 0 : tileSets.Count;

        // La vista recibe una notificacion al finalizar cada operacion.
        public event Action ObservationsChanged;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public int ActiveTileSetIndex => activeTileSetIndex;
        public bool IsInitialized => domains != null;

        public WFCTileSet ActiveTileSet =>
            tileSets != null && activeTileSetIndex >= 0 &&
            activeTileSetIndex < tileSets.Count
                ? tileSets[activeTileSetIndex] : null;

        // ========================================================
        // CONTROL GENERAL
        // ========================================================

        /*
         * El algoritmo WFC desarrolla cuatro tareas principales:
         *
         * 1. Preparar patrones.
         * 2. Construir restricciones.
         * 3. Generar mediante observacion y propagacion.
         * 4. Representar el resultado.
         *
         * Durante la generacion se trabaja con asignaciones
         * parciales. Los dominios disminuyen progresivamente
         * hasta que cada celda tenga un unico patron.
         *
         * Referencia: Karth y Smith (2017), Seccion 4.
         */

        public string GetTileSetName(int index)
        {
            return tileSets != null && index >= 0 && index < tileSets.Count
                ? tileSets[index].id : "Sin tileset";
        }

        public void ConfigureSize(int newWidth, int newHeight)
        {
            if (newWidth < 1 || newHeight < 1)
                throw new ArgumentOutOfRangeException(nameof(newWidth), "Las dimensiones deben ser positivas.");

            width = newWidth;
            height = newHeight;
            Clear();
        }

        public bool WasChanged(int x, int y) => changedCells.Contains(new Vector2Int(x, y));

        public void SelectTileSet(int index)
        {
            if (tileSets == null || index < 0 || index >= tileSets.Count)
                return;

            activeTileSetIndex = index;
            Clear();
        }

        public bool Initialize()
        {
            Clear();
            LastOperation = WFCOperation.Initialization;

            if (ActiveTileSet == null || ActiveTileSet.Tiles.Count == 0)
            {
                Status = "El TileSet seleccionado no contiene patrones.";
                Debug.LogWarning(Status, this);
                OutputObservations();
                return false;
            }

            if (width < 1 || height < 1)
            {
                Status = "El ancho y el alto deben ser positivos.";
                OutputObservations();
                return false;
            }

            foreach (WFCTile tile in ActiveTileSet.Tiles)
            {
                if (tile == null || float.IsNaN(tile.weight) || float.IsInfinity(tile.weight) || tile.weight <= 0f)
                {
                    Status = "Cada patron debe tener un peso positivo y finito.";
                    OutputObservations();
                    return false;
                }
            }

            random = new System.Random(
                randomSeed ? Environment.TickCount : seed);

            // Preparar los dominios iniciales.
            if (!PatternsFromSample() || domains == null)
            {
                Status = "No hay patrones habilitados en este tileset.";
                OutputObservations();
                return false;
            }

            // Construir las relaciones de compatibilidad.
            BuildPropagator();

            // Revisar tambien las restricciones iniciales, incluso si solo
            // hay un patron habilitado y ninguna celda necesita observarse.
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    EnqueueOutgoing(x, y);

            Phase = WFCPhase.Propagating;
            Status = "Inicializado. Propaga las restricciones iniciales.";

            OutputObservations();
            return true;
        }

        public void Step()
        {
            switch (Phase)
            {
                case WFCPhase.ReadyToObserve:
                    Observe();
                    break;

                case WFCPhase.Propagating:
                    Propagate();
                    break;
            }
        }

        public void Clear()
        {
            domains = null;
            compatible = null;
            pendingArcs.Clear();

            changedCells.Clear();
            LastOperation = WFCOperation.None;
            LastObservedCell = new Vector2Int(-1, -1);
            ObservationCount = 0;
            PropagationCount = 0;
            Phase = WFCPhase.NotInitialized;
            Status = "Sin inicializar.";
            OutputObservations();
        }

        // ========================================================
        // ETAPA 1: PATTERNS FROM SAMPLE
        // ========================================================

        /*
         * OBJETIVO:
         * Preparar los patrones disponibles y los dominios
         * iniciales de todas las posiciones de la matriz.
         *
         * En esta version utilizamos Simple Tiled WFC:
         * los patrones corresponden a piezas definidas en
         * el TileSet, no a subimagenes extraidas automaticamente.
         *
         * EJEMPLO:
         *
         * Si el TileSet contiene cuatro patrones:
         *
         *   P0, P1, P2, P3
         *
         * Pero P2 esta deshabilitado, el dominio inicial es:
         *
         *   D(x,y) = {P0, P1, P3}
         *
         * Todas las celdas comienzan con esos patrones
         * disponibles. Durante WFC se iran descartando opciones.
         */

        private bool PatternsFromSample()
        {
            // Paso 1: recorremos TODOS los tiles del tileset activo (esten
            // habilitados o no) y guardamos solo los INDICES de los que tienen
            // "available == true". Guardamos indices (no los WFCTile en si)
            // porque domains[,] y compatible[,,] trabajan con indices de patron,
            // no con referencias a objetos.
            List<int> patronesHabilitados = new List<int>();

            for(int i = 0; i < ActiveTileSet.Tiles.Count; i++)
            {
                WFCTile tile = ActiveTileSet.Tiles[i];
                if (tile.available)
                {
                    patronesHabilitados.Add(i);
                }

            }

            // Esta comprobacion va DESPUES del loop, no adentro: un tile
            // deshabilitado individual es normal (se saltea sin mas), el
            // problema real es que la lista termine vacia (ningun patron
            // disponible en todo el tileset).
            if (patronesHabilitados.Count == 0)
            {
                Debug.Log("No hay tiles disponibles");
                return false;
            }

            // Paso 2: creamos la grilla de dominios, con una entrada por
            // cada celda del mapa (width x height).
            domains = new List<int>[width, height];

            // Paso 3: cada celda arranca con TODOS los patrones habilitados
            // como posibilidad. Usamos "new List<int>(patronesHabilitados)"
            // para que cada celda reciba su PROPIA copia independiente de la
            // lista -- si todas compartieran la misma referencia, reducir el
            // dominio de una celda durante la propagacion afectaria (por
            // error) a todas las demas celdas al mismo tiempo.
            for(int i = 0; i < width; i++)
            {
                for(int j = 0; j < height; j++)
                {
                    domains[i, j] = new List<int>(patronesHabilitados);
                }
            }

            return true;
        }

        // ========================================================
        // ETAPA 2: BUILD PROPAGATOR
        // ========================================================

        /*
         * OBJETIVO:
         * Construir un indice con las relaciones de compatibilidad
         * entre todos los patrones disponibles.
         *
         * La compatibilidad depende de la direccion relativa.
         *
         * EJEMPLO:
         *
         * Un camino horizontal tiene:
         *
         *   North = 0, East = 1, South = 0, West = 1
         *
         * Si queremos colocar otra pieza al Este, esta debe
         * tener una conexion compatible hacia el Oeste.
         *
         * Esto permite descartar combinaciones incorrectas
         * sin evaluar nuevamente sus conexiones durante
         * cada propagacion.
         *
         * compatible[d,a,b] almacena esta relacion.
         */

        private void BuildPropagator()
        {
            int count = ActiveTileSet.Tiles.Count;
            // compatible[d, a, b] = ¿puede el patron "b" ubicarse en la
            // direccion "d" respecto del patron "a"? Precalculamos esto UNA
            // sola vez aca para no tener que volver a comparar conexiones de
            // tiles cada vez que se ejecuta Propagate().
            compatible = new bool[4, count, count];

            // Recorremos las 4 direcciones del enum WFCDirection
            // (0=North, 1=East, 2=South, 3=West) y, para cada una, TODAS las
            // combinaciones posibles de pares de patrones:
            //   j = patron "origen" (A)
            //   k = patron "vecino candidato" (B)
            for(int i = 0; i < 4; i++)
            {
                WFCDirection direccion = (WFCDirection)i; // convertimos el indice numerico al valor del enum
                for(int j = 0; j < count; j++)
                {
                    for(int k = 0; k < count; k++)
                    {
                        // ¿El patron k puede ir como vecino del patron j,
                        // ubicado en esta direccion respecto de j?
                        compatible[i, j, k] = ActiveTileSet.Tiles[j].IsCompatible(ActiveTileSet.Tiles[k], direccion);
                    }
                }
            }
        }

        // ========================================================
        // ETAPA 3A: OBSERVE
        // ========================================================

        /*
         * OBJETIVO:
         * Seleccionar una celda que todavia no este resuelta
         * y asignarle uno de sus patrones posibles.
         *
         * EJEMPLO:
         *
         * Antes de observar:
         *
         *   D(2,3) = {P0, P2, P5, P7}
         *
         * Si se selecciona P5:
         *
         *   D(2,3) = {P5}
         *
         * La celda queda resuelta, pero sus vecinos pueden
         * mantener patrones incompatibles con esta decision.
         *
         * Por lo tanto, debe ejecutarse la propagacion.
         */

        public bool Observe()
        {
            if (Phase != WFCPhase.ReadyToObserve)
                return false;

            changedCells.Clear();
            LastOperation = WFCOperation.Observation;
            // El ruido de FindLowestEntropy rompe los empates al azar,
            // tambien durante la primera observacion.
            Vector2Int celda = FindLowestEntropy();

            // (-1,-1) es el valor "centinela" que devuelve la busqueda
            // cuando ya no queda ninguna celda sin resolver:
            // en ese caso la generacion ya esta completa.
            if (celda == new Vector2Int(-1, -1))
            {
                Phase = WFCPhase.Completed;
                Status = "Completado";
                OutputObservations();
                return true;
            }

            // De los patrones que TODAVIA son validos en esta celda, elegimos
            // uno al azar, dandole mas probabilidad a los de mayor peso.
            int patronElegido = ChooseWeightedPattern(domains[celda.x, celda.y]);

            // "Colapsar" el dominio de la celda: pasa de tener varios
            // patrones posibles a tener SOLO el elegido. Creamos una lista
            // nueva con ese unico elemento (en vez de modificar la lista
            // anterior) para no arrastrar los demas patrones descartados.
            List<int> listaAux = new List<int>() { patronElegido};

            domains[celda.x, celda.y] = listaAux;
            changedCells.Add(celda);
            LastObservedCell = celda;
            ObservationCount++;

            // Como esta celda acaba de cambiar, sus vecinas dentro del mapa
            // podrian tener patrones que ya no son compatibles con esta
            // decision. Encolamos esas relaciones para que Propagate() las
            // revise (este metodo ya viene implementado).
            EnqueueOutgoing(celda.x, celda.y);
            Status = $"Observada ({celda.x},{celda.y}): {ActiveTileSet.Tiles[patronElegido].id}. Falta propagar.";
            Phase = WFCPhase.Propagating; // el siguiente Step() va a llamar a Propagate()
            OutputObservations();
            return true;
        }

        // --------------------------------------------------------
        // SELECCION DE CELDA
        // --------------------------------------------------------

        private Vector2Int FindLowestEntropy()
        {
            Vector2Int mejorCelda = new Vector2Int(-1, -1);
            double mejorValor = double.PositiveInfinity; // arrancamos en "infinito" para que cualquier entropia real sea menor

            for(int i = 0; i < width; i++)
            {
                for(int j = 0; j < height; j++)
                {
                    // Solo consideramos celdas NO resueltas (mas de un
                    // patron posible). Las resueltas no compiten por ser
                    // elegidas de nuevo.
                    if (domains[i, j].Count > 1)
                    {
                        // Paso 1: sumamos el peso de TODOS los patrones que
                        // siguen en el dominio de esta celda. Lo necesitamos
                        // para poder normalizar las probabilidades en el
                        // paso siguiente (p = peso / pesoTotal).
                        float pesoTotal = 0;

                        foreach (int patron in domains[i, j])
                        {
                            pesoTotal += ActiveTileSet.Tiles[patron].weight;
                        }

                        // Paso 2: entropia de Shannon, H = -sum(p * log(p)),
                        // con p = probabilidad normalizada de cada patron
                        // posible en esta celda. Cuanto MENOR es H, mas
                        // "concentrada" (restringida) esta la celda -- por
                        // eso buscamos la de menor entropia (heuristica MRV).
                        double entropia = 0;

                        foreach (int patron in domains[i, j])
                        {
                            float probabilidadNormalizada = ActiveTileSet.Tiles[patron].weight / pesoTotal;
                            entropia = entropia - (probabilidadNormalizada * Math.Log(probabilidadNormalizada));
                        }

                        // Sumamos un ruido muy pequeño: no altera comparaciones
                        // donde la entropia real es distinta, pero rompe los
                        // empates entre celdas igualmente restringidas (si no
                        // estuviera, siempre ganaria la primera celda del
                        // recorrido, sin ningun componente aleatorio real).
                        double ruido = random.NextDouble() * 0.0001;
                        double entropiaRuido = entropia + ruido;

                        // Nos quedamos con la celda de MENOR entropia vista
                        // hasta ahora en el recorrido.
                        if(entropiaRuido < mejorValor)
                        {
                            mejorValor = entropiaRuido;
                            mejorCelda = new Vector2Int(i,j);
                        }
                    }
                }
            }

            return mejorCelda;
        }

        // --------------------------------------------------------
        // SELECCION DE PATRON
        // --------------------------------------------------------

        private int ChooseWeightedPattern(IReadOnlyList<int> domain)
        {
            // Selección por "ruleta ponderada": sumamos el peso de todos los
            // patrones todavia validos en este dominio -- define el tamaño
            // total de la ruleta imaginaria.
            float pesoTotal = 0f;

            foreach (int indicePatron in domain)
            {
                pesoTotal += ActiveTileSet.Tiles[indicePatron].weight;
            }

            // Sorteamos un punto al azar dentro de [0, pesoTotal). Este valor
            // queda FIJO durante todo el metodo: es "donde cae la bolita"
            // de la ruleta.
            double numeroRandom = random.NextDouble() * pesoTotal;
            float pesoAcumulado = 0f;

            // Recorremos los patrones acumulando su peso (arrancando en 0)
            // hasta que el acumulado supere el numero sorteado: ahi "cayo la
            // bolita". Los patrones con mas peso ocupan un segmento mas
            // grande de la ruleta, asi que tienen mas chances de ser
            // elegidos, pero cualquiera con peso > 0 sigue siendo posible.
            foreach (int indicePatron in domain)
            {
                pesoAcumulado += ActiveTileSet.Tiles[indicePatron].weight;
                if(numeroRandom < pesoAcumulado)
                {
                    return indicePatron;
                }
            }

            // Red de seguridad por posibles errores de redondeo de punto
            // flotante: en teoria el loop de arriba siempre deberia
            // encontrar un patron antes de llegar aca, pero por las dudas
            // devolvemos el ultimo patron del dominio (nunca -1, para no
            // romper el resto del codigo con un indice invalido).
            int indiceFinal = domain.Count - 1;
            return domain[indiceFinal];
        }

        // ========================================================
        // ETAPA 3B: PROPAGATE
        // ========================================================

        /*
         * OBJETIVO:
         * Actualizar los dominios de las celdas relacionadas
         * despues de una observacion o de otra reduccion.
         *
         * EJEMPLO:
         *
         * Se observa un camino horizontal en la celda A.
         * Su conexion Este esta abierta.
         *
         * La celda B, ubicada al Este, tiene:
         *
         *   D(B) = {Horizontal, Vertical}
         *
         * Horizontal tiene West = 1.
         * Vertical tiene West = 0.
         *
         * Por lo tanto, Vertical deja de ser valido:
         *
         *   D(B) = {Horizontal}
         *
         * La reduccion de B puede afectar a sus propios
         * vecinos. Este proceso continua hasta que no
         * queden restricciones pendientes.
         */

        public bool Propagate()
        {
            if (Phase != WFCPhase.Propagating)
                return false;

            LastOperation = WFCOperation.Propagation;
            changedCells.Clear();
            PropagationCount++;

            // Procesamos TODA la cola de arcos pendientes en este mismo
            // llamado a Propagate() (no uno por Step()), hasta que no quede
            // ninguno por revisar. Esto implementa "arc consistency" al
            // estilo AC-3: cada arco representa una relacion direccional
            // pendiente de comprobar.
            while (pendingArcs.Count != 0)
            {
                // Cada arco dice: "la celda (x,y) cambio -- revisa si eso
                // afecta a su vecino ubicado en 'direction'".
                WFCArc arco = pendingArcs.Dequeue();

                // Coordenadas de ese vecino: celda origen + offset de la
                // direccion del arco (mismo calculo que usa EnqueueOutgoing).
                int nx = arco.x + Offsets[arco.direction].x;
                int ny = arco.y + Offsets[arco.direction].y;
                bool changed = false;

                // Trabajamos sobre una COPIA del dominio del vecino, porque
                // vamos a modificar domains[nx,ny] (con Remove) mientras lo
                // recorremos -- hacerlo directamente sobre la lista original
                // tiraria una excepcion de C# ("collection modified during
                // enumeration").
                List<int> listaAux = new List<int>();
                listaAux = new List<int>(domains[nx, ny]);

                // Por cada patron "indiceAux" que todavia es candidato en el
                // dominio del vecino...
                foreach(int indiceAux in listaAux)
                {
                    // ...buscamos si existe AL MENOS UN patron
                    // "indicePrincipal" en el dominio de la celda ORIGEN que
                    // lo soporte (que sea compatible con el en esta
                    // direccion, segun la tabla precalculada en
                    // BuildPropagator).
                    bool supported = false;
                    foreach(int indicePrincipal in domains[arco.x, arco.y])
                    {
                        if (compatible[arco.direction, indicePrincipal, indiceAux])
                        {
                            supported = true;
                            break; // ya encontramos soporte, no hace falta seguir buscando
                        }
                    }

                    // Si NINGUN patron de la celda origen soporta a este
                    // patron del vecino, ya no tiene sentido que siga siendo
                    // una posibilidad valida: lo sacamos de su dominio.
                    if(!supported)
                    {
                        domains[nx, ny].Remove(indiceAux);
                        changed = true;
                    }
                }

                // Si el dominio del vecino cambio, hay dos casos posibles:
                if(changed)
                {
                    changedCells.Add(new Vector2Int(nx, ny));
                    // Caso 1: se quedo sin NINGUN patron posible -> es una
                    // contradiccion. El algoritmo no implementa backtracking,
                    // asi que directamente la reportamos y cortamos la
                    // generacion en este punto.
                    if (domains[nx,ny].Count == 0)
                    {
                        ReportContradiction(nx,ny);
                        OutputObservations();
                        return false;
                    }

                    // Caso 2: el vecino sigue teniendo opciones, pero como SU
                    // dominio cambio, sus propios vecinos tambien podrian
                    // necesitar revision. Encolamos sus arcos salientes para
                    // seguir propagando el efecto en cadena.
                    EnqueueOutgoing(nx, ny);
                }

            }

            // La cola quedo vacia: ya no hay mas relaciones pendientes por
            // revisar. Comprobamos si, con los cambios hechos, la
            // generacion completa quedo resuelta.
            CheckCompletion();

            // Si no hubo contradiccion ni se completo todavia, volvemos a la
            // fase de observacion para que el proximo Step() siga generando
            // (eligiendo y colapsando otra celda).
            if (Phase != WFCPhase.Completed && Phase != WFCPhase.Contradiction)
            {
                Phase = WFCPhase.ReadyToObserve;
                Status = $"Propagacion terminada: {ChangedCellCount} celdas cambiaron. Listo para observar.";
            }
            OutputObservations();
            return true;
        }

        // --------------------------------------------------------
        // HERRAMIENTAS DE PROPAGACION
        // --------------------------------------------------------

        /*
         * WFCArc representa una relacion dirigida:
         *
         *   (x,y) ---- direccion ----> vecino
         *
         * EnqueueOutgoing agrega las relaciones entre una
         * celda y sus vecinos existentes.
         *
         * La cola pendingArcs permite revisar estas relaciones
         * durante la propagacion.
         *
         * Este metodo se entrega implementado.
         */

        private void EnqueueOutgoing(int x, int y)
        {
            for (int d = 0; d < 4; d++)
            {
                int nx = x + Offsets[d].x;
                int ny = y + Offsets[d].y;

                if (IsInBounds(nx, ny))
                    pendingArcs.Enqueue(new WFCArc(x, y, d));
            }
        }

        // Se utiliza cuando alguna celda pierde todas
        // sus posibilidades de asignacion.
        private void ReportContradiction(int x, int y)
        {
            Phase = WFCPhase.Contradiction;
            Status = $"Contradiccion en ({x},{y}): dominio vacio.";
            Debug.LogWarning(Status, this);
        }

        // Comprueba si todas las posiciones tienen un unico
        // patron y ya no existen relaciones pendientes.
        private void CheckCompletion()
        {
            if (domains == null || Phase == WFCPhase.Contradiction ||
                pendingArcs.Count > 0)
                return;

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (domains[x, y] == null || domains[x, y].Count != 1)
                        return;

            Phase = WFCPhase.Completed;
            Status = "Generacion completada.";
        }

        // ========================================================
        // ETAPA 4: OUTPUT OBSERVATIONS
        // ========================================================

        /*
         * OBJETIVO:
         * Representar el estado actual de la generacion.
         *
         * Durante WFC, una celda puede encontrarse:
         *
         * - Sin resolver: mantiene multiples patrones.
         * - Resuelta: tiene un unico patron.
         * - En contradiccion: no tiene patrones disponibles.
         *
         * EJEMPLO:
         *
         *   8  4  2
         *   4  1  2
         *   8  4  8
         *
         * Los numeros representan las posibilidades restantes
         * por celda. La posicion central ya esta resuelta.
         *
         * La visualizacion deberia permitir observar como
         * los dominios cambian durante la generacion.
         *
         * Puede utilizarse SpriteRenderer, Gizmos, Canvas
         * u otros elementos de Unity.
         */

        private void OutputObservations()
        {
            // WFCBoardView conserva las celdas y actualiza sus sprites,
            // posibilidades y colores. El algoritmo no crea objetos de UI.
            ObservationsChanged?.Invoke();
        }

        // ========================================================
        // CONSULTAS PARA LA VISUALIZACION
        // ========================================================

        // Verifica si una posicion pertenece a la matriz.
        public bool IsInBounds(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }

        // Obtiene la cantidad de patrones posibles.
        // Devuelve -1 para una posicion invalida.
        public int GetDomainCount(int x, int y)
        {
            if (!IsInitialized || !IsInBounds(x, y))
                return -1;

            return domains[x, y] != null
                ? domains[x, y].Count : -1;
        }

        // Permite consultar los patrones restantes.
        // Los valores corresponden a indices del TileSet.
        public IReadOnlyList<int> GetDomain(int x, int y)
        {
            if (!IsInitialized || !IsInBounds(x, y))
                return null;

            return domains[x, y];
        }

        // Obtiene el centro de una celda en coordenadas
        // globales, considerando las dimensiones de la matriz.
        public Vector3 GetCellWorldPosition(int x, int y)
        {
            Vector3 position = new Vector3(
                (x - (width - 1) * 0.5f) * cellSize,
                (y - (height - 1) * 0.5f) * cellSize,
                0f);

            return transform.TransformPoint(position);
        }
    }
}
