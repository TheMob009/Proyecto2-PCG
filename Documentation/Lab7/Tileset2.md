# Tileset 2: tuneles de cueva

Siete sprites generados con la herramienta integrada de generacion de imagenes, con la pieza horizontal como referencia visual para las otras seis. Se preparan en Unity a 128 x 128 px, Sprite Single, filtro Point, sin mipmaps y sin compresion.

| Pieza | Lados abiertos |
|---|---|
| cave_default | Ninguno (roca solida) |
| cave_line_LR | L, R |
| cave_line_TB | T, B |
| cave_corner_TR | T, R |
| cave_corner_TL | T, L |
| cave_corner_BR | B, R |
| cave_corner_BL | B, L |

T = arriba, B = abajo, R = derecha y L = izquierda. Los siete patrones tienen peso 1 y estan habilitados. Este conjunto permite roca o tuneles con dos conexiones, sin ramificaciones; el primer conjunto tambien admite extremos, intersecciones y cruces. La compatibilidad exige que los bordes enfrentados coincidan (abierto con abierto, cerrado con cerrado).

Los bordes exteriores del mapa son libres, como en el primer conjunto: las conexiones pueden continuar fuera de la matriz. No se impone conectividad global ni se usa backtracking. Una contradiccion se informa y permite reiniciar.

## Uso y evidencias

Abrir `Assets/Scenes/WFC.unity`, pulsar Play, seleccionar `Cambiar tileset` y luego `Inicializar / reiniciar`. La primera propagacion comprueba las restricciones iniciales; despues se alternan observacion y propagacion. Tambien se puede usar reproduccion automatica y pausa.

Se verificaron ambos conjuntos en Unity 2022.3.62f3: 240 comprobaciones de generacion, adyacencia, importacion, controles y representacion. Las capturas se obtuvieron de la escena real, renderizada a 1280 x 800:

| Conjunto | Observacion | Propagacion | Resultado |
|---|---|---|---|
| Caminos | [Captura](Capturas/caminos_observacion.png) | [Captura](Capturas/caminos_propagacion.png) | [Captura](Capturas/caminos_resultado.png) |
| Cueva | [Captura](Capturas/cueva_observacion.png) | [Captura](Capturas/cueva_propagacion.png) | [Captura](Capturas/cueva_resultado.png) |

## Prompts de generacion

### cave_line_LR.png

```text
Use case: stylized-concept.
Asset type: ONE square top-down 2D game tile for a modular cave tunnel tileset used in Unity Wave Function Collapse.
Create cave_line_LR: a SINGLE horizontal walkable tunnel excavated through solid natural rock, connecting ONLY the centers of the LEFT and RIGHT edges. It is not a picture of an entrance: we look straight down on a cross-section map of underground tunnels.
Style: crisp restrained hand-painted pixel art, low resolution 128 x 128 design, square full-bleed opaque tile, consistent compact details legible at 128 pixels. A warm dusty taupe stone floor forms one continuous central corridor, dark charcoal brown rock surrounds it, subtle warm slate-gray beveled rocky wall edges. Flat ambient overhead lighting, no directional cast shadows.
Geometry is essential: the corridor centerline is precisely y=50% all the way left to right. The walkable floor occupies y=31.25% to 68.75% at BOTH left and right image edges; wall rims extend to y=25% and 75%. At left/right edges, tunnel must continue off the canvas with NO cap, entrance arch, border, or obstruction. First and last 10% of the corridor are straight with identical uniform taupe floor and charcoal rock colors at the border. Top and bottom edges consist entirely of continuous dark solid rock; no other openings. Interior can have slight natural irregularity and a few small stone chips but keep the floor mostly clear. Rock corners and closed edges share the same dark charcoal palette, no black background outside the tile. No vignette.
Do not include pipes, grass, water, rails, wooden supports, torches, crystals, characters, text, grid lines, sheet, labels, gutters, drop shadows, perspective or isometric effects. Render only the single tile filling the square.
```

### cave_default.png

