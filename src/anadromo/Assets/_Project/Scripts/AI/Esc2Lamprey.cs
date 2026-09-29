using UnityEngine;

namespace Anadromo.AI
{
    public sealed class Esc2Lamprey : MonoBehaviour
    {
        public enum BehaviourState { Chill, Chasing, Attached, Stunned }
        public Renderer body;
        public float detectionRadius=4, chaseSpeed=4, maxGrip=100, stunDuration=3, drainInterval=2, drainDamage=5;
        public BehaviourState State { get; private set; }
        public PiranhaPlayerTarget Target=>school?school.target:null;
        public float Grip { get; private set; }=100;
        public Vector3 attachOffset=new Vector3(.35f,-.25f,.65f);
        [Header("Audio de succión")]
        public AudioClip feedingLoop;
        [Range(0f,1f)] public float feedingVolume=.65f;
        PiranhaSchool school;
        Transform cameraTransform;
        int attachmentSlot;
        public int AttachmentSlot => attachmentSlot;
        Vector3 home,previous;
        float stunTimer,drainTimer;
        AudioSource feedingSource;
        void Awake()
        {
            school=GetComponentInParent<PiranhaSchool>(); home=previous=transform.position;
            feedingSource=gameObject.AddComponent<AudioSource>();
            feedingSource.playOnAwake=false;
            feedingSource.loop=true;
            feedingSource.spatialBlend=1f;
            feedingSource.rolloffMode=AudioRolloffMode.Linear;
            feedingSource.minDistance=1f;
            feedingSource.maxDistance=8f;
            feedingSource.dopplerLevel=0f;
            feedingSource.clip=feedingLoop;
            feedingSource.volume=feedingVolume;
            var cam=school && school.target ? school.target.transform.GetComponentInChildren<Camera>() : null;
            cameraTransform=cam?cam.transform:(school&&school.target?school.target.transform:null);
        }
        void OnTransformParentChanged() { school=GetComponentInParent<PiranhaSchool>(); }
        void Update()
        {
            if(school && school.target && school.target.Alive) Tick(Time.deltaTime);
            UpdateFeedingAudio();
        }
        void UpdateFeedingAudio()
        {
            if(!feedingSource) return;
            bool feeding=State==BehaviourState.Attached && Target && Target.Alive && feedingLoop;
            if(!feeding) { StopFeedingAudio(); return; }
            feedingSource.volume=feedingVolume;
            if(feedingSource.clip!=feedingLoop) feedingSource.clip=feedingLoop;
            if(!feedingSource.isPlaying) feedingSource.Play();
        }
        void StopFeedingAudio() { if(feedingSource) feedingSource.Stop(); }
        void Tick(float dt)
        {
            var player=school.target; previous=transform.position;
            if(State==BehaviourState.Stunned)
            {
                stunTimer-=dt;
                if(stunTimer<=0) { State=BehaviourState.Chill; Grip=maxGrip; }
            }
            else if(State==BehaviourState.Attached)
            {
                StickToPlayer(); drainTimer-=dt;
                if(drainTimer<=0) { drainTimer+=drainInterval; player.TakeDamage(drainDamage); }
            }
            else
            {
                Vector3 target=player.Position;
                bool detected=school.Contains(target)&&Vector3.Distance(previous,target)<=detectionRadius&&school.ClearPath(previous,target);
                if(State==BehaviourState.Chill && detected) State=BehaviourState.Chasing;
                else if(State==BehaviourState.Chasing && (!school.Contains(target)||Vector3.Distance(previous,target)>detectionRadius*2||!school.ClearPath(previous,target))) State=BehaviourState.Chill;
                if(State==BehaviourState.Chasing)
                {
                    Vector3 delta=Vector3.MoveTowards(previous,target,chaseSpeed*dt)-previous; float allowed=delta.magnitude;
                    foreach(var hit in Physics.SphereCastAll(previous,.15f,delta.normalized,allowed,school.obstacleLayers,QueryTriggerInteraction.Ignore))
                        if(school.IsObstacle(hit.collider)) allowed=Mathf.Min(allowed,Mathf.Max(0,hit.distance-.02f));
                    transform.position=school.Clamp(previous+delta.normalized*allowed);
                    if(Vector3.Distance(transform.position,target)<=.65f && school.ClearPath(transform.position,target)) Attach(player);
                }
            }
        }
        void LateUpdate() { if(State==BehaviourState.Attached) StickToPlayer(); }
        void StickToPlayer() { if(cameraTransform) transform.SetPositionAndRotation(cameraTransform.position+cameraTransform.rotation*new Vector3((attachmentSlot%2==0?1:-1)*Mathf.Abs(attachOffset.x),attachOffset.y-(attachmentSlot/2)*.12f,attachOffset.z),cameraTransform.rotation); else if(Target) transform.position=Target.Position; }
        void Attach(PiranhaPlayerTarget player)
        {
            if (!player || !player.Alive || State == BehaviourState.Attached) return;
            var camera = player.GetComponentInChildren<Camera>();
            cameraTransform = camera ? camera.transform : player.transform;
            State=BehaviourState.Attached; Grip=maxGrip; drainTimer=drainInterval; attachmentSlot=player.GetLampreySlot(); player.AddLamprey(this); StickToPlayer();
            UpdateFeedingAudio();
        }
        public void ReduceGrip(float amount)
        {
            if(State!=BehaviourState.Attached || !Target) return;
            Grip=Mathf.Max(0,Grip-amount);
            if(Grip>0) return;
            Target.RemoveLamprey(this); State=BehaviourState.Stunned; stunTimer=stunDuration; drainTimer=drainInterval;
            StopFeedingAudio();
            Vector3 origin=Target.Position;
            Vector3 randomScatter = UnityEngine.Random.insideUnitSphere * 1.5f;
            Vector3 destination=school.Clamp(origin+Target.transform.forward*1.5f + randomScatter);
            transform.position=school.MoveWithoutObstacles(origin,destination,.15f);

        }
        public void ResetLamprey()
        {
            StopFeedingAudio();
            if(State==BehaviourState.Attached && Target) Target.RemoveLamprey(this);
            State=BehaviourState.Chill; Grip=maxGrip; stunTimer=0; drainTimer=drainInterval;
            transform.position=home;
        }
        void OnDisable() { StopFeedingAudio(); if(State==BehaviourState.Attached) ResetLamprey(); }
    }
}
