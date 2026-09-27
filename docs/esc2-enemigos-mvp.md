# Esc2 · enemigos MVP

Escena: `src/anadromo/Assets/_Project/Scenes/Esc2_Enemigos_MVP.unity`.

En Unity: **Anadromo → Cueva MVP → Abrir escena**, luego **Play**. La escena
es independiente de `esc2`, y usa exclusivamente cubos, esferas y cilindros.
El menú de apertura permite guardar los cambios pendientes de la escena actual.

## Controles

| Acción | Control |
| --- | --- |
| Nadar / mirar | WASD / ratón |
| Bajar / subir | Q / E |
| Nadar rápido | Shift izquierdo |
| Encender o apagar linterna | F |
| Desprender lampreas | Alternar A/D en menos de 0,6 s o sacudir el ratón horizontalmente |
| Liberar / capturar cursor | Esc / clic izquierdo |
| Reiniciar, incluso tras morir o llegar a la salida | R |

La velocidad normal es 2,4 m/s; con Shift es 6 m/s. El ruido se calcula con
el desplazamiento real: empujar una pared no produce ruido de desplazamiento.
El HUD muestra salud, ruido, luz, penalización por lampreas, cavernas visitadas
y un mapa con la posición del jugador. Llegar al marcador de H termina la partida.

## Mecánicas y recorrido

- **Pirañas (B y E):** CHILL → DISTURBED al acercarse; atacan inmediatamente
  con ruido ≥ 3 m/s, o después de 2,5 s de proximidad. Alertan a otras pirañas
  del cardumen a 20 m. Embisten a 7 m/s con giro lento; contacto: 8 de daño
  por pez, con 1 s de intervalo. Pierden el ataque tras 3 s sin detección.
- **Pez linterna (C y G):** señuelo luminoso en reposo; lo apaga al detectar
  al jugador, espera 1,8 s y embiste en la dirección fijada al iniciar el ataque.
  La linterna, dentro del cono y sin paredes de por medio, cancela el ataque
  y mantiene apagado el señuelo. Contacto: 25 de daño; después se recupera.
- **Lampreas (D y F):** persiguen, se adhieren junto a la cámara y restan
  15 puntos porcentuales de velocidad cada una (mínimo: 25%). Cada lamprea
  drena 5 de salud cada 2 s. Agarre: 100; A/D alternado resta 35, una sacudida
  del ratón resta 25. Al soltarse quedan aturdidas 3 s, restauran la velocidad
  y después recuperan el agarre. La lista admite desprender varias a la vez.
- **Tiburones (B–F y E–G):** entrar cerca del recorrido activa un aviso de
  3 s. Recorren el túnel a 15 m/s sin perseguir al jugador; contacto letal.
  Es posible apartarse del eje o cambiar de altura. El paso puede repetirse
  después de 18 s. Al terminar la ruta desaparecen.

Las detecciones comprueban paredes y los enemigos móviles no atraviesan la
geometría. El daño usa un barrido entre posiciones para detectar contactos
incluso si un enemigo rápido cruza al jugador entre dos frames.

## Alcance del mapa

Ocho salas A–H, diez conexiones (tres circuitos independientes) y tres ramales
sin salida, inspirados en `cavern-map.jpg`. Es una adaptación para probar
mecánicas: las salas están a una misma cota, con seis metros de altura,
en lugar de reproducir las profundidades y dimensiones del dibujo.
El jugador puede nadar verticalmente. La entrada es segura.

Este MVP usa teclado y ratón; no conecta todavía la locomoción XR ni el
sistema de energía del escenario principal. La salud pertenece al MVP.
Los sonidos marcados TODO en el pseudocódigo quedan pendientes.

## Edición y comprobaciones

Los componentes están en `Assets/_Project/Scripts/CavernMVP/`, bajo el
namespace `Anadromo.CavernMVP`. Los enemigos y sus referencias están guardados
en la escena; sus valores públicos se pueden ajustar en el Inspector.
`CavernMvpWorld` define las salas, conexiones, construcción con primitivas,
encuentros, HUD y reinicio. El reinicio reconstruye los valores de ese generador;
para cambios persistentes entre reinicios, ajustar también `Build()`.

**Anadromo → Cueva MVP → Validar mecánicas** ejecuta comprobaciones de
conectividad, estados, oclusión, daño, agarre y recorridos, incluyendo
30/60/120 FPS. Resultado en `Logs/CavernMvpValidation.txt`.

**Anadromo → Cueva MVP → Probar escena en Play Mode** prueba la escena
guardada, referencias, materiales, colisiones del jugador, muerte, sacudidas,
reinicio y salida; sale automáticamente de Play Mode y restaura la escena de
arranque previa. Guarda resultado y captura en `Logs/CavernMvpPlayCheck.txt`
y `Logs/CavernMvpPlay.png`. Estos archivos locales no se versionan.

La escena se crea automáticamente si falta después de importar los scripts;
también existe el menú **Crear escena si falta**. No reemplaza una escena MVP
existente ni guarda la escena abierta del usuario.