```text
Use case: stylized-concept. Asset type: ONE opaque square top-down 2D cave map game tile. Input image is the STYLE AND MATERIAL REFERENCE, not a sheet to reproduce.
Make a matching tile from the SAME cave set. Match the reference's pixel-art style, warm dusty taupe walkable tunnel floor, charcoal-brown solid rock masses, small rocky beveled walls, restrained stone chips, flat overhead ambient lighting, texture detail scale and contrast. The surrounding dark rock is solid unexcavated cave, not an empty background. Full bleed square tile with no frame.
All open exits must be centered exactly at the MIDPOINT of their image edge, with the SAME tunnel width as the reference horizontal tunnel. The warm floor must reach each open edge, uninterrupted and uncapped. At an opening the walkable floor spans 37.5% to 62.5% of that edge and rocky wall rims span 31.25% to 68.75%. In the outermost 10% near each opening keep constant straight width and align walls precisely so these modular pieces connect. Every CLOSED edge is continuous solid dark rock. The tunnel may bend only near the center. No extra paths or branches. Render this single tile only, designed to stay legible after import at 128 x 128 pixels.
Avoid text, labels, grids, gutters, borders, scene perspective, isometric view, cave entrance arches, pipes, grass, water, torches, crystals, wooden props, rails, characters, vignettes and cast shadows.
Exact tile geometry: There is NO TUNNEL anywhere. Fill the ENTIRE tile with continuous solid natural charcoal-brown cave rock matching the reference's rock masses, including the center and all four edges. No walkable floor, holes, doors, bright patches or corridors.
```

### cave_line_TB.png

```text
Use case: stylized-concept. Asset type: ONE opaque square top-down 2D cave map game tile. Input image is the STYLE AND MATERIAL REFERENCE, not a sheet to reproduce.
Make a matching tile from the SAME cave set. Match the reference's pixel-art style, warm dusty taupe walkable tunnel floor, charcoal-brown solid rock masses, small rocky beveled walls, restrained stone chips, flat overhead ambient lighting, texture detail scale and contrast. The surrounding dark rock is solid unexcavated cave, not an empty background. Full bleed square tile with no frame.
All open exits must be centered exactly at the MIDPOINT of their image edge, with the SAME tunnel width as the reference horizontal tunnel. The warm floor must reach each open edge, uninterrupted and uncapped. At an opening the walkable floor spans 37.5% to 62.5% of that edge and rocky wall rims span 31.25% to 68.75%. In the outermost 10% near each opening keep constant straight width and align walls precisely so these modular pieces connect. Every CLOSED edge is continuous solid dark rock. The tunnel may bend only near the center. No extra paths or branches. Render this single tile only, designed to stay legible after import at 128 x 128 pixels.
Avoid text, labels, grids, gutters, borders, scene perspective, isometric view, cave entrance arches, pipes, grass, water, torches, crystals, wooden props, rails, characters, vignettes and cast shadows.
Exact tile geometry: A SINGLE straight VERTICAL tunnel at x=50%, connecting ONLY TOP and BOTTOM midpoint exits. The LEFT and RIGHT edges are solid dark rock. The floor continues past both top and bottom edges.
```

### cave_corner_TR.png

```text
Use case: stylized-concept. Asset type: ONE opaque square top-down 2D cave map game tile. Input image is the STYLE AND MATERIAL REFERENCE, not a sheet to reproduce.
Make a matching tile from the SAME cave set. Match the reference's pixel-art style, warm dusty taupe walkable tunnel floor, charcoal-brown solid rock masses, small rocky beveled walls, restrained stone chips, flat overhead ambient lighting, texture detail scale and contrast. The surrounding dark rock is solid unexcavated cave, not an empty background. Full bleed square tile with no frame.
All open exits must be centered exactly at the MIDPOINT of their image edge, with the SAME tunnel width as the reference horizontal tunnel. The warm floor must reach each open edge, uninterrupted and uncapped. At an opening the walkable floor spans 37.5% to 62.5% of that edge and rocky wall rims span 31.25% to 68.75%. In the outermost 10% near each opening keep constant straight width and align walls precisely so these modular pieces connect. Every CLOSED edge is continuous solid dark rock. The tunnel may bend only near the center. No extra paths or branches. Render this single tile only, designed to stay legible after import at 128 x 128 pixels.
Avoid text, labels, grids, gutters, borders, scene perspective, isometric view, cave entrance arches, pipes, grass, water, torches, crystals, wooden props, rails, characters, vignettes and cast shadows.
Exact tile geometry: A SINGLE 90-degree curved tunnel connecting ONLY TOP and RIGHT midpoint exits. Start at the TOP center, run vertically down to the center, bend to the RIGHT and exit at RIGHT center. LEFT and BOTTOM edges are entirely solid rock. TOP and RIGHT are both OPEN. The bend must be a clear elbow; never a diagonal shortcut.
```

### cave_corner_TL.png

