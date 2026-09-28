# GDD - Mecánica: Sistema de Energía Vital y Feedback Visual (PC-VR)
**Proyecto:** Anádromo (Juego de natación contra la corriente en PC-VR)

## 1. Concepto Principal
El juego no utilizará barras de "Vida" y "Comida" separadas. En su lugar, emplea un sistema unificado llamado **"Energía Vital"**. Dado que la experiencia es PC-VR, el HUD tradicional (barras flotantes) está prohibido para mantener la inmersión. Toda la información sobre el estado del jugador se comunicará a través de efectos de post-procesado (Post-Processing) y viñetas (Vignettes) en la cámara del jugador.

## 2. Lógica de la Energía Vital (Stats)
El jugador tiene un único valor interno de Energía (ej. 0 a 100).
* **Drenaje Constante (Cansancio):** La energía se agota gradualmente con el paso del tiempo, simulando el esfuerzo físico de nadar contra la corriente del río.
* **Drenaje Inmediato (Daño):** Chocar contra obstáculos (rocas) o recibir ataques de depredadores resta un porcentaje alto de energía de golpe.
* **Recuperación (Krill):** El único recurso consumible del juego es el Krill. Comer krill restaura la energía.

## 3. Efectos Físicos en el Gameplay
La cantidad de energía afecta físicamente al avatar del jugador para generar tensión:
* Cuando la Energía Vital cae por debajo de un umbral crítico (ej. < 30%), el jugador sufre penalizaciones físicas:
  * Reducción de la velocidad de nado.
  * Rotación más pesada o lenta.
  * Mayor susceptibilidad a ser arrastrado hacia atrás por la corriente.

## 4. Sistema de Retroalimentación Visual (VR Vignette)
El estado de la Energía Vital se comunica al jugador mediante tres estímulos visuales específicos en la cámara VR:

### A. Estado de Agotamiento (Hambre/Esfuerzo)
* **Efecto:** Viñeta oscura (Negro o Azul Abisal) combinada con desaturación progresiva de la imagen (escala de grises).
* **Comportamiento:** A medida que la energía baja del 100% al 0%, la viñeta se cierra lentamente generando "visión de túnel" y el entorno pierde sus colores vivos. Simula la pérdida de consciencia y evita mareos en VR cuando el jugador se mueve lento.

### B. Estado de Daño (Impacto)
* **Efecto:** Destello rápido (Flash) de color Rojo o Blanco puro en los bordes de la visión.
* **Comportamiento:** Dura aprox. 0.2 a 0.5 segundos al momento del impacto. Inmediatamente después del destello, la viñeta de agotamiento (Negra) avanza bruscamente para reflejar la pérdida sustancial de energía, indicando que el golpe fue grave.

### C. Estado de Recuperación (Consumo de Krill)
* **Efecto:** Destello sutil de luz cálida (Naranja o Dorado bioluminiscente) y recuperación de la visión.
* **Comportamiento:** Al comer krill, la viñeta oscura retrocede instantáneamente y el mundo recupera su saturación de color original. El sutil flash cálido refuerza psicológicamente que el krill es nutrición y alivio.

## 5. Requisitos de Implementación (Notas para el Programador/IA)
* **Scripting:** Se requiere un script `PlayerEnergyController` que maneje las variables de energía, el `Update` para el drenaje constante, y métodos públicos `TakeDamage(float amount)` y `ConsumeKrill(float amount)`.
* **Visuales:** Se recomienda integrar esto a través del sistema de **Post-Processing Volume** de Unity (usando Vignette y Color Grading/Color Adjustments) o mediante un **Shader de pantalla personalizado** para VR que controle la intensidad de la viñeta, el color de los destellos y la saturación de la imagen en base al valor actual de la Energía Vital.
