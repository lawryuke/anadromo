# Entorno OceanViz en Anádromo

Abre `src/anadromo/Assets/_Project/Scenes/Act1_OpenOcean.unity` y pulsa Play. Es la escena de inicio ya configurada en el proyecto.

La escena conserva la alimentación, la energía y el cardumen del Blockout. Incorpora los tres terrenos del mapa Mediterráneo de `src/OceanViz/OceanViz3`, su superficie de agua, cáusticas, iluminación, partículas de rayos y VFX de residuos. El reflejo usa una sonda en tiempo real. El entorno está desplazado a (82, 65, 50), con la superficie a Y=65 y el jugador inicialmente a Y=55.

VR está temporalmente desactivado en Act1_OpenOcean y Blockout_Test: inicio automático de XR, seguimiento del visor, XROrigin, acciones XR, nado con mandos y renderizado XR. Los paquetes y componentes se conservan para poder reactivarlos. `DebugVuelo` está habilitado y controla la cámara con **WASD/flechas**, **Q/E** para bajar/subir y **botón derecho + movimiento del ratón** para mirar. Haz clic en la pestaña Game para darle foco al teclado.

`PezCardumen.prefab` utiliza la geometría original de Sea Bass (lubina), sus texturas y FishAdvancedShaderGraph. `OceanFishAnimation` proporciona los parámetros de animación de natación a cada pez. El modelo está convertido a una malla Unity para que también pueda verse en el editor, sin cargar un GLB por cada ejemplar.

`OceanEnvironment` aplica los parámetros de corriente del Mediterráneo mediante NoiseTextureManager y WaterCurrentUtility. Los peces reciben la corriente completa; el jugador recibe un 15%, configurable en el Inspector. El movimiento de escritorio y el nado XR utilizan el mismo campo de ruido. Las corrientes son el sistema procedural de OceanViz, no una simulación de fluidos volumétrica.

Validación: menú **Anadromo > Validate Ocean Environment**. Abre una escena de vista previa sin cambiar las escenas abiertas y escribe el resultado y una captura en `tools/validation/ocean-environment.txt` y `tools/validation/ocean-environment.png`. Comprueba terrenos, colisiones, agua, reflejos, partículas, referencias, geometría de peces, shader y muestreo de corrientes.

Los recursos originales siguen en sus carpetas importadas y la escena Blockout_Test se conserva. Créditos y licencias de OceanViz están en `src/anadromo/ThirdParty`.

Los terrenos antiguos `New Terrain.asset` y `New Terrain 1.asset` procedían de Unity 6000.5 y no se podían cargar en 6000.3. Se reconstruyeron con 6000.3 a partir de sus alturas, huecos, dimensiones y ajustes, conservando sus GUID. La comparación completa de alturas dio error máximo 0 y los huecos coinciden. No tenían capas pintadas, árboles ni detalles. Los originales están en `tools/desktop-backup`.

Se retiraron de los ajustes globales de URP seis recursos de versiones posteriores que no existen en URP 17.3. El inicio de escena espera a que termine la importación. La compilación y la validación del entorno pasaron. La prueba en Play Mode verificó avance con W, ascenso con E, giro con ratón y que el cargador XR permanece detenido, sin errores nuevos durante la prueba. Los resultados están en `tools/validation/ocean-environment.txt` y `tools/validation/desktop-play.txt`.
