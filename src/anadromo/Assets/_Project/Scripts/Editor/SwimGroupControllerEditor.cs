using Anadromo.Mechanics;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SwimGroupController))]
public class SwimGroupControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var controller = (SwimGroupController)target;
        EditorGUILayout.HelpBox("Controla las copias del Box Object Spawner durante Play. " +
            "Movement Target dirige el nado; Target Object del spawner solo orienta al generar. " +
            "Fish gira con agilidad; Shark mantiene avance y circula al permanecer en una zona. " +
            "La dirección de nado es +Z y se aplica la corrección del modelo configurada en el spawner. Los Rigidbody de las raíces se controlan como cinemáticos mientras el controlador está activo. " +
            "Collide With Obstacles barre una esfera antes de avanzar y permite deslizarse por paredes. " +
            "El radio automático es conservador. Los solapamientos iniciales se corrigen antes de avanzar; un paso más estrecho que el volumen seguirá bloqueado.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.LabelField("Integrantes registrados", controller.MemberCount.ToString());
            EditorGUILayout.LabelField("Último obstáculo", controller.LastCollisionObstacle ?? "Ninguno");
            EditorGUILayout.LabelField("Solapamientos corregidos", controller.RecoveredOverlaps.ToString());
            if (controller.useFishStartSequence && controller.swimStyle == SwimGroupController.SwimStyle.Fish)
            {
                EditorGUILayout.LabelField("Inicio de peces", controller.FishStartStatus);
                if (GUILayout.Button("Iniciar oscilación de peces")) controller.StartFishSequence();
            }
            using (new EditorGUI.DisabledScope(controller.movementTarget == null))
                if (GUILayout.Button("Nadar hacia Movement Target"))
                    controller.GoToTarget(controller.movementTarget);
            if (GUILayout.Button("Volver a nado normal")) controller.ReturnToNormal();
        }
    }
}
