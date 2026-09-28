#if UNITY_EDITOR
using Anadromo.AI;
using UnityEditor;
using UnityEngine;

public abstract class Esc2EnemyInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var enemy=(Component)target;
        if(EditorUtility.IsPersistent(enemy)) return;
        var school=enemy.GetComponentInParent<PiranhaSchool>();
        if(!school || !school.target)
            EditorGUILayout.HelpBox("Este enemigo no puede interactuar: necesita un padre con PiranhaSchool y su Target asignado al jugador. Usa Anadromo > Esc2 > Depredadores > Reparar referencias y colores.",MessageType.Error);
        else
        {
            EditorGUILayout.ObjectField("Territorio",school,typeof(PiranhaSchool),true);
            EditorGUILayout.ObjectField("Jugador",school.target,typeof(PiranhaPlayerTarget),true);
            if(!school.Contains(enemy.transform.position) && !Application.isPlaying)
                EditorGUILayout.HelpBox("El enemigo está fuera de su territorio. Revisa los límites de ZonaA.",MessageType.Warning);
        }
    }
}
[CustomEditor(typeof(Esc2Lamprey))]
public sealed class Esc2LampreyInspector : Esc2EnemyInspector { }
[CustomEditor(typeof(Esc2Anglerfish))]
public sealed class Esc2AnglerfishInspector : Esc2EnemyInspector { }
[CustomEditor(typeof(Esc2Piranha))]
public sealed class Esc2PiranhaInspector : Esc2EnemyInspector { }
#endif
