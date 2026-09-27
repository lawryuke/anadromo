#if UNITY_EDITOR
using System;
using System.IO;
using Anadromo.AI;
using Anadromo.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Esc2EnergySetup
{
    static Esc2EnergySetup() { EditorApplication.update+=Request; }
    static void Request()
    {
        const string path="Library/Esc2Energy.request";
        if(!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action=File.ReadAllText(path).Trim(); File.Delete(path);
        try { if(action=="install") Install(); if(action=="test") Esc2EnergyCheck.Run(); }
        catch(Exception e) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2EnergySetup.txt",e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Anadromo/Esc2/Energia/Integrar energia vital")]
    public static void Install()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/_Project/Scenes/esc2.unity") throw new InvalidOperationException("Abre esc2.");
        var player=UnityEngine.Object.FindFirstObjectByType<PiranhaPlayerTarget>();
        if(!player) throw new InvalidOperationException("Falta el jugador del encuentro.");
        Directory.CreateDirectory("Library/Esc2PiranhaBackup");
        EditorSceneManager.SaveScene(scene,"Library/Esc2PiranhaBackup/esc2-before-vital-energy.unity",true);
        var vital=player.GetComponent<PlayerEnergyController>();
        if(!vital) vital=Undo.AddComponent<PlayerEnergyController>(player.gameObject);
        if(!player.GetComponent<EnergyVisualFeedback>()) Undo.AddComponent<EnergyVisualFeedback>(player.gameObject);
        player.showDebugStatus=false; EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2EnergySetup.txt","PASS: KM_Player comparte EnergySystem para desgaste, golpes y krill; sin barra de salud; feedback de energia conectado.");
    }
}
#endif
