using System.Collections;
using UnityEngine;

namespace Anadromo.Mechanics
{
    public sealed class FallingRockShowerSpawner : MonoBehaviour
    {
        public Transform[] ceilingAnchors;
        public Mesh rockMesh;
        public Material rockMaterial;
        public Material sedimentMaterial;
        public Material cueMaterial;
        [Range(4, 32)] public int poolSize = 18;
        [Min(.5f)] public float warningDuration = 2.2f;
        [Min(1)] public float fallSpeed = 2.4f;
        public Vector2 diameterRange = new Vector2(.25f, .7f);
        Scenario4Manager encounter;
        FallingRock[] pool;
        ParticleSystem sediment;
        AudioSource crack;
        AudioClip crackClip;
        Coroutine shower;
        readonly RaycastHit[] predictionHits = new RaycastHit[32];
        public int ActiveCount { get; private set; }
        public int ReleasedCount { get; private set; }
        public int SpawnedCount { get; private set; }
        int lastAnchor = -1;
        public void NotifyRelease() { ReleasedCount++; }

        public void Initialize(Scenario4Manager manager)
        {
            encounter = manager;
            pool = new FallingRock[poolSize];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject();
                go.name = "Roca reutilizable " + i;
                go.transform.SetParent(transform, false);
                go.SetActive(false);
                var stone = new GameObject("Roca escaneada");
                stone.transform.SetParent(go.transform, false);
                var filter = stone.AddComponent<MeshFilter>();
                filter.sharedMesh = rockMesh;
                stone.AddComponent<MeshRenderer>().sharedMaterial = rockMaterial;
                var collider = go.AddComponent<SphereCollider>();
                collider.radius = .3f;
                var body = go.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.mass = 4;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                pool[i] = go.AddComponent<FallingRock>();
                pool[i].Initialize(this, body, filter);
                for (int j = 0; j < i; j++) Physics.IgnoreCollision(collider, pool[j].GetComponent<Collider>());
            }
            var dust = new GameObject("Sedimentos de desprendimientos");
            dust.transform.SetParent(transform, false);
            sediment = dust.AddComponent<ParticleSystem>();
            sediment.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sediment.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = 220;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 2.6f; main.startSpeed = .5f;
            main.startSize = new ParticleSystem.MinMaxCurve(.05f, .24f);
            main.startColor = new Color(.3f, .55f, .5f, .4f);
            var emission = sediment.emission; emission.enabled = false;
            var renderer = sediment.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sedimentMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            crack = dust.AddComponent<AudioSource>();
            crack.spatialBlend = 1; crack.minDistance = 2; crack.maxDistance = 24; crack.volume = .3f;
            const int samples = 11025;
            var data = new float[samples];
            var random = new System.Random(41);
            for (int i = 0; i < samples; i++)
            {
                float t = i / 22050f;
                data[i] = ((float)random.NextDouble() * 2 - 1) * Mathf.Exp(-t * 12) * .4f;
            }
            crackClip = AudioClip.Create("Crujido de roca E4", samples, 1, 22050, false);
            crackClip.SetData(data, 0);
            crack.clip = crackClip;
        }

        public void StartShower()
        {
            if (shower != null || pool == null) return;
            if (sediment) sediment.Play();
            shower = StartCoroutine(Shower());
        }
        public void ResetShower()
        {
            if (shower != null) StopCoroutine(shower);
            shower = null;
            if (pool != null) foreach (var rock in pool) if (rock) rock.gameObject.SetActive(false);
            ActiveCount = 0;
            ReleasedCount = SpawnedCount = 0;
            lastAnchor = -1;
            if (sediment) sediment.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (crack) crack.Stop();
        }

