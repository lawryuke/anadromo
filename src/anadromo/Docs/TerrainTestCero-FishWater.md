# Percepcion submarina de TerrainTestCero

La escena guarda `FishWaterExperience` en `OceanViz Water - TerrainTestCero`, con referencias explicitas a Main Camera, WaterSurface y sus materiales. Se activa al entrar en Play. Los objetos auxiliares y el Volume se crean en memoria y se eliminan al desactivar el componente; se restauran los ajustes ambientales anteriores.

- Alcance nominal: 10 m. Niebla exponencial cuadratica con aproximadamente 10% de contraste residual a esa distancia; sin recorte brusco del plano lejano. Es niebla de materiales URP, no scattering volumetrico fisico.
- Profundidad: distancia vertical hasta WaterSurface. La superficie actual esta a Y=30.7 y la camara inicial cerca de Y=6. El filtro reduce el canal rojo al 12% a partir de 10 m y atenua luz/color con la profundidad. Estos valores son direccion artistica ajustable, no una simulacion biologica.
- Corrientes: ruido Simplex 3D continuo, suavizado temporal y deriva limitada a 0.3 m/s. Hay zonas junto al arrecife y entrada del tunel, y refugios en la plataforma inicial y lateral rocoso. Seleccionar el componente muestra sus radios. Los refugios reducen el empuje; no se calcula una simulacion hidrodinamica de Karman ni ahorro metabolico.
- Turbulencia: hasta 180 microburbujas cercanas, refraccion local de la textura opaca y pulsos hapticos irregulares. No se modifica la rotacion de la cabeza ni la proyeccion XR.
- Presas: hasta 64 destellos sobre componentes `Prey`, dentro de 10 m y un cono frontal de 45 grados. Respetan la profundidad de la geometria opaca. Es una ayuda de contraste, no vision UV real.
- Snell: plano bajo la superficie con apertura aproximada de 96.6 grados y reflexion del probe fuera de ella. No es reflexion planar ni refraccion fisica completa. Las cuevas opacas lo ocluyen.

## Prueba

Abrir TerrainTestCero y entrar en Play. Avanzar desde (10,6,0) hacia (10,6,8), luego hacia (12,4,23); buscar el refugio (17,4,19). Comprobar que el empuje y las burbujas aumentan gradualmente y disminuyen en el refugio. Mirar hacia la superficie desde una zona abierta. Acercarse a una presa para ver el destello frontal.

`Anadromo > Validate TerrainTestCero Fish Water` comprueba conexiones, zonas calmas, refugio superpuesto, corriente activa, limites de velocidad, ruido y compilacion de materiales. Escribe el resultado en `Temp/fish-water-validation.txt`.

La escena mantiene su controlador de escritorio. Para usar un rig XR, asignar su camara a `viewer` y este componente al campo `waterExperience` de `SwimLocomotion`, usando un solo controlador de locomocion. El componente envia impulsos solamente a dispositivos XR con soporte haptico. No se fuerza un FOV panoramico sobre las lentes del visor.

Validado aqui: compilacion C# de runtime y validador con el compilador y referencias locales de Unity 6000.3.10f1. Pendiente: ejecutar el validador dentro del editor, revision visual en Play y prueba de rendimiento/comodidad/haptica en Quest.
