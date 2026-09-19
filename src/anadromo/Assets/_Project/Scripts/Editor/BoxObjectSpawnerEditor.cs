using Anadromo.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(BoxObjectSpawner))]
public class BoxObjectSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Arrastra el objeto de Hierarchy a Source Object. La caja no necesita Collider. " +
            "Los MeshCollider no convexos se comprueban mediante sus bounds (más conservador). " +
            "La separación se comprueba solo al generar.", MessageType.Info);
        var spawner = (BoxObjectSpawner)target;
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
        var spawner = (BoxObjectSpawner)target;
        var box = new UnityEditor.IMGUI.Controls.BoxBoundsHandle();
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
            }
        }
    }
}
