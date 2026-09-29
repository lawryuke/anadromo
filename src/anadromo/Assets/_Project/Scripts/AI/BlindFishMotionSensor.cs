using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

namespace Anadromo.AI
{
    [DisallowMultipleComponent]
    public sealed class BlindFishMotionSensor : MonoBehaviour
    {
        public float vrNoisySpeed=1.5f, stillBodySpeed=.06f;
        public float stillHeadSpeed=.04f, stillHandSpeed=.08f;
        public float stillHeadAngularSpeed=8, stillHandAngularSpeed=15;
        public float quietHold=.2f, smoothingTime=.1f;
        public bool MotionValid { get; private set; }=true;
        public bool IsNoisy { get; private set; }
        public bool IsStill { get; private set; }
        public float Speed { get; private set; }
        Rigidbody body;
        Vector3 previousPosition;
        float quietTime;
        readonly Vector3[] positions=new Vector3[3];
        readonly Quaternion[] rotations=new Quaternion[3];
        readonly bool[] tracked=new bool[3];
        readonly float[] linear=new float[3],angular=new float[3];
        static readonly XRNode[] nodes={XRNode.Head,XRNode.LeftHand,XRNode.RightHand};
        readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
        bool ReadTrackedPose(int index, out Vector3 p, out Quaternion r)
        {
            p=default; r=Quaternion.identity;
            var device=InputDevices.GetDeviceAtXRNode(nodes[index]);
            if(device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool isTracked) && isTracked &&
                device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition,out p) &&
                device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation,out r)) return true;
            if(index==0) return false;
            // Quest hand tracking does not guarantee LeftHand/RightHand InputDevices.
            SubsystemManager.GetSubsystems(handSubsystems);
            foreach(var subsystem in handSubsystems)
            {
                if(!subsystem.running) continue;
                var hand=index==1 ? subsystem.leftHand : subsystem.rightHand;
                if(hand.isTracked && hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose pose))
                { p=pose.position; r=pose.rotation; return true; }
            }
            return false;
        }
        void Awake() { body=GetComponent<Rigidbody>(); previousPosition=transform.position; }
        void Update()
        {
            float dt=Time.deltaTime;
            if(dt<=0) return;
            Speed=body ? body.linearVelocity.magnitude : Vector3.Distance(previousPosition,transform.position)/dt;
            previousPosition=transform.position;
            bool still=Speed<stillBodySpeed;
            MotionValid=true;
            if(XRSettings.isDeviceActive)
            {
                IsNoisy=Speed>=vrNoisySpeed;
                float blend=1-Mathf.Exp(-dt/Mathf.Max(.01f,smoothingTime));
                for(int i=0;i<nodes.Length;i++)
                {
                    bool valid=ReadTrackedPose(i,out Vector3 p,out Quaternion r);
                    if(!valid) { tracked[i]=false; MotionValid=false; continue; }
                    if(tracked[i])
                    {
                        linear[i]=Mathf.Lerp(linear[i],Vector3.Distance(positions[i],p)/dt,blend);
                        angular[i]=Mathf.Lerp(angular[i],Quaternion.Angle(rotations[i],r)/dt,blend);
                        still &= linear[i]<(i==0?stillHeadSpeed:stillHandSpeed) && angular[i]<(i==0?stillHeadAngularSpeed:stillHandAngularSpeed);
                    }
                    else { linear[i]=angular[i]=0; still=false; }
                    positions[i]=p; rotations[i]=r; tracked[i]=true;
                }
            }
            else
            {
                var k=Keyboard.current;
                bool moving=k!=null && (k.wKey.isPressed || k.sKey.isPressed || k.aKey.isPressed || k.dKey.isPressed || k.qKey.isPressed || k.eKey.isPressed);
                IsNoisy=moving && k!=null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed) && Speed>.03f;
                still &= !moving;
            }
            quietTime=MotionValid && still ? quietTime+dt : 0;
            IsStill=quietTime>=quietHold;
        }
        public void ResetMotion()
        {
            previousPosition=transform.position; quietTime=0; IsStill=IsNoisy=false;
            System.Array.Clear(tracked,0,tracked.Length);
        }
    }
}
