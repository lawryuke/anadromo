using Anadromo.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ZoneLimit))]
public class ZoneLimitEditor : Editor
{
    private readonly UnityEditor.IMGUI.Controls.BoxBoundsHandle box =
        new UnityEditor.IMGUI.Controls.BoxBoundsHandle();

    public override void OnInspectorGUI()
    {
        if (target == null) return;
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Define una zona lógica sin usar BoxCollider físicos. " +
            "En Scene: Usa la herramienta de escala (R) o arrastra los puntos de la caja para ajustar su tamaño. " +
            "Ideal para triggers y límites sin interferir con las colisiones del juego.", MessageType.Info);
    }

    private void OnSceneGUI()
    {
        if (Tools.current != Tool.Scale || Tools.viewToolActive) return;

        var limit = (ZoneLimit)target;
        if (limit == null) return;
        
        using (new Handles.DrawingScope(Color.cyan, limit.transform.localToWorldMatrix))
        {
            box.center = limit.center;
            box.size = limit.size;
            EditorGUI.BeginChangeCheck();
            box.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(limit, "Ajustar tamaño de ZoneLimit");
                limit.center = box.center;
                limit.size = box.size;
                PrefabUtility.RecordPrefabInstancePropertyModifications(limit);
                EditorUtility.SetDirty(limit);
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(limit.gameObject.scene);
            }
        }
    }
}
