# Pirañas en esc2 — zona A

> Actualizacion: en la escena final esc2, vida/comida se han unificado en
> [energia vital](esc2-energia-vital.md). Las cantidades de dano indicadas
> abajo descuentan energia; la barra de salud y los bordes rojos se retiraron.

La escena `Assets/_Project/Scenes/esc2.unity` contiene dos pirañas de
primitivas 3D en la cámara de entrada A. No necesitan cargar el escenario MVP.

En la jerarquía:

```text
ZonaA (PiranhaSchool, Target = KM_Player)
  Pirana_A_01
    Cuerpo
  Pirana_A_02
    Cuerpo
  Pez_Linterna_A_01
  Lamprea_A_01
  Lamprea_A_02
```

El territorio usa el objeto `ZonaA` de la jerarquía actual. Su caja se ha
ampliado para contener las posiciones existentes de los cinco enemigos.
Selecciónalo para ver sus límites con Gizmos activos.
Las posiciones de los enemigos son relativas al cardumen: al mover la zona,
mueve su objeto raíz completo. No uses las coordenadas antiguas de la entrada.
La posición inicial del jugador queda fuera del territorio: el encuentro
comienza al avanzar hacia la cámara, no al pulsar Play.

## Cómo probar

La zona A incluye también un pez linterna y dos lampreas. Los tres comparten
la salud del jugador; el jugador no lleva linterna propia.

El pez linterna permanece quieto con el señuelo encendido. Al detectarte
dentro de 5 m, apaga el señuelo, espera 1,8 s y embiste en dirección fija.
Apártate o cambia de altura; mirarlo no frena el ataque. Si falla, se recupera
durante 2 s.

Las lampreas te persiguen a 4 m/s dentro de la zona. Al adherirse ralentizan
tu nado un 15% cada una y drenan 5 puntos de salud cada 2 s. Alternar A–D–A–D con entre 0,12 y 0,6 s entre pulsaciones o sacudir rápidamente el ratón las desprende.
Quedan aturdidas durante 3 s antes de poder volver a perseguirte.

Abre esc2 y pulsa Play. Avanza hacia las dos formas verdes. Al detectarte
cambian a amarillo; si permaneces cerca 2,5 s, atacan y se vuelven rojas.
Superar el umbral de ruido dentro del alcance provoca el ataque inmediato.

Los valores actuales de GameSettings son 1 m/s y 2 m/s; se conservan.
El umbral de ruido de las pirañas sigue en 6 m/s, así que con estos valores
Shift no activa el ataque inmediato; sí se activa por proximidad tras 2,5 s. La velocidad se mide con el desplazamiento del
cuerpo en pasos de física, para que las diferencias entre FPS y física
no conviertan el nado normal en falsos picos de ruido.

Cada mordida resta 8 de salud, con 1 s de intervalo por piraña. Un pulso
rojo indica daño. Al agotarse los 100 puntos de salud, se desactiva el
control de movimiento existente; **R** restaura al jugador en su posición
inicial y reinicia las pirañas, lampreas y pez linterna. Este reinicio es local al encuentro, no
reinicia comida consumida ni otros eventos del nivel.

## Cómo duplicar una piraña

1. Fuera de Play Mode, selecciona **Pirana_A_01** o **Pirana_A_02** (el
   objeto raíz de la piraña, no el hijo `Cuerpo`).
2. Pulsa **Ctrl+D**.
3. Mantén la copia como hija de **Zona A - Cardumen Piranas**.
4. Muévela a un punto libre dentro de la caja, separada de paredes y otras
   pirañas. Guarda la escena.

La copia encuentra al cardumen y su jugador automáticamente al activarse.
No hay listas de compañeras ni referencias de jugador que completar en
cada piraña. Su posición al entrar en Play será su centro de patrulla.

**Para duplicar el pez linterna o una lamprea**, selecciona
`Pez_Linterna_A_01` o `Lamprea_A_01`, pulsa **Ctrl+D** y conserva la copia
dentro de `Zona A - Cardumen Piranas`. Cada enemigo busca el objetivo común
al activarse. Se moverá desde la nueva posición; comprueba que tenga sitio
libre y que no nazca dentro de una pared.

También puedes arrastrar los prefabs `PezLinternaEsc2.prefab` y
`LampreaEsc2.prefab` desde `Assets/_Project/Prefabs/Enemies/` al cardumen.

También puedes arrastrar `Assets/_Project/Prefabs/Enemies/PiranaEsc2.prefab`
dentro del mismo cardumen. Una instancia fuera de un objeto con
`PiranhaSchool` no funciona porque carece de territorio y objetivo.

Para cambiar parámetros de todas, edita el prefab. Para una sola, modifica
su componente `Esc2Piranha` en la escena sin aplicar esos overrides al prefab.
Mantén la escala del objeto raíz y del cardumen en `(1,1,1)`; ajusta el
tamaño visual en el hijo `Cuerpo`.

## Configuración implementada

| Parámetro | Valor en esc2 |
| --- | --- |
| Detección | 5 m, dentro de zona y sin paredes interpuestas |
| Espera por proximidad | 2,5 s |
| Umbral de ruido | 6 m/s |
| Velocidad de patrulla / ataque | 1 / 9 m/s |
| Giro tranquilo / ataque | 5 / 0,5 |
| Radio de alerta a compañeras | 20 m, con línea libre entre peces |
| Tiempo sin detección para calmarse | 3 s |
| Daño / intervalo | 8 / 1 s por pez |
| Radio de contacto | 0,45 m |

