using Anadromo.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(BoxObjectSpawner))]
public class BoxObjectSpawnerEditor : Editor
{
    private readonly UnityEditor.IMGUI.Controls.BoxBoundsHandle box =
        new UnityEditor.IMGUI.Controls.BoxBoundsHandle();

    public override void OnInspectorGUI()
    {
        if (target == null) return;
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Arrastra el objeto de Hierarchy a Source Object. La caja no necesita Collider. " +
            "En Scene: W mueve el generador; R permite ajustar las caras de la caja. " +
            "Los MeshCollider no convexos se comprueban mediante sus bounds (más conservador). " +
            "La separación se comprueba solo al generar.", MessageType.Info);
        var spawner = (BoxObjectSpawner)target;
        if (spawner != null && spawner.GetComponent<SwimGroupController>() == null && !EditorUtility.IsPersistent(spawner))
            if (GUILayout.Button("Añadir controlador de nado al grupo"))
                Undo.AddComponent<SwimGroupController>(spawner.gameObject);
        if (!string.IsNullOrEmpty(spawner.LastGenerationMessage))
            EditorGUILayout.HelpBox(spawner.LastGenerationMessage, MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorUtility.IsPersistent(spawner)))
        {
            if (GUILayout.Button("Generar / regenerar")) Run(spawner, true);
            if (GUILayout.Button("Borrar copias generadas")) Run(spawner, false);
        }
    }

    private static void Run(BoxObjectSpawner spawner, bool generate)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generador de caja");
        Undo.RecordObject(spawner, "Generador de caja");
        if (generate) spawner.Generate();
        else spawner.ClearGenerated();
        if (!Application.isPlaying)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        }
        Undo.CollapseUndoOperations(group);
    }

    private void OnSceneGUI()
    {
        // Do not register bounds controls while moving or rotating: they can capture
        // mouse input intended for Unity's standard Transform handles.
        if (Tools.current != Tool.Scale || Tools.viewToolActive) return;

        var spawner = (BoxObjectSpawner)target;
        using (new Handles.DrawingScope(Color.cyan, spawner.transform.localToWorldMatrix))
        {
            box.center = spawner.center;
            box.size = spawner.size;
            EditorGUI.BeginChangeCheck();
            box.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(spawner, "Ajustar caja de generación");
                spawner.center = box.center;
                spawner.size = box.size;
                PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
                EditorUtility.SetDirty(spawner);
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            }
        }
    }
}
