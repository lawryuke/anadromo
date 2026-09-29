using UnityEngine;
using Anadromo.Mechanics;

namespace Anadromo.AI
{
    // A reusable crossing, independent from schools: never steers towards the player.
    public sealed class Esc2SharkPassage : MonoBehaviour
    {
        public enum PassageState { Waiting, Warning, Crossing, Cooldown, Ending }
        public PiranhaPlayerTarget target;
        public Transform shark;
        public Transform[] waypoints;
        public ZoneLimit activationZone;
        public float travelSpeed=15, warningDuration=3, repeatDelay=18, activationRadius=4;
        [Min(0f), Tooltip("Velocidad de giro en grados por segundo hacia el siguiente punto de ruta.")]
        public float turnSpeed=180f;
        public float contactRadius=.5f, bodyHalfLength=.75f, obstacleRadius=.4f;
        public LayerMask obstacleLayers=Physics.DefaultRaycastLayers;
        [Header("Audio 3D del recorrido")]
        public AudioClip passageSound;
        public bool loopPassageSound;
        [Range(0f,1f)] public float soundVolume=1f;
        [Min(.01f)] public float soundMinDistance=3f;
        [Min(.01f)] public float soundMaxDistance=35f;
        [Min(0f), Tooltip("Segundos inmóvil al final del recorrido mientras el sonido baja hasta silencio.")]
        public float endFadeDuration=1.5f;
        AudioSource passageAudio;
        public PassageState State { get; private set; }
        public float WarningRemaining { get; private set; }
        int waypoint;
        float cooldown;
        float endElapsed, endStartVolume;
        public bool Configured => target && shark && waypoints!=null && waypoints.Length>=2 && System.Array.TrueForAll(waypoints,p=>p);