Al abandonar el jugador la caja, se cancela la persecución. Los peces
permanecen dentro de la zona y vuelven a patrullar. Un barrido de esfera
frena su avance contra obstáculos y cambia su dirección. No hay búsqueda
de rutas: el encuentro se limita a esta cámara abierta, no a perseguir al
jugador por todo el laberinto.

`Obstacle Layers` usa las capas de raycast normales, excluyendo
`Ignore Raycast`; además se ignoran los colliders del jugador y del propio
cardumen. Las paredes existentes de la cueva conservan sus colliders y capas.

## Componentes

- `Esc2Piranha`: comportamiento adaptado del MVP, patrulla, ataque y mordida.
- `PiranhaSchool`: territorio, objetivo compartido y descubrimiento de integrantes.
- `PiranhaPlayerTarget`, añadido a `Player/KM_Player`: mide movimiento y
  recibe daño sin añadir un segundo controlador ni una cámara.

La salud de este encuentro es independiente de `EnergySystem`; comer no
restaura automáticamente las heridas. El HUD del encuentro muestra la salud
y el pulso rojo indica impactos. `On Health Changed` y `On Death`
permiten conectar posteriormente la presentación o el sistema global de
salud. La respuesta de muerte está conectada al `SimpleFlyCamera` que usa
esc2; al migrar a XR habrá que conectar también el bloqueo de esa locomoción.

## Comprobaciones

**Anadromo → Esc2 → Depredadores → Validar integración** ejecuta pruebas
aisladas de ruido, ataque y evasión del pez linterna, agarre de lamprea,
duplicación, alerta bloqueada por paredes, daño, límites y reinicio a
30/60/120 FPS. Resultado: `Logs/Esc2PiranhaValidation.txt`.

**Probar escena en Play Mode**, en el mismo menú, comprueba las dos pirañas,
el pez linterna, las dos lampreas, la locomoción original, el daño y el
reinicio en la escena guardada. Luego vuelve al editor.
Resultado: `Logs/Esc2PiranhaPlayCheck.txt`.

Las sacudidas se prueban con alternancia A/D o con movimiento horizontal
del ratón en esta versión de escritorio. Al migrar a VR, la entrada debe
conectarse al movimiento de brazos/controladores del jugador.

Antes de instalar se guardó una copia local de la escena en
`Library/Esc2PiranhaBackup/esc2-before-piranhas.unity`.

## Corrección de lampreas y pez linterna

Los tres enemigos habían quedado cerca de Z=3 mientras el territorio estaba
centrado en Z=20,5. Ahora están dentro de la zona A actual, en posiciones
libres de obstáculos. Las instalaciones nuevas usan posiciones locales.

Las lampreas ocupan lugares separados frente a la cámara y muestran una
indicación de sacudida en el HUD. Al desprenderlas se restaura la velocidad;
R conserva sus posiciones iniciales. El pez linterna apaga tanto la luz
como la esfera del señuelo. Su embestida respeta obstáculos y límites de
zona; el reinicio también lo devuelve a su posición original.

Para duplicar cualquiera: fuera de Play, selecciona su objeto raíz,
Ctrl+D y desplaza la copia dentro de los límites del mismo cardumen,
sin solaparla con rocas. No hay listas que registrar manualmente.

**Anadromo → Esc2 → Depredadores → Verificar lampreas y pez linterna**
abre la escena guardada en Play y verifica persecución y adhesión naturales,
drenaje, ralentización acumulada, desprendimiento mediante la función de
sacudida, apagado del señuelo, embestida, evasión y reinicio. Para aislar
los casos desactiva temporalmente las pirañas y coloca al jugador en la
zona; no fuerza el estado de adhesión ni altera velocidades de enemigos.
El resultado está en `Logs/Esc2CompanionCheck.txt`. El cableado de entrada
A/D también tiene una prueba específica descrita debajo. La entrada de ratón
no está incluida en esa prueba. Al terminar vuelve a la escena previa del editor.

## Referencias y colores en la jerarquía actual

`ZonaA` necesita el componente **PiranhaSchool**, con **Target = KM_Player**.
Los cinco enemigos deben seguir como hijos de ese objeto. Crear otra carpeta
y moverlos allí sin este componente los deja sin territorio ni objetivo.
El Inspector de cada enemigo muestra esas referencias y avisa si faltan.
El menú **Anadromo → Esc2 → Depredadores → Reparar referencias y colores**
reconecta el grupo y guarda los materiales sin recolocar los peces.

Colores base, visibles también fuera de Play: piraña verde, lamprea morada,
pez linterna azul grisáceo y señuelo verde luminoso. Amarillo/rojo indican
estados de alerta/ataque; el pez linterna se oscurece durante la preparación.

**Anadromo → Esc2 → Depredadores → Probar entrada W y sacudidas A D** mantiene
los cinco enemigos activos y el controlador original habilitado. Coloca al
jugador una vez en la entrada del encuentro; después utiliza eventos de teclado
del Input System para nadar con W y desprender las lampreas con A–D–A–D.
Comprueba desplazamiento real, persecución, adhesión, ralentización, daño,
embestida y recuperación de velocidad. Resultado: `Logs/Esc2InputCheck.txt`.