```text
Use case: stylized-concept. Asset type: ONE opaque square top-down 2D cave map game tile. Input image is the STYLE AND MATERIAL REFERENCE, not a sheet to reproduce.
Make a matching tile from the SAME cave set. Match the reference's pixel-art style, warm dusty taupe walkable tunnel floor, charcoal-brown solid rock masses, small rocky beveled walls, restrained stone chips, flat overhead ambient lighting, texture detail scale and contrast. The surrounding dark rock is solid unexcavated cave, not an empty background. Full bleed square tile with no frame.
All open exits must be centered exactly at the MIDPOINT of their image edge, with the SAME tunnel width as the reference horizontal tunnel. The warm floor must reach each open edge, uninterrupted and uncapped. At an opening the walkable floor spans 37.5% to 62.5% of that edge and rocky wall rims span 31.25% to 68.75%. In the outermost 10% near each opening keep constant straight width and align walls precisely so these modular pieces connect. Every CLOSED edge is continuous solid dark rock. The tunnel may bend only near the center. No extra paths or branches. Render this single tile only, designed to stay legible after import at 128 x 128 pixels.
Avoid text, labels, grids, gutters, borders, scene perspective, isometric view, cave entrance arches, pipes, grass, water, torches, crystals, wooden props, rails, characters, vignettes and cast shadows.
Exact tile geometry: A SINGLE 90-degree curved tunnel connecting ONLY TOP and LEFT midpoint exits. Start at TOP center, run vertically down to the center, bend to the LEFT and exit at LEFT center. RIGHT and BOTTOM edges are entirely solid rock. TOP and LEFT are both OPEN. Clear elbow, never a diagonal shortcut.
```

### cave_corner_BR.png

```text
Use case: stylized-concept. Asset type: ONE opaque square top-down 2D cave map game tile. Input image is the STYLE AND MATERIAL REFERENCE, not a sheet to reproduce.
Make a matching tile from the SAME cave set. Match the reference's pixel-art style, warm dusty taupe walkable tunnel floor, charcoal-brown solid rock masses, small rocky beveled walls, restrained stone chips, flat overhead ambient lighting, texture detail scale and contrast. The surrounding dark rock is solid unexcavated cave, not an empty background. Full bleed square tile with no frame.
All open exits must be centered exactly at the MIDPOINT of their image edge, with the SAME tunnel width as the reference horizontal tunnel. The warm floor must reach each open edge, uninterrupted and uncapped. At an opening the walkable floor spans 37.5% to 62.5% of that edge and rocky wall rims span 31.25% to 68.75%. In the outermost 10% near each opening keep constant straight width and align walls precisely so these modular pieces connect. Every CLOSED edge is continuous solid dark rock. The tunnel may bend only near the center. No extra paths or branches. Render this single tile only, designed to stay legible after import at 128 x 128 pixels.
Avoid text, labels, grids, gutters, borders, scene perspective, isometric view, cave entrance arches, pipes, grass, water, torches, crystals, wooden props, rails, characters, vignettes and cast shadows.
Exact tile geometry: A SINGLE 90-degree curved tunnel connecting ONLY BOTTOM and RIGHT midpoint exits. Start at BOTTOM center, run vertically up to the center, bend to the RIGHT and exit at RIGHT center. LEFT and TOP edges are entirely solid rock. BOTTOM and RIGHT are both OPEN. Clear elbow, never a diagonal shortcut.
```

### cave_corner_BL.png

```text
Use case: stylized-concept. Asset type: ONE opaque square top-down 2D cave map game tile. Input image is the STYLE AND MATERIAL REFERENCE, not a sheet to reproduce.
Make a matching tile from the SAME cave set. Match the reference's pixel-art style, warm dusty taupe walkable tunnel floor, charcoal-brown solid rock masses, small rocky beveled walls, restrained stone chips, flat overhead ambient lighting, texture detail scale and contrast. The surrounding dark rock is solid unexcavated cave, not an empty background. Full bleed square tile with no frame.
All open exits must be centered exactly at the MIDPOINT of their image edge, with the SAME tunnel width as the reference horizontal tunnel. The warm floor must reach each open edge, uninterrupted and uncapped. At an opening the walkable floor spans 37.5% to 62.5% of that edge and rocky wall rims span 31.25% to 68.75%. In the outermost 10% near each opening keep constant straight width and align walls precisely so these modular pieces connect. Every CLOSED edge is continuous solid dark rock. The tunnel may bend only near the center. No extra paths or branches. Render this single tile only, designed to stay legible after import at 128 x 128 pixels.
Avoid text, labels, grids, gutters, borders, scene perspective, isometric view, cave entrance arches, pipes, grass, water, torches, crystals, wooden props, rails, characters, vignettes and cast shadows.
Exact tile geometry: A SINGLE 90-degree curved tunnel connecting ONLY BOTTOM and LEFT midpoint exits. Start at BOTTOM center, run vertically up to the center, bend to the LEFT and exit at LEFT center. RIGHT and TOP edges are entirely solid rock. BOTTOM and LEFT are both OPEN. Clear elbow, never a diagonal shortcut.
```


