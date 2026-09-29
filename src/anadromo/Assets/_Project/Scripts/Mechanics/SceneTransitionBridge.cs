using UnityEngine;
using UnityEngine.UI;

namespace Anadromo.Logic
{
    /// <summary>Passes a one-use, text-free fade from the cave ending into esc2.</summary>
    public static class SceneTransitionBridge
    {
        static bool awaitingArrival;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { awaitingArrival = false; }

        public static void ExpectArrival() { awaitingArrival = true; }

        public static void FadeInIfExpected(Camera viewer)
        {
            if (!awaitingArrival || !viewer) return;
            awaitingArrival = false;

            var curtain = new GameObject("Scene arrival fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var canvas = curtain.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = viewer;
            canvas.sortingOrder = 32760;
            curtain.transform.SetParent(viewer.transform, false);
            curtain.transform.localPosition = new Vector3(0, 0, .7f);
            curtain.transform.localRotation = Quaternion.identity;
            curtain.transform.localScale = Vector3.one * .001f;
            curtain.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 1000);

            var image = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(curtain.transform, false);
            image.rectTransform.sizeDelta = new Vector2(3000, 3000);
            image.color = Color.black;
            image.raycastTarget = false;

            var group = curtain.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            curtain.AddComponent<SceneArrivalFade>().Initialize(group);
        }
    }

    public sealed class SceneArrivalFade : MonoBehaviour
    {
        const float Duration = 1.5f;
        CanvasGroup group;
        float elapsed;

        public void Initialize(CanvasGroup fadeGroup) { group = fadeGroup; }

        void Update()
        {
            if (!group) return;
            elapsed += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(elapsed / Duration);
            if (elapsed >= Duration) Destroy(gameObject);
        }
    }
}
