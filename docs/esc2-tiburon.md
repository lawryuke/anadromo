# Tiburón en esc2

La escena final `Assets/_Project/Scenes/esc2.unity` contiene un paso de
tiburón antes de la zona A. No necesita cargar `Esc2_Enemigos_MVP`.

## Ubicación y mecánica

Objeto raíz: **PasoTiburonEsc2**, con el componente `Esc2SharkPassage`.
Ruta inicial: `(-0.5, -0.5, 9)` → `(-0.5, -0.5, 12)` → `(-0.5, -0.5, 15)`.
`Referencia_Refugio`, en `(0.7, -0.5, 12)`, señala un espacio lateral libre.
Es una referencia del editor, no una barrera ni una zona de invulnerabilidad.

- Al acercarte a menos de 4 m de la ruta, sin una pared interpuesta, aparece
  un aviso de **3 segundos**. El inicio del nivel queda fuera del alcance.
- El tiburón cruza los puntos a **15 m/s**, sin dirigirse hacia el jugador.
- El contacto es **letal**: utiliza la salud y la muerte del encuentro.
- Puedes apartarte lateralmente, cambiar de altura o esperar fuera del eje.
  Mirarlo o quedarse quieto no detiene el paso.
- Desaparece al llegar al final. Puede avisar de nuevo a partir de **18 s
  desde su aparición**, si el jugador sigue o vuelve a estar cerca.
- **R**, después de morir, restaura al jugador y reinicia también el paso.

El cuerpo naranja y sus aletas usan únicamente esferas y cubos con material
propio. Fuera de Play se ve el cuerpo en el inicio de la ruta para colocarlo;
durante Play se oculta hasta terminar el aviso. El aviso actual es texto en
pantalla; no se han añadido audio ni háptica.

## Configuración y duplicación

Selecciona el objeto raíz para ajustar `Travel Speed`, `Warning Duration`,
`Repeat Delay`, `Activation Radius` y los puntos `Ruta_00`, `Ruta_01`, `Ruta_02`.
Gizmos muestra el recorrido y el radio de contacto. Los puntos y el cuerpo
deben permanecer como hijos del mismo paso.

Para duplicar, selecciona el objeto raíz fuera de Play, pulsa **Ctrl+D** y
desplaza el conjunto. Comprueba que el nuevo recorrido y un lateral estén
libres de rocas. No lo añadas al cardumen: tiene su propio recorrido y una
referencia directa **Target = KM_Player**. Si arrastras el prefab
`Assets/_Project/Prefabs/Enemies/PasoTiburonEsc2.prefab`, asigna ese Target.

El instalador comprueba la ruta y un refugio antes de guardarlos. Durante
el juego un barrido frena el paso si encuentra una pared; no busca rutas
alternativas. El daño comprueba cada tramo recorrido, incluso si atraviesa
varios puntos entre frames, para evitar saltarse al jugador a alta velocidad.

## Comprobación

**Anadromo → Esc2 → Depredadores → Verificar tiburon en Play** prueba la
escena guardada: aviso, contacto letal, entrada R, esquiva por teclado con
el controlador original, desaparición, repetición y barridos a 5/30/60/120 FPS.
Coloca al jugador en el encuentro para cada caso; los demás enemigos siguen
activos. El resultado queda en `Logs/Esc2SharkCheck.txt`.
