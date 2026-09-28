using UnityEngine;

namespace Anadromo.AI
{
    public sealed class Esc2BlindFish : MonoBehaviour
    {
        public enum BehaviourState { Chill, Perturbed, Critical, Inspecting, Attack, Retreat }
        public PiranhaPlayerTarget target;
        public BlindFishMotionSensor motion;
        public Transform body;
        public Renderer bodyRenderer;
        public Transform[] patrolPoints;
        public LayerMask obstacleLayers=Physics.DefaultRaycastLayers;
        public float detectionRadius=6, agitationRise=25, agitationFall=12.5f;
        public float graceDuration=2, inspectionDuration=3, inspectionDistance=1.5f;
        public float patrolSpeed=.6f, approachSpeed=2.5f, attackSpeed=7, retreatDuration=3;
        public float bodyRadius=.3f, contactRadius=.55f, territoryRadius=12;
        public BehaviourState State { get; private set; }
        public float Agitation { get; private set; }
        public Vector3 InspectionPoint { get; private set; }
        public float StateTime { get; private set; }
        Vector3 home,trackedPosition;
        Quaternion homeRotation;
        Transform view;
        int patrolIndex;
        bool inspectingAtPoint;
        float lostTime;
        MaterialPropertyBlock tint;
        void Awake() { home=body?body.position:transform.position; homeRotation=body?body.rotation:transform.rotation; }
        void Start()
        {
            if(target && !motion) motion=target.GetComponent<BlindFishMotionSensor>();
            if(!target || !motion || !body || !bodyRenderer)
            { Debug.LogError("Pez ciego: asigna Target, Motion, Body y Body Renderer.",this); enabled=false; return; }
            var camera=target.GetComponentInChildren<Camera>(); view=camera?camera.transform:target.transform;
            ResetFish();
        }
        bool IsWall(Collider c) => c && !c.transform.IsChildOf(transform) && !c.transform.IsChildOf(target.transform.root);
        public bool ClearPath(Vector3 a,Vector3 b)
        {
            Vector3 d=b-a;
            foreach(var hit in Physics.RaycastAll(a,d.normalized,d.magnitude,obstacleLayers,QueryTriggerInteraction.Ignore)) if(IsWall(hit.collider)) return false;
            return true;
        }
        bool Free(Vector3 p)
        {
            foreach(var c in Physics.OverlapSphere(p,bodyRadius,obstacleLayers,QueryTriggerInteraction.Ignore)) if(IsWall(c)) return false;
            return true;
        }
        void SetState(BehaviourState state) { State=state; StateTime=0; lostTime=0; }
        void Update()
        {
            if(!target || !motion || !body || !target.Alive) return;
            float dt=Time.deltaTime;
            ApplyColor();
            if(!motion.MotionValid) return; // Lost tracking is not evidence of movement.
            StateTime+=dt;
            bool nearby=Vector3.Distance(body.position,target.transform.position)<=detectionRadius && ClearPath(body.position,target.transform.position);
            switch(State)
            {
                case BehaviourState.Chill:
                case BehaviourState.Perturbed:
                    if(nearby && motion.IsNoisy) Agitation=Mathf.Min(100,Agitation+agitationRise*dt);
                    else if(!nearby || motion.IsStill) Agitation=Mathf.Max(0,Agitation-agitationFall*dt);
                    State=Agitation>0?BehaviourState.Perturbed:BehaviourState.Chill;
                    Patrol(dt);
                    if(Agitation>=100) { trackedPosition=target.transform.position; SetState(BehaviourState.Critical); }
                    break;
                case BehaviourState.Critical:
                    // Approach the last detected position without touching the player during grace.
                    if(Vector3.Distance(body.position,trackedPosition)>inspectionDistance+.2f) Move(trackedPosition,approachSpeed,dt,false,inspectionDistance+.2f);
                    if(motion.IsStill) BeginInspection();
                    else if(StateTime>=graceDuration) SetState(BehaviourState.Attack);
                    break;
                case BehaviourState.Inspecting:
                    if(!motion.IsStill) { SetState(BehaviourState.Attack); break; }
                    if(!inspectingAtPoint)
                    {
                        Move(InspectionPoint,approachSpeed,dt,false);
                        if(Vector3.Distance(body.position,InspectionPoint)<.12f) { inspectingAtPoint=true; StateTime=0; }
                        else if(StateTime>6) BeginRetreat();
                    }
                    else
                    {
                        Vector3 look=target.transform.position-body.position;
                        if(look.sqrMagnitude>.001f) body.rotation=Quaternion.LookRotation(look);
                        if(StateTime>=inspectionDuration) BeginRetreat();
                    }
                    break;
                case BehaviourState.Attack:
                    // Once the grace/inspection has been broken, stopping late cannot cancel the attack.
                    if(Vector3.Distance(target.transform.position,home)>territoryRadius) { BeginRetreat(); break; }
                    if(!ClearPath(body.position,target.transform.position))
                    { lostTime+=dt; if(lostTime>2) BeginRetreat(); break; }
                    lostTime=0; Move(target.transform.position,attackSpeed,dt,true);
                    break;
                case BehaviourState.Retreat:
                    Move(home,approachSpeed,dt,false);
                    if(StateTime>=retreatDuration && Vector3.Distance(body.position,home)<.15f) SetState(BehaviourState.Chill);
                    break;
            }
            ApplyColor();
        }
        void BeginInspection()
        {
            Vector3 origin=target.transform.position,forward=view?view.forward:target.transform.forward;
            Vector3 desired=origin+forward*inspectionDistance;
            // Capture the viewing direction only once. Looking around does not move the inspection point.
            InspectionPoint=body.position;
            if(Free(desired) && ClearPath(origin,desired) && ClearPath(body.position,desired)) InspectionPoint=desired;
            else
                foreach(var direction in new[]{(body.position-origin).normalized,Vector3.right,Vector3.left,Vector3.up})
                {
                    var candidate=origin+direction*inspectionDistance;
                    if(Free(candidate) && ClearPath(origin,candidate) && ClearPath(body.position,candidate)) { InspectionPoint=candidate; break; }
                }
            inspectingAtPoint=false; SetState(BehaviourState.Inspecting);
        }
        void BeginRetreat() { Agitation=0; SetState(BehaviourState.Retreat); }
        void Patrol(float dt)
        {
            if(patrolPoints==null || patrolPoints.Length==0) return;
            var point=patrolPoints[patrolIndex%patrolPoints.Length];
            if(!point) return;
            Move(point.position,patrolSpeed,dt,false);
            if(Vector3.Distance(body.position,point.position)<.1f) patrolIndex=(patrolIndex+1)%patrolPoints.Length;
        }
        void Move(Vector3 destination,float speed,float dt,bool lethal,float stopDistance=0)
        {
            Vector3 start=body.position,delta=destination-start;
            float distance=Mathf.Min(speed*dt,Mathf.Max(0,delta.magnitude-stopDistance));
            Vector3 direction=delta.normalized;
            foreach(var hit in Physics.SphereCastAll(start,bodyRadius,direction,distance,obstacleLayers,QueryTriggerInteraction.Ignore))
                if(IsWall(hit.collider)) distance=Mathf.Min(distance,Mathf.Max(0,hit.distance-.02f));
            body.position=start+direction*distance;
            if(direction.sqrMagnitude>.001f) body.rotation=Quaternion.Slerp(body.rotation,Quaternion.LookRotation(direction),1-Mathf.Exp(-6*dt));
            if(lethal && Esc2SharkPassage.SegmentDistance(target.transform.position,start,body.position)<=contactRadius && ClearPath(body.position,target.transform.position))
                target.TakeDamage(target.maxHealth);
        }
        void ApplyColor()
        {
            if(!bodyRenderer) return;
            if(tint==null) tint=new MaterialPropertyBlock();
            Color color=new Color(.22f,.46f,.65f);
            if(State==BehaviourState.Perturbed) color=Color.Lerp(Color.yellow,new Color(1,.3f,.02f),Agitation/100);
            if(State==BehaviourState.Critical) color=Color.Lerp(new Color(.45f,0,0),Color.red,.5f+.5f*Mathf.Sin(Time.time*18));
            if(State==BehaviourState.Inspecting) color=Color.Lerp(new Color(.25f,.06f,.4f),new Color(.85f,.2f,1),.5f+.5f*Mathf.Sin(Time.time*10));
            if(State==BehaviourState.Attack) color=Color.red;
            tint.SetColor("_BaseColor",color); tint.SetColor("_EmissionColor",color*.25f); bodyRenderer.SetPropertyBlock(tint);
        }
        public void ResetFish()
        {
            if(body) body.SetPositionAndRotation(home,homeRotation);
            Agitation=StateTime=lostTime=0; State=BehaviourState.Chill; patrolIndex=0; inspectingAtPoint=false; ApplyColor();
        }
        void OnDrawGizmosSelected()
        {
            if(!body) return;
            Gizmos.color=Color.yellow; Gizmos.DrawWireSphere(body.position,detectionRadius);
            if(patrolPoints!=null) for(int i=1;i<patrolPoints.Length;i++) if(patrolPoints[i-1] && patrolPoints[i]) Gizmos.DrawLine(patrolPoints[i-1].position,patrolPoints[i].position);
        }
    }
}
