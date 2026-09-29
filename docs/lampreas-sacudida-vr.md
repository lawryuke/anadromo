# Lampreas: sacudida en Oculus

La escena `Assets/_Project/Scenes/esc2.unity` incorpora el rig de nado de
`TerrainTestVisuales` en `Player > XR Origin (VR)`. Se conserva el componente
`PiranhaPlayerTarget` y sus IDs: los territorios y enemigos siguen apuntando al
mismo jugador. Cámara, alimentación, energía, aletas y seguimiento XR están
conectados a este rig. F1 permite alternar con el control de escritorio existente.

## Mecánica

- Al adherirse la primera lamprea se detienen el avance, las corrientes virtuales
  y los giros de locomoción. El Rigidbody queda temporalmente cinemático. El visor
  mantiene el seguimiento físico de la cabeza y las manos.
- La vista oscila en roll ±2,5° a 0,45 Hz, alrededor de los ojos. Este efecto
  modifica `Camera Offset`, sin sobrescribir la pose del visor.
- Sacudir las manos rápidamente en sentidos alternos reduce el agarre. Se leen
  posiciones crudas de muñeca en espacio de tracking; el balanceo visual y los
  giros virtuales no generan progreso. No hace falta sacudir la cabeza.
- Cada cambio válido resta 25 de agarre. Con el agarre normal de 100 hacen falta
  cuatro cambios válidos, después de iniciar el primer desplazamiento. El umbral
  inicial es 8 cm y 0,35 m/s, con un máximo de 0,65 s entre desplazamientos.
- Hay 6 s desde el primer agarre. Otras lampreas no reinician ese plazo; la misma
  sacudida afecta a todas. Se mantiene el drenaje de energía ya existente.
- Al desprenderse la última lamprea se restauran el modo de movimiento previo y
  la pose visual. Las lampreas pasan a `Stunned` mediante su lógica existente.
- Agotar el plazo consume la energía restante y dispara `onDeath`. La locomoción
  queda bloqueada hasta reintentar; desprender lampreas después de morir no revive.

Los parámetros están en `LampreyShakeController`, guardados en el rig de `esc2`.
El detector ignora movimiento lento, desplazamiento en una sola dirección,
pequeñas vibraciones, saltos de tracking y el primer dato tras recuperar tracking.
El aleteo deja de emitir impulsos durante el agarre y se reinicia al recuperar el
control para no acumular un impulso pendiente.

## Controles y comprobación

- Oculus: sacudidas de manos; pantalla de derrota con reinicio al juntar las
  manos durante 2 s o pulsar A/X. También hay lectura de posiciones de mandos
  cuando no está disponible el subsistema de manos.
- Escritorio: F1 activa el vuelo de prueba, A/D alternados o movimientos opuestos
  del ratón permiten escapar; R reinicia tras morir.
- Menú Unity: `Anadromo > Esc2 > Lampreas > Verificar sacudida y rig Oculus`.
  La comprobación usa el rig y las lampreas guardados en `esc2`, aísla los otros
  ataques durante la prueba y revisa bloqueo, liberación, varias lampreas,
  derrota, restauración y reconocimiento de gestos con muestras deterministas.
  Escribe el resultado en `Logs/LampreyShakeCheck.txt`.

La prueba automatizada no sustituye la calibración de velocidad y comodidad con
el visor real. No se incorpora el antiguo `LampreyAttackManager` de la rama
`shake`: se aprovecha su idea de agarre cronometrado y balanceo, integrada con la
energía y las lampreas actuales.