        void Start()
        {
            if(!Configured) { Debug.LogError("Paso de tiburon: asigna jugador, cuerpo y al menos dos puntos de ruta.",this); enabled=false; return; }
            ResetPassage();
        }
        public static float SegmentDistance(Vector3 point,Vector3 from,Vector3 to)
        {
            Vector3 delta=to-from;
            float t=delta.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector3.Dot(point-from,delta)/delta.sqrMagnitude);
            return Vector3.Distance(point,from+delta*t);
        }
        bool IsWall(Collider c) => c && !c.transform.IsChildOf(transform) && !c.transform.IsChildOf(target.transform.root);
        bool Clear(Vector3 from,Vector3 to)
        {
            Vector3 d=to-from;
            foreach(var hit in Physics.RaycastAll(from,d.normalized,d.magnitude,obstacleLayers,QueryTriggerInteraction.Ignore))
                if(IsWall(hit.collider)) return false;
            return true;
        }
        bool PlayerNearRoute()
        {
            Vector3 p=target.transform.position;
            if (activationZone != null)
            {
                return activationZone.Contains(p);
            }
            for(int i=1;i<waypoints.Length;i++)
            {
                Vector3 a=waypoints[i-1].position,d=waypoints[i].position-a;
                Vector3 nearest=a+d*(d.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude));
                if(Vector3.Distance(p,nearest)<=activationRadius && Clear(p,nearest)) return true;
            }
            return false;
        }
        void Update() { Tick(Time.deltaTime); }
        void LateUpdate()
        {
            if(!passageAudio) return;
            if(State==PassageState.Warning && waypoints!=null && waypoints.Length>0 && waypoints[0])
                passageAudio.transform.position=waypoints[0].position;
            else if((State==PassageState.Crossing || State==PassageState.Ending) && shark)
                passageAudio.transform.position=shark.position;
        }
        public void Tick(float dt)
        {
            if(!Configured || !target.Alive) { StopPassageSound(); return; }
            if(dt<=0) return;
            if(State==PassageState.Waiting)
            {
                if(PlayerNearRoute())
                {
                    shark.SetPositionAndRotation(waypoints[0].position,Quaternion.LookRotation((waypoints[1].position-waypoints[0].position).normalized));
                    shark.gameObject.SetActive(true);
                    State=PassageState.Warning; WarningRemaining=warningDuration;
                    PlayPassageSound();
                }
                return;
            }
            if(State==PassageState.Warning)
            {
                WarningRemaining-=dt;
                if(WarningRemaining>0) return;
                waypoint=1; cooldown=repeatDelay; State=PassageState.Crossing;
                return;
            }
            cooldown=Mathf.Max(0,cooldown-dt);
            if(State==PassageState.Ending)
            {
                endElapsed+=dt;
                float progress=endFadeDuration>0f ? Mathf.Clamp01(endElapsed/endFadeDuration) : 1f;
                if(passageAudio) passageAudio.volume=endStartVolume*(1f-progress);
                if(progress>=1f) FinishCrossing();
                return;
            }
            if(State==PassageState.Cooldown) { if(cooldown<=0) State=PassageState.Waiting; return; }
            float budget=Mathf.Max(0,travelSpeed)*dt;
            while(budget>0 && waypoint<waypoints.Length)
            {
                Vector3 start=shark.position,delta=waypoints[waypoint].position-start;
                if(delta.magnitude<.001f) { waypoint++; continue; }
                Vector3 direction=delta.normalized;
                float step=Mathf.Min(budget,delta.magnitude),allowed=step;
                foreach(var hit in Physics.SphereCastAll(start,obstacleRadius,direction,step,obstacleLayers,QueryTriggerInteraction.Ignore))
                    if(IsWall(hit.collider)) allowed=Mathf.Min(allowed,Mathf.Max(0,hit.distance-.02f));
                Vector3 end=start+direction*allowed;
                // Share the frame's turn time across legs when crossing multiple waypoints.
                float segmentTime=step/travelSpeed;
                Quaternion rotation=Quaternion.RotateTowards(shark.rotation,Quaternion.LookRotation(direction),Mathf.Max(0f,turnSpeed)*segmentTime);
                shark.SetPositionAndRotation(end,rotation);
                // Sweep every leg, including frames that cross more than one waypoint.
                if(SegmentDistance(target.transform.position,start-direction*bodyHalfLength,end+direction*bodyHalfLength)<=contactRadius && Clear(end,target.transform.position))
                    target.TakeDamage(target.maxHealth);
                budget-=step;
                if(allowed<step-.001f) { FinishCrossing(); return; }
                if(Vector3.Distance(end,waypoints[waypoint].position)<.001f) waypoint++;
                if(!target.Alive) return;
            }
            if(waypoint>=waypoints.Length)
            {
                shark.position=waypoints[waypoints.Length-1].position;
                State=PassageState.Ending;
                endElapsed=0f;
                endStartVolume=passageAudio ? passageAudio.volume : 0f;
                if(endFadeDuration<=0f) FinishCrossing();
            }
        }
        void PlayPassageSound()
        {
            if(!passageSound || !shark) return;
            if(!passageAudio)
            {
                // Keep the emitter under the controller and follow the warning/attack position.
                var emitter=new GameObject("Passage Audio 3D");
                emitter.transform.SetParent(transform,false);
                passageAudio=emitter.AddComponent<AudioSource>();
            }
            passageAudio.transform.position=waypoints[0].position;
            passageAudio.playOnAwake=false;
            passageAudio.loop=loopPassageSound;
            passageAudio.spatialBlend=1f;
            passageAudio.rolloffMode=AudioRolloffMode.Linear;
            passageAudio.minDistance=Mathf.Max(.01f,soundMinDistance);
            passageAudio.maxDistance=Mathf.Max(passageAudio.minDistance+.01f,soundMaxDistance);
            passageAudio.dopplerLevel=0f;
            passageAudio.volume=soundVolume;
            passageAudio.clip=passageSound;
            passageAudio.Play();
        }
        void StopPassageSound() { if(passageAudio) passageAudio.Stop(); }
        void FinishCrossing() { StopPassageSound(); shark.gameObject.SetActive(false); State=PassageState.Cooldown; }
        public void ResetPassage()
        {
            StopPassageSound();
            State=PassageState.Waiting; WarningRemaining=cooldown=endElapsed=endStartVolume=0; waypoint=1;
            if(shark) { if(waypoints!=null && waypoints.Length>0 && waypoints[0]) shark.position=waypoints[0].position; shark.gameObject.SetActive(false); }
        }
        void OnDisable() { StopPassageSound(); if(Application.isPlaying && shark) shark.gameObject.SetActive(false); }
        void OnGUI()
        {
            if(State!=PassageState.Warning || !target || !target.Alive) return;
            GUI.color=new Color(1,.65f,.15f);
            GUI.Box(new Rect(Screen.width/2-220,90,440,60),"TIBURON EN EL PASO — "+Mathf.CeilToInt(WarningRemaining)+" s\nApartate del recorrido: cambia de altura o ve a un lateral");
            GUI.color=Color.white;
        }
        void OnDrawGizmosSelected()
        {
            if(waypoints==null) return;
            Gizmos.color=new Color(1,.45f,.1f);
            for(int i=0;i<waypoints.Length;i++) if(waypoints[i])
            { Gizmos.DrawWireSphere(waypoints[i].position,contactRadius); if(i>0 && waypoints[i-1]) Gizmos.DrawLine(waypoints[i-1].position,waypoints[i].position); }
        }
    }
}
