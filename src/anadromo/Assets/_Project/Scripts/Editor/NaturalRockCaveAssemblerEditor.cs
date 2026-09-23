using UnityEditor;
using UnityEngine;

namespace Anadromo.Environment
{
    [CustomEditor(typeof(NaturalRockCaveAssembler))]
    public class NaturalRockCaveAssemblerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            NaturalRockCaveAssembler script = (NaturalRockCaveAssembler)target;

            GUILayout.Space(15);
            GUI.backgroundColor = new Color(0.18f, 0.65f, 0.35f);
            if (GUILayout.Button("🪨 CONSTRUIR / REORGANIZAR CUEVA DE ROCAS NATURALES", GUILayout.Height(40)))
            {
                script.ConstruirCuevaNatural();
                EditorUtility.SetDirty(script);
            }
            GUI.backgroundColor = Color.white;
        }
    }
}
