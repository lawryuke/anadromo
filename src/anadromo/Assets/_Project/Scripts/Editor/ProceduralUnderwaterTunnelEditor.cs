using UnityEditor;
using UnityEngine;

namespace Anadromo.Environment
{
    [CustomEditor(typeof(ProceduralUnderwaterTunnel))]
    public class ProceduralUnderwaterTunnelEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ProceduralUnderwaterTunnel script = (ProceduralUnderwaterTunnel)target;

            GUILayout.Space(15);
            GUI.backgroundColor = new Color(0.12f, 0.55f, 0.85f);
            if (GUILayout.Button("🌊 GENERAR / ACTUALIZAR MÁQUINA DE TÚNEL", GUILayout.Height(38)))
            {
                script.GenerarMallaTunel();
                EditorUtility.SetDirty(script);
            }
            GUI.backgroundColor = Color.white;
        }
    }
}
