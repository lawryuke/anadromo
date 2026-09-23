using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Anadromo.Logic
{
    [DisallowMultipleComponent]
    public sealed class LevelEndingEffects : MonoBehaviour
    {
        [Header("Temblor")]
        [Min(0)] public float shakeAmplitude = .025f;
        [Min(.1f)] public float shakeFrequency = 12;
        [Header("Derrumbe")]
        public Mesh rockMesh;
        public Material rockMaterial;
        [Min(1)] public int rockCount = 12;
        [Min(.01f)] public float rockInterval = .18f;
        [Min(.5f)] public float spawnHeight = 2.5f;
        [Min(.1f)] public float rockRadius = 1.8f;
        [Min(.1f)] public float fallDuration = 3;
        [Min(.1f)] public float fadeDuration = 2;
        public bool IsComplete { get; private set; }
        public bool IsTrembling => trembling;
        public int SpawnedRocks { get; private set; }
        bool trembling, endingStarted;
        Transform shakeRoot, originalParent;
        Camera viewer;
        GameObject darkness;
        CanvasGroup curtain;
        readonly List<GameObject> rocks = new List<GameObject>();
        Vector3 shakeOrigin;

        public void StartTrembling(Camera camera)
        {
            if (endingStarted || trembling || camera == null) return;
            viewer = camera;
            originalParent = camera.transform.parent;
            var pivot = new GameObject("Temblor de cámara");
            shakeRoot = pivot.transform;
            shakeRoot.SetParent(originalParent, false);
            camera.transform.SetParent(shakeRoot, false);
            shakeOrigin = shakeRoot.localPosition;
            trembling = true;
        }

        void LateUpdate()
        {
            if (!trembling || shakeRoot == null) return;
            float t = Time.time * shakeFrequency;
            shakeRoot.localPosition = shakeOrigin + new Vector3(
                Mathf.Sin(t * 1.13f), Mathf.Sin(t * 1.71f), Mathf.Sin(t * .83f)) * shakeAmplitude;
        }

        void StopTrembling()
        {
            trembling = false;
            if (shakeRoot != null) shakeRoot.localPosition = shakeOrigin;
        }

        public void BeginRockfall(Camera camera)
        {
            if (endingStarted || camera == null) return;
            viewer = camera;
            endingStarted = true;
            StopTrembling();
            StartCoroutine(Rockfall());
        }

        IEnumerator Rockfall()
        {
            // The impact area is fixed when the collapse starts.
            Vector3 center = viewer.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            for (int i = 0; i < Mathf.Max(1, rockCount); i++)
            {
                Vector2 scatter = Random.insideUnitCircle * rockRadius;
                Vector3 position = center + forward * 1.2f +
                    new Vector3(scatter.x, spawnHeight + Random.Range(0f, .8f), scatter.y);
                var rock = new GameObject("Roca derrumbe " + (i + 1));
                rock.transform.SetPositionAndRotation(position, Random.rotation);
                var filter = rock.AddComponent<MeshFilter>();
                var renderer = rock.AddComponent<MeshRenderer>();
                if (rockMesh != null)
                {
                    filter.sharedMesh = rockMesh;
                    // Imported rock meshes can be huge; normalize each piece to world metres.
                    float length = Mathf.Max(.001f, rockMesh.bounds.size.magnitude);
                    rock.transform.localScale = Vector3.one * Random.Range(.4f, .8f) / length;
                }
                else
                {
                    var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    filter.sharedMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
                    Destroy(primitive);
                    rock.transform.localScale = Vector3.one * Random.Range(.2f, .45f);
                }
                renderer.sharedMaterial = rockMaterial;
                var collider = rock.AddComponent<SphereCollider>();
                collider.center = filter.sharedMesh.bounds.center;
                collider.radius = filter.sharedMesh.bounds.extents.magnitude * .7f;
                var body = rock.AddComponent<Rigidbody>();
                body.mass = 2;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.angularVelocity = Random.insideUnitSphere * 3;
                body.linearVelocity = Vector3.down * 1.5f;
                rocks.Add(rock);
                SpawnedRocks++;
                yield return new WaitForSeconds(rockInterval);
            }
            yield return new WaitForSeconds(fallDuration);
            CreateCurtain();
            float elapsed = 0;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                curtain.alpha = Mathf.Clamp01(elapsed / Mathf.Max(.1f, fadeDuration));
                yield return null;
            }
            curtain.alpha = 1;
            IsComplete = true;
        }

        void CreateCurtain()
        {
            darkness = new GameObject("Oscuridad final", typeof(Canvas), typeof(CanvasGroup));
            Canvas canvas = darkness.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = viewer;
            canvas.planeDistance = viewer.nearClipPlane + .05f;
            canvas.sortingOrder = 32760;
            curtain = darkness.GetComponent<CanvasGroup>();
            curtain.alpha = 0;
            curtain.blocksRaycasts = false;
            var panel = new GameObject("Negro", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(darkness.transform, false);
            var image = panel.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void OnDisable()
        {
            StopAllCoroutines();
            StopTrembling();
            if (viewer != null && shakeRoot != null && viewer.transform.parent == shakeRoot)
                viewer.transform.SetParent(originalParent, false);
            if (shakeRoot != null) Destroy(shakeRoot.gameObject);
            if (darkness != null) Destroy(darkness);
            foreach (var rock in rocks) if (rock != null) Destroy(rock);
            rocks.Clear();
        }
    }
}
