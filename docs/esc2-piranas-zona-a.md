# Pirañas en esc2 — zona A

La escena `Assets/_Project/Scenes/esc2.unity` contiene dos pirañas de
primitivas 3D en la cámara de entrada A. No necesitan cargar el escenario MVP.

En la jerarquía:

```text
Zona A - Cardumen Piranas
  Pirana_A_01
    Cuerpo
  Pirana_A_02
    Cuerpo
  Pez_Linterna_A_01
  Lamprea_A_01
  Lamprea_A_02
```

El cardumen está centrado en `(0, -0.3, 1)` y ocupa una caja de
`6 × 2.6 × 7 m`. Selecciónalo para ver sus límites con Gizmos activos.
Las posiciones iniciales son `(-1.3, -0.3, 1.2)` y `(1.2, -0.2, 2.2)`.
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
tu nado un 15% cada una y drenan 5 puntos de salud cada 2 s. Cambiar A/D tres
veces en menos de 0,6 s o sacudir rápidamente el ratón las desprende.
Quedan aturdidas durante 3 s antes de poder volver a perseguirte.

Abre esc2 y pulsa Play. Avanza hacia las dos formas verdes. Al detectarte
cambian a amarillo; si permaneces cerca 2,5 s, atacan y se vuelven rojas.
Acelerar con Shift dentro del alcance provoca el ataque inmediato.

El nado normal de esta escena es 4 m/s y el rápido 8 m/s. El umbral de
ruido se fijó en 6 m/s. La velocidad se mide con el desplazamiento del
cuerpo en pasos de física, para que las diferencias entre FPS y física
no conviertan el nado normal en falsos picos de ruido.

Cada mordida resta 8 de salud, con 1 s de intervalo por piraña. Un pulso
rojo indica daño. Al agotarse los 100 puntos de salud, se desactiva el
control de movimiento existente; **R** restaura al jugador en su posición
inicial y reinicia las pirañas. Este reinicio es local al encuentro, no
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
de ruido, ataque y evasión del pez linterna, persecución y agarre de lamprea,
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
