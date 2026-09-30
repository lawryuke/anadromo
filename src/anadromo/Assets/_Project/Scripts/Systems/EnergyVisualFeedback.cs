using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Anadromo.Systems
{
    // Own runtime profile: never edits the cave's saved lighting/volume assets.
    public class EnergyVisualFeedback : MonoBehaviour
    {
        public float maximumVignette=.48f;
        PlayerEnergyController player;
        Anadromo.Locomotion.ComfortVignette comfort;
        Anadromo.Mechanics.LampreyShakeController struggle;
        Volume volume;
        VolumeProfile profile;
        Vignette vignette;
        ColorAdjustments color;
        UniversalAdditionalCameraData cameraData;
        bool previousPostProcessing;
        public float ExhaustionIntensity => vignette==null ? 0 : vignette.intensity.value;
        public float Saturation => color==null ? 0 : color.saturation.value;
        protected virtual void Start()
        {
            player=GetComponent<PlayerEnergyController>();
            comfort=GetComponent<Anadromo.Locomotion.ComfortVignette>();
            struggle=GetComponent<Anadromo.Mechanics.LampreyShakeController>();
            var camera=GetComponentInChildren<Camera>();
            if(!player || !camera) { Debug.LogError($"[EnergyVisualFeedback] Falla: player={player!=null}, camera={camera!=null}", this); enabled=false; return; }
            cameraData=camera.GetUniversalAdditionalCameraData();
            previousPostProcessing=cameraData.renderPostProcessing; cameraData.renderPostProcessing=true;
            var go=new GameObject("Energy Vital Feedback (Runtime)"); go.transform.SetParent(transform,false);
            // Pick a layer already sampled by this camera's volume mask.
            for(int layer=0;layer<32;layer++) if((cameraData.volumeLayerMask.value & (1<<layer))!=0) { go.layer=layer; break; }
            volume=go.AddComponent<Volume>(); volume.isGlobal=true; volume.priority=110;
            profile=ScriptableObject.CreateInstance<VolumeProfile>(); volume.sharedProfile=profile;
            vignette=profile.Add<Vignette>(true); vignette.smoothness.Override(.8f); vignette.rounded.Override(true);
            color=profile.Add<ColorAdjustments>(); color.saturation.overrideState=true;
            ApplyFeedback();
        }
        protected virtual void LateUpdate() { ApplyFeedback(); }
        public void ApplyFeedback()
        {
            if(!player || !vignette) return;
            float exhaustion=1-player.Fraction;
            float impact=player.ImpactPulse,recovery=player.RecoveryPulse;
            // color negro para cansancio y naranja cuando come kril
            var shade=Color.Lerp(new Color(.015f,.035f,.07f),new Color(1,.62f,.16f),recovery);
            // color amarillo enfermizo, naranja para comer
            // var shade = Color.Lerp(new Color(0.36f, 0.41f, 0.06f), new Color(1, 0.62f, 0.16f), recovery);
            shade =Color.Lerp(shade,Color.white,impact);
            if ((struggle && struggle.IsStruggling) || player.Current <= 0f)
            {
                vignette.intensity.Override(comfort ? comfort.Intensity : 0f);
                vignette.color.Override(new Color(.015f, .035f, .07f));
                color.saturation.Override(0f);
                return;
            }
            vignette.color.Override(shade);
            vignette.intensity.Override(Mathf.Max(comfort ? comfort.Intensity : 0f,
                Mathf.Clamp(exhaustion*maximumVignette+impact*.18f+recovery*.08f,0,.65f)));
            color.saturation.Override(-100*exhaustion);
        }
        protected virtual void OnDisable() { if(volume) volume.enabled=false; }
        protected virtual void OnEnable() { if(volume) volume.enabled=true; }
        protected virtual void OnDestroy()
        {
            if(cameraData) cameraData.renderPostProcessing=previousPostProcessing;
            if(volume) Destroy(volume.gameObject);
            if(profile) { foreach(var component in profile.components) if(component) Destroy(component); Destroy(profile); }
        }
    }
}
