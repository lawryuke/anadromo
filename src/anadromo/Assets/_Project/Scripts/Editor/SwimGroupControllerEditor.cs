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
            "El frente es +Z. Los Rigidbody de las raíces se controlan como cinemáticos mientras el controlador está activo.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.LabelField("Integrantes registrados", controller.MemberCount.ToString());
            using (new EditorGUI.DisabledScope(controller.movementTarget == null))
                if (GUILayout.Button("Nadar hacia Movement Target"))
                    controller.GoToTarget(controller.movementTarget);
            if (GUILayout.Button("Volver a nado normal")) controller.ReturnToNormal();
        }
    }
}
