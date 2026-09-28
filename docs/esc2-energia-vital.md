# Energía vital en esc2

Implementación basada en [GDD_Mecanica_Energia_VR.md](GDD_Mecanica_Energia_VR.md).
El jugador ya no tiene depósitos separados de vida y comida. `EnergySystem`
es la única fuente de energía; `PlayerEnergyController` conecta los ataques,
la alimentación, las penalizaciones y la presentación visual.

## Reglas

- Máximo: 100. Desgaste normal: 1 por segundo; nadar con Shift: 3 por segundo.
- Piraña: resta 8. Pez linterna: 25. Lamprea: 5 cada 2 segundos, además de
  su ralentización de 15% por ejemplar. El tiburón agota la energía al contacto.
- Un choque físico con velocidad relativa mínima de 1,5 m/s resta 12;
  hay 0,8 s entre impactos. Los triggers de comida no causan daño.
- Comer krill usa su `Prey.energyValue` existente. No se inventaría otro recurso.
- Bajo 30%, se reduce progresivamente la velocidad de nado y la respuesta
  de giro del controlador de escritorio. La corriente externa tiene más efecto.
  Los multiplicadores se combinan con la ralentización de las lampreas.
- Al llegar a cero, se detiene el control de movimiento de esc2. La comida
  no resucita al jugador. R reinicia energía, efectos y enemigos.

## Presentación

No hay barra de vida ni barra de energía. `Show Debug Status` está desactivado
en el jugador; solo sirve para mostrar un número durante depuración.

- Cansancio: viñeta azul abisal/negra y desaturación proporcionales a la
  energía perdida. Intensidad oscura máxima inicial: 0,48.
- Impacto: pulso blanco en los bordes durante 0,35 s. Después permanece
  el agotamiento correspondiente al nuevo valor de energía.
- Krill: pulso dorado de 0,45 s; la viñeta se abre y vuelve el color según
  la energía recuperada, sin fingir que una comida siempre llena el recurso.

`EnergyVisualFeedback` crea su propio Volume/Profile temporal y habilita el
postprocesado de la cámara. No modifica los perfiles de iluminación guardados.
No desplaza ni sacude la cámara ni altera el seguimiento de la cabeza.
Las instrucciones de teclado de lampreas/reinicio se ocultan si hay un visor XR.
Las señales de enemigos del prototipo siguen siendo independientes del estado
energético; no se ha implementado audio ni háptica nuevos.

## Código y compatibilidad

`PiranhaPlayerTarget.Health`, `maxHealth` y sus eventos se conservan como
adaptadores de las referencias existentes: leen la energía, no guardan vida.
`DamageVignetteController` conserva su nombre para escenas antiguas, pero
utiliza el nuevo feedback energético. El escenario histórico MVP conserva
su interfaz de depuración de mapa/objetivos; la integración de krill comprobada
es la de `PlayerFeeding` en la escena final esc2.

Los parámetros se editan en `EnergySystem`, `PlayerEnergyController` y
`EnergyVisualFeedback` de `KM_Player`. La respuesta física comprobada es la
del controlador de escritorio; el ajuste de brazadas y la comodidad visual
deben verificarse en el visor con la locomoción VR antes de darla por validada.

## Verificación

Menú **Anadromo → Esc2 → Energia → Verificar ciclo de energia en Play**.
Prueba desgaste pasivo, ataques, viñeta/desaturación, movimiento real con W,
consumo de krill por trigger, recuperación, agotamiento y reinicio con R.
Resultado: `Logs/Esc2EnergyCheck.txt`.

Las pruebas aisladas anteriores de enemigos pausan el desgaste temporal para
mantener sus cantidades deterministas. La nueva prueba comprueba expresamente
el desgaste antes de pausarlo para los casos de impactos y comida.
