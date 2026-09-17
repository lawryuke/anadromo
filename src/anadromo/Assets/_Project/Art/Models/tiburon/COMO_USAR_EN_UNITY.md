# Megalodón: ascenso vertical pregrabado

El modelo nada verticalmente con la cabeza hacia arriba y asciende 10 metros en 6 segundos. Se ha conservado y remuestreado el movimiento de huesos del modelo original. La longitud del tiburón se ha ajustado aproximadamente a 6 metros como escala inicial editable.

- `Megalodon_Ascenso.fbx`: malla, esqueleto, texturas incrustadas y animación horneada a 30 fps.
- `Megalodon_Ascenso.blend`: archivo editable, fotogramas 1–181.
- `Textures`: imágenes separadas para configurar materiales en Unity.
- `AscensoRoot`: hueso que contiene el recorrido vertical completo.

## Importar y reproducir

1. Copia el FBX y la carpeta Textures dentro de Assets en tu proyecto.
2. Selecciona el FBX. En Rig, elige **Animation Type: Generic**, **Avatar Definition: Create From This Model** y **Root node: AscensoRoot**. Aplica los cambios.
3. En Animation, habilita **Import Animation**. Usa el clip completo de 6 segundos y renómbralo `Ascenso_Vertical_6s` si aparece con un prefijo. Desactiva **Loop Time** y **Loop Pose**: el recorrido es de una sola pasada.
4. En las opciones de movimiento, selecciona **AscensoRoot** como **Root Motion Node** si la versión de Unity presenta ese selector. Si aparecen las opciones Root Transform, deja **Root Transform Position (Y) → Bake Into Pose desactivado** para que la altura se aplique al objeto, y usa **Based Upon: Original**. Los controles visibles pueden cambiar al escoger un nodo explícito.
5. Crea un Animator Controller y arrastra el clip como estado predeterminado. Asigna ese controller al Animator del tiburón.
6. Activa **Apply Root Motion** en el Animator. Mantén la escala del objeto en (1, 1, 1) y la velocidad del Animator en 1 para obtener 10 metros en 6 segundos. Para que comience debajo de la superficie, coloca el objeto en la profundidad deseada: el recorrido es relativo a su posición inicial.
7. Reproduce la escena. El tiburón sube por el eje Y de Unity; en Blender el eje vertical es Z. Al finalizar, el clip no debe reiniciarse.

## Materiales y comprobación

El FBX incluye las imágenes, pero el shader de Blender no se traslada idénticamente a Unity. Si hace falta, usa Extract Materials / Extract Textures y asigna body_baseColor al color base y body_normal como Normal Map, usando el shader de tu proyecto (Built-in, URP o HDRP).

La animación y el desplazamiento se comprobaron en Blender y mediante reimportación del FBX. No se ha integrado ni probado dentro de tu proyecto Unity. Si el tiburón nada en el sitio, revisa Apply Root Motion, AscensoRoot y que la altura no esté horneada dentro de la pose. Un controlador de movimiento o Rigidbody que escriba la posición puede interferir con el recorrido.

El archivo original permanece intacto. La copia contiene los 105 huesos originales y un hueso nuevo para el ascenso. No se necesita programar la trayectoria para reproducir este clip.

Documentación oficial: [Root Motion y Generic](https://docs.unity3d.com/6000.0/Documentation/Manual/RootMotion.html), [importación de animaciones Generic](https://docs.unity3d.com/6000.0/Documentation/Manual/GenericAnimations.html).
