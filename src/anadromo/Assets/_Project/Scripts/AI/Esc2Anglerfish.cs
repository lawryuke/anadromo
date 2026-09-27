using UnityEngine;

namespace Anadromo.AI
{
    public sealed class Esc2Anglerfish : MonoBehaviour
    {
        public enum BehaviourState { Chill, Disturbed, Attack, Recover }
        public Renderer body;
        public Light lureLight;
        public Transform lure;
        public float detectionRadius=5, attackDelay=1.8f, attackSpeed=9, damage=25, recoveryDuration=2, contactRadius=.7f;
        public BehaviourState State { get; private set; }
        PiranhaSchool school;
        Vector3 home, direction, previous;
        float timer, attackElapsed, cooldown;
        Vector3 lureHome;
        MaterialPropertyBlock tint;
        void Awake() { school=GetComponentInParent<PiranhaSchool>(); home=previous=transform.position; direction=transform.forward; tint=new MaterialPropertyBlock(); if(!lure) { var bulb=transform.Find("Señuelo"); if(bulb) lure=bulb; } if(lure) lureHome=lure.localPosition; }
        void OnTransformParentChanged() { school=GetComponentInParent<PiranhaSchool>(); }
        void Update() { if(school && school.target && school.target.Alive) Tick(Time.deltaTime); }
        void Tick(float dt)
        {
            var player=school.target; previous=transform.position; cooldown-=dt;
            bool detected=school.Contains(player.transform.position) && Vector3.Distance(previous,player.transform.position)<=detectionRadius && school.ClearPath(previous,player.transform.position);
            if(cooldown>0) { State=BehaviourState.Recover; }
            else if(State==BehaviourState.Chill && detected) { State=BehaviourState.Disturbed; timer=0; }
            else if(State==BehaviourState.Disturbed)
            {
                if(!detected) { State=BehaviourState.Chill; timer=0; }
                else { timer+=dt; if(timer>=attackDelay) { State=BehaviourState.Attack; attackElapsed=0; direction=(player.transform.position-previous).normalized; } }
            }
            if(State==BehaviourState.Attack)
            {
                attackElapsed+=dt;
                Vector3 delta=direction*attackSpeed*dt; float max=delta.magnitude;
                foreach(var hit in Physics.SphereCastAll(transform.position,.3f,direction,max,school.obstacleLayers,QueryTriggerInteraction.Ignore))
                    if(school.IsObstacle(hit.collider)) max=Mathf.Min(max,Mathf.Max(0,hit.distance-.02f));
                transform.position+=direction*max;
                float u=delta.sqrMagnitude<.00001f?0:Mathf.Clamp01(Vector3.Dot(player.transform.position-previous,delta)/delta.sqrMagnitude);
                if(cooldown<=0 && Vector3.Distance(previous+delta*u,player.transform.position)<=contactRadius && school.ClearPath(transform.position,player.transform.position))
                { player.TakeDamage(damage); cooldown=recoveryDuration; State=BehaviourState.Recover; }
                else if(attackElapsed>=1.3f) { State=BehaviourState.Recover; cooldown=recoveryDuration; }
                else if(attackSpeed>0 && delta.sqrMagnitude>0) transform.rotation=Quaternion.LookRotation(direction);
            }
            if(lureLight) lureLight.enabled=State==BehaviourState.Chill;
            if(lure) lure.localPosition=lureHome+Vector3.up*(State==BehaviourState.Chill?Mathf.Sin(Time.time*3)*.08f:0);
            if(body)
            {
                Color c=State==BehaviourState.Chill?new Color(.32f,.35f,.52f):State==BehaviourState.Disturbed?new Color(1,.68f,.12f):State==BehaviourState.Attack?new Color(1,.12f,.08f):new Color(.17f,.21f,.27f);
                tint.SetColor("_BaseColor",c); tint.SetColor("_Color",c); body.SetPropertyBlock(tint);
            }
            if(State==BehaviourState.Recover && cooldown<=0) { State=BehaviourState.Chill; timer=0; }
        }
        public void ResetFish() { if(school) home=school.Clamp(home); transform.position=home; State=BehaviourState.Chill; timer=cooldown=attackElapsed=0; }
    }
}