        IEnumerator Shower()
        {
            while (encounter && encounter.HazardsActive)
            {
                Vector3 player = encounter.viewer.transform.position;
                int selected = -1;
                Vector3 position = Vector3.zero;
                float nearest = float.PositiveInfinity;
                // Authored ceiling sectors; looking around never moves the source of a fall.
                if (ceilingAnchors != null) for (int index = 0; index < ceilingAnchors.Length; index++)
                {
                    var candidate = ceilingAnchors[index];
                    if (!candidate || candidate.position.y < player.y + 1.5f || candidate.position.y > player.y + 12) continue;
                    Vector3 delta = candidate.position - player;
                    if (Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude > 100) continue;
                    if (!TryGetReleasePoint(candidate, out var release)) continue;
                    float distance = delta.sqrMagnitude;
                    // Prefer the next visible section, not an already passed ceiling behind the swimmer.
                    distance -= Vector3.Dot(encounter.viewer.transform.forward, delta.normalized) * 25;
                    if (index == lastAnchor) distance += 20;
                    if (distance < nearest) { nearest = distance; selected = index; position = release; }
                }
                if (selected >= 0)
                {
                    lastAnchor = selected;
                    EmitSediment(position, 24);
                    // Debris trail also makes the warning legible below the visibility ceiling.
                    EmitSediment(new Vector3(position.x, Mathf.Max(player.y + 2.5f, position.y - 3), position.z), 14);
                    crack.transform.position = position;
                    crack.pitch = Random.Range(.7f, 1);
                    crack.Play();
                    // Fix the warning before release; never move it under an escaping swimmer.
                    Vector3 landing = position + Vector3.down * 24;
                    int hits = Physics.RaycastNonAlloc(position, Vector3.down, predictionHits, 24,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    float firstContact = 24;
                    for (int i = 0; i < hits; i++)
                    {
                        var hit = predictionHits[i];
                        if (hit.collider.GetComponentInParent<Scenario4Swimmer>() || hit.collider.GetComponent<FallingRock>()) continue;
                        if (hit.distance < firstContact) { firstContact = hit.distance; landing = hit.point; }
                    }
                    // In open water mark where the rock will cross the player's original depth.
                    if (landing.y < player.y - 2) landing = new Vector3(position.x, player.y - .6f, position.z);
                    foreach (var rock in pool)
                        if (!rock.gameObject.activeSelf)
                        {
                            float diameter = Random.Range(diameterRange.x, diameterRange.y);
                            rock.Prepare(position, landing, fallSpeed * Mathf.Lerp(.85f, 1.1f,
                                Mathf.InverseLerp(diameterRange.x, diameterRange.y, diameter)), diameter, warningDuration);
                            ActiveCount++;
                            SpawnedCount++;
                            break;
                        }
                }
                yield return new WaitForSeconds(Mathf.Lerp(2.1f, 1.5f, encounter.Progress));
            }
            shower = null;
        }

        public static bool TryGetReleasePoint(Transform ceiling, out Vector3 position)
        {
            // Anchors are authored in air below real ceiling geometry. Query its underside.
            Vector3 probe = ceiling.position - Vector3.up * 1.5f;
            position = ceiling.position;
            var authoredRoof = ceiling.GetComponentInParent<MeshCollider>();
            if (authoredRoof)
            {
                // Scanned meshes may have an open underside. Bounds provide a safe fallback below it.
                position.y = authoredRoof.bounds.min.y - .6f;
                if (authoredRoof.Raycast(new Ray(probe, Vector3.up), out var underside, 4))
                    position = underside.point - Vector3.up * .6f;
            }
            else
            {
                if (!Physics.Raycast(probe, Vector3.up, out var roof, 4, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore) || !(roof.collider is MeshCollider)) return false;
                position = roof.point - Vector3.up * .6f;
            }
            // Reject fragments born inside a ledge or immediately blocked before becoming visible.
            if (Physics.CheckSphere(position, .37f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            return !Physics.SphereCast(position, .34f, Vector3.down, out _, 2.1f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        public void EmitSediment(Vector3 point, int count)
        {
            if (!sediment) return;
            var emit = new ParticleSystem.EmitParams { position = point, applyShapeToPosition = true };
            sediment.Emit(emit, count);
        }
        public void Impact(FallingRock rock, Vector3 point, bool hitPlayer)
        {
            if (!rock.gameObject.activeSelf) return;
            EmitSediment(point, 32);
            if (encounter && encounter.viewer && Vector3.Distance(point, encounter.viewer.transform.position) < 5)
                encounter.vision.AddSediment(.2f);
            if (hitPlayer && encounter) encounter.RockHit(rock.Diameter);
            Recycle(rock);
        }
        public void Recycle(FallingRock rock)
        {
            if (!rock.gameObject.activeSelf) return;
            rock.gameObject.SetActive(false);
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
        }
        void OnDisable() => ResetShower();
        void OnDestroy() { if (crackClip) Destroy(crackClip); }
    }
}
