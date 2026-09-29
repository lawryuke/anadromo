# Integración de VR_Player en esc2

Validado el 29 de septiembre de 2026 con Unity 6000.3.10f1, en la escena real y mediante Play Mode. Se conservaron los cambios que ya tenía la escena antes de esta intervención.

## Configuración

`VR_Player` incorpora los mismos componentes de energía vital, daño, respuesta visual y detección de movimiento de `KM_Player`. Conserva el nado por aleteo y el giro por cabeza existentes. `KM_Player` permanece desactivado.

El collider sólido sigue al visor y es independiente del trigger de alimentación. Los enemigos y las zonas de retorno usan la posición del visor; no atacan el punto del suelo que representa el origen del rig. El cansancio y las lampreas reducen el impulso de nado. Al morir se detiene la locomoción, manteniendo el seguimiento del visor.

- Sacudir las manos libera las lampreas progresivamente. También se leen las posiciones de mandos XR como alternativa para este gesto.
- Tras morir, acercar ambas manos a menos de 45 cm del visor durante 1,5 segundos reinicia la escena. Aparece una instrucción y progreso dentro de VR.
- El reinicio completo restaura presas, enemigos, energía y trampas de un solo uso. `esc2` se añadió a las escenas habilitadas del build para permitir su recarga, sin modificar cuál escena se ejecuta primero.
- Los tres generadores de kril producen presas con `Food_PlayerOnly`; comerlas restaura energía.
- La viñeta de confort y la respuesta visual de energía se combinan. El ataque cinematográfico del tiburón no fuerza la orientación del rig VR.

## Auditoría de la escena

Sin scripts faltantes ni referencias externas al `KM_Player` inactivo. Se inspeccionaron también los componentes de las instancias de prefab y objetos inactivos.

| Sistema | Cantidad |
|---|---:|
| Territorios de enemigos | 10 |
| Pirañas | 14 |
| Peces linterna | 7 |
| Lampreas | 7 |
| Peces ciegos | 6 |
| Pasos de tiburón | 4 |
| Tiburones de patrulla | 3 |
| Sistemas antirretorno con medusa/tiburón | 7 |
| ZoneLimit | 25 |
| Generadores de comida | 3 |
| Guías LuzViajera | 2 |

Las guías y avisos de medusa siguen sus rutas existentes. No hay un `LevelManager` ni cardúmenes aliados `SwimGroupController` en esta escena; esos sistemas pertenecen a otras escenas y no se añadieron artificialmente.

## Validación reproducible

Con `esc2` abierta y fuera de Play Mode:

1. `Tools > Anadromo > VR > Validate esc2 player`: inspección de componentes, referencias, prefabs y scripts faltantes.
2. `Tools > Anadromo > VR > Run esc2 Play Mode checks`: pruebas sobre instancias de la escena, descartando las alteraciones temporales al finalizar.

Las pruebas pasaron: población de kril; alimentación por trigger físico; recuperación; impulso por aleteo y fatiga; mordida de piraña; agarre, drenaje y liberación de lamprea; ataque de pez linterna; agitación y ataque de pez ciego; contacto letal de patrulla; activación de paso de tiburón; checkout, advertencia y zona letal antirretorno; muerte; instrucciones VR; daño por colisión física; recarga y restauración de las siete trampas.

El test desplaza deliberadamente el visor respecto al origen para detectar ataques y triggers mal posicionados. Inyecta eventos de aleteo y estados de ruido: **no sustituye una prueba humana de tracking, comodidad ni gestos en Quest**. Tampoco se generó ni instaló un APK en esta intervención.

Resultados conservados en [VR-Player-PlayMode-results.txt](VR-Player-PlayMode-results.txt). Al repetir las pruebas, los nuevos resultados se escriben en `Logs/VRPlayerPlayCheck.txt` y `Logs/VRPlayerSceneAudit.txt`.
