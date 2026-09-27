#if UNITY_EDITOR
using System;
using System.IO;
using Anadromo.AI;
using UnityEditor;
using UnityEngine;

public static class Esc2PiranhaValidation
{
    static void InvokeLifecycle(Component component,string method) => component.GetType().GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(component,null);
    static object Invoke(Component component,string method,params object[] args) => component.GetType().GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(component,args);
    [MenuItem("Anadromo/Esc2/Pirañas/Validar integración")]
    public static void Run()
    {
        int checks=0;
        Action<bool,string> check=(ok,message)=>{if(!ok) throw new InvalidOperationException(message); checks++;};
        foreach(int fps in new[]{30,60,120})
        {
            var root = new GameObject("Fixture pirañas"); root.transform.position = new Vector3(1000,1000,1000);
            GameObject wall = null;
            try
            {
                var school=root.AddComponent<PiranhaSchool>(); school.zoneSize=Vector3.one*20;
                var go=new GameObject("Player fixture"); go.transform.SetParent(root.transform,false);
                var player=go.AddComponent<PiranhaPlayerTarget>(); InvokeLifecycle(player,"Awake"); school.target=player;
                Func<string,Vector3,Esc2Piranha> make=(name,offset)=>
                {
                    var f=new GameObject(name); f.transform.SetParent(root.transform,false); f.transform.localPosition=offset;
                    var fish=f.AddComponent<Esc2Piranha>(); InvokeLifecycle(fish,"Awake"); InvokeLifecycle(fish,"OnEnable"); return fish;
                };
                var a=make("A",Vector3.forward*2); var b=make("B",Vector3.forward*3);
                a.normalSpeed=b.normalSpeed=0; a.attackSpeed=b.attackSpeed=0;
                a.Tick(.1f); check(a.State==Esc2Piranha.BehaviourState.Disturbed,"Quietud inicia espera");
                for(int i=0;i<fps*3;i++) a.Tick(1f/fps);
                check(a.State==Esc2Piranha.BehaviourState.Attack && b.State==Esc2Piranha.BehaviourState.Attack,"Alerta de cardumen a "+fps+" FPS");
                a.ResetFish(); b.ResetFish();
                player.transform.position+=Vector3.right*.08f; player.SampleMotion(.02f); a.Tick(.01f);
                check(a.State==Esc2Piranha.BehaviourState.Disturbed,"4 m/s no activan inmediatamente");
                player.transform.position+=Vector3.right*.16f; player.SampleMotion(.02f); a.Tick(.01f);
                check(a.State==Esc2Piranha.BehaviourState.Attack,"8 m/s activan ataque");
                var copy=UnityEngine.Object.Instantiate(b.gameObject,root.transform); var duplicate=copy.GetComponent<Esc2Piranha>();
                InvokeLifecycle(duplicate,"Awake"); InvokeLifecycle(duplicate,"OnEnable");
                duplicate.ResetFish(); school.Alert(a);
                check(duplicate.School==school && duplicate.State==Esc2Piranha.BehaviourState.Attack,"Duplicado se une sin configurar referencias");
                UnityEngine.Object.DestroyImmediate(copy);
                player.ResetEncounter();
                a.transform.position=player.transform.position+Vector3.forward*.1f; a.BeginAttack(); a.Tick(.01f);
                check(player.Health==92,"Mordida resta 8"); a.Tick(.5f); check(player.Health==92,"Cooldown evita daño por frame");
                a.Tick(.6f); check(player.Health==84,"Segunda mordida tras intervalo");
                player.ResetEncounter(); a.ResetFish(); b.ResetFish();
                a.transform.localPosition=Vector3.left; b.transform.localPosition=Vector3.right;
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=root.transform.position; wall.transform.localScale=new Vector3(.3f,6,6);
                Physics.SyncTransforms(); school.Alert(a);
                check(b.State==Esc2Piranha.BehaviourState.Chill,"Alerta no cruza paredes");
                player.transform.localPosition=Vector3.right*2;
                a.transform.forward=Vector3.right; InvokeLifecycle(a,"Awake"); a.attackSpeed=9; a.BeginAttack(); a.Tick(.5f);
                check(a.transform.localPosition.x<-.3f,"Barrido impide atravesar pared");
                UnityEngine.Object.DestroyImmediate(wall);
                player.transform.localPosition=Vector3.right*30; a.Tick(.1f);
                check(a.State==Esc2Piranha.BehaviourState.Chill && school.Contains(a.transform.position),"Jugador fuera de zona cancela persecución");
                player.TakeDamage(100); check(!player.Alive,"Daño letal"); player.ResetEncounter();
                check(player.Health==100 && player.Velocity==Vector3.zero && a.State==Esc2Piranha.BehaviourState.Chill,"Reinicio restaura salud, ruido y cardumen");
                var movement=go.AddComponent<SimpleFlyCamera>(); player.desktopMovement=movement;
                var lampGo=new GameObject("Lamprea prueba"); lampGo.transform.SetParent(root.transform,false); lampGo.transform.localPosition=Vector3.left*.5f;
                var lamp=lampGo.AddComponent<Esc2Lamprey>(); InvokeLifecycle(lamp,"Awake");
                Invoke(lamp,"Attach",player); check(lamp.State==Esc2Lamprey.BehaviourState.Attached && player.SpeedMultiplier==.85f && movement.externalSpeedMultiplier==.85f,"Lamprea ralentiza locomoción");
                for(int i=0;i<fps*2+1;i++) Invoke(lamp,"Tick",1f/fps);
                check(Mathf.Approximately(player.Health,95),"Lamprea drena 5 de salud cada 2 s");
                lamp.ReduceGrip(35); lamp.ReduceGrip(35); lamp.ReduceGrip(35);
                check(lamp.State==Esc2Lamprey.BehaviourState.Stunned && player.SpeedMultiplier==1 && movement.externalSpeedMultiplier==1,"Sacudidas sueltan y restauran velocidad");
                Invoke(lamp,"Tick",3.1f); check(lamp.State==Esc2Lamprey.BehaviourState.Chill && lamp.Grip==100,"Lamprea recupera el agarre");
                var anglerGo=new GameObject("Pez linterna prueba"); anglerGo.transform.SetParent(root.transform,false); anglerGo.transform.localPosition=Vector3.forward*2;
                var angler=anglerGo.AddComponent<Esc2Anglerfish>(); InvokeLifecycle(angler,"Awake"); angler.attackSpeed=.1f;
                Invoke(angler,"Tick",.01f); check(angler.State==Esc2Anglerfish.BehaviourState.Disturbed,"Pez linterna se inmoviliza al ser descubierto");
                for(int i=0;i<fps*2+1;i++) Invoke(angler,"Tick",1f/fps);
                check(angler.State==Esc2Anglerfish.BehaviourState.Attack,"Pez linterna inicia su embestida");
                player.transform.position+=Vector3.right*2; angler.attackSpeed=9;
                for(int i=0;i<fps/4;i++) Invoke(angler,"Tick",1f/fps);
                check(player.Health==95,"Esquivar la trayectoria fija evita la embestida");
            }
            finally { if(wall) UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(root); }
        }
        Directory.CreateDirectory("Logs"); string result="PASS: "+checks+" comprobaciones de pirañas esc2, 30/60/120 FPS, ruido, cardumen, duplicación, paredes, daño, límites y reinicio.";
        File.WriteAllText("Logs/Esc2PiranhaValidation.txt",result); Debug.Log(result);
    }
}
#endif
