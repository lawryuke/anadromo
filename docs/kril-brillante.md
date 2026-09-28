# Kril brillante

Prefab: `Assets/_Project/Prefabs/KrilBrillante.prefab`.
Arrástralo de Project a la escena; Ctrl+D duplica una instancia.

La raíz conserva `Prey` (+15 de energía), `Krill` (flotación lenta), un
`SphereCollider` trigger de 0,18 m y el tag `Food_PlayerOnly`. El jugador actual
acepta ese tag. `Food_Krill` se usa en otros grupos para depredadores y no figura
entre las comidas aceptadas por `PlayerFeeding` en esc2.

`Visual` contiene dos esferas: cuerpo naranja y núcleo blanco cálido. No tienen
colliders. Usan los materiales `Kril-NaranjaGlow` y `Kril-BlancoGlow`, con el shader
`Anadromo/Medusa Glow`. Cada renderer lleva `DistantLureGlow`, reutilizado como
control de emisión y distancia; no añade comportamiento de enemigo.

En ese componente, `Emission` controla el color HDR/brillo y `Water Settings`
apunta a `Esc2-WaterDepthY`. Respeta sus **Luminous objects fade start/end**, ahora
12/18 m. Brilla sin Point Light y sin revelar terreno. Las paredes opacas lo ocultan.

## Sustituir las esferas por un modelo

1. Abre el prefab con doble clic en Project.
2. Arrastra tu modelo como hijo de `Visual`, a posición local cero. Ajusta su
   rotación y escala, sin escalar la raíz para conservar el radio de alimentación.
3. Desactiva o elimina `Cuerpo_MVP` y `Nucleo_MVP`.
4. Para conservar exactamente este brillo, asigna `Kril-NaranjaGlow` o
   `Kril-BlancoGlow` a los renderers del modelo. Añade `DistantLureGlow` a cada
   objeto con `MeshRenderer` o `SkinnedMeshRenderer`, asigna `Esc2-WaterDepthY`
   en Water Settings y el color HDR deseado en Emission.
5. Conserva la raíz con `Prey`, `Krill`, el tag y el collider. Quita los colliders
   del modelo importado; ajusta solamente el collider de la raíz si hace falta.
6. Guarda el prefab. Las instancias sin modificaciones locales heredan el modelo.

Estos materiales ofrecen emisión uniforme, sin texturas ni sombreado del modelo.
Si quieres conservar las texturas del asset, usa un material **URP/Lit** con
Emission y su mapa de emisión. En ese caso no añadas `DistantLureGlow`: el material
se dibuja como objeto opaco y usa la visibilidad normal del agua. Conservar a la
vez sus texturas y la visibilidad luminosa requiere adaptar su shader.

## Movimiento y grupos

Activa `Krill > Is Static` para dejarlo quieto. Si se coloca bajo un grupo que lo
controla mediante `SwimGroupController`, el movimiento del grupo tiene prioridad.
En `BoxObjectSpawner`, puedes asignar este prefab como `Source Object`; revisa
`Spawned Tag`, porque el spawner reemplaza el tag del prefab. No se cambiaron los
spawners ni los kriles existentes de esc2.

## Medusa: activación por zona existente

En esc2, `AntiBacktrackTunnel` usa `ZoneLimit`, no un Box Collider físico. Primero
el jugador cruza **Checkout Zone**; al volver a **Warning Zone**, el componente
llama `BeginRoute()` de la medusa asignada en **Medusa Warning**. **Kill Zone** es
la zona de castigo posterior; no hace falta entrar en ella para activar la medusa.
`ZoneLimit > Center/Size` define la caja y no requiere Is Trigger ni Rigidbody.
La medusa debe tener sus Waypoints y esperar inicialmente (`GameSettings >
Jellyfish Waits For Abysm`, que tiene prioridad sobre `Wait Abysm Phase`).
Ese sistema es una advertencia al regresar por el túnel, no un disparador libre
de primera entrada. Añadir solo un Box Collider no ejecuta `BeginRoute()`.
