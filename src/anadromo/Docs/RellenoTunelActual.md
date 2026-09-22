# Relleno del túnel actual — TerrainTestVisuales

Se añadió `Relleno_Entre_Tunel_Curvo_Y_Terreno` a la escena, con el componente
`TunnelGapInfill`. Lee el contorno real del submesh interior del LOD0 original
y los límites de los cuatro BoxCollider existentes. No reemplaza la malla,
no altera la trayectoria, no mueve las rocas y no desactiva superficies.

El relleno ocupa el espacio entre ese contorno y la abertura rectangular.
Los labios curvos de entrada/salida se prolongan 3.5 cm dentro del terreno
para ocultar la junta. La pared interior del acantilado sigue visible:
la piel coincidente del relleno se utiliza únicamente en su collider cerrado,
evitando superficies coplanares y parpadeo. El paso permanece abierto.

El generador está adaptado a esta escena: la abertura está alineada con Z
mundial y el extremo posterior del submesh se encuentra en su Z local mínima.
Si se reorganizan los objetos o se gira todo el conjunto hay que adaptar esa
convención; no es un reparador booleano genérico de cualquier malla.

Material interior: `TerrainTestVisuales_TunnelInterior.mat`, con el nuevo shader
`Anadromo/Tunnel Continuous Rock`. Usa color mineral, vetas, grano y relieve
procedurales en coordenadas mundiales. Se mantiene continuo entre las paredes
y el relleno sin estirar las islas de un atlas UV. Responde a la iluminación
PBR de URP; suavidad 0.27, fuerza del relieve 0.65. Los mapas originales siguen
referenciados en el material, pero este shader utiliza su textura procedural.
La fachada rocosa conserva su material. En la actualización posterior se
extendió el acabado a techo, suelo y laterales, y se reparó el submesh interior;
ver `AcabadoRocosoContinuo.md` para los valores y el comportamiento actuales.

El relleno y su collider se generan al cargar la escena, tanto en edición
como en ejecución. Se liberan al desactivar su componente. No se guardan como
reemplazos de los assets originales y no se modificaron los scripts anteriores.

Comprobaciones realizadas: compilación C# con bibliotecas locales de Unity;
encaje del contorno dentro de la abertura rectangular; colliders originales
sin cambios; assets de malla y prefab originales sin cambios. La abertura
curva ocupa aproximadamente X 11.525–12.965 e Y 2.589–3.870; cabe dentro del
hueco original X 11.400–13.050 e Y 2.480–3.960.

Pendiente: importación/compilación GPU del shader y revisión visual en Unity,
incluidas las uniones con el frente rocoso irregular y las transiciones de LOD.
