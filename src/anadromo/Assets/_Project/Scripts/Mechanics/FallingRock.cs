using UnityEngine;

namespace Anadromo.Mechanics
{
    public sealed class FallingRock : MonoBehaviour
    {
        FallingRockShowerSpawner owner;
        Rigidbody body;
        float meshSize, age, countdown, fallSpeed;
        Vector3 destination;
        Transform meshVisual;
        Vector3 meshCenter;
        public float Diameter { get; private set; }
        public bool IsFalling { get; private set; }
        public Vector3 PredictedLanding => destination;

        public void Initialize(FallingRockShowerSpawner spawner, Rigidbody rigidbody, MeshFilter filter)
        {
            owner = spawner; body = rigidbody;
            meshVisual = filter.transform;
            Bounds bounds = filter.sharedMesh.bounds;
            meshCenter = bounds.center;
            meshSize = Mathf.Max(.001f, bounds.size.x, bounds.size.y, bounds.size.z);
        }

        public void Prepare(Vector3 position, Vector3 predictedLanding, float speed, float diameter, float warning)
        {
            destination = predictedLanding;
            Diameter = diameter; fallSpeed = speed; countdown = warning;
            transform.SetPositionAndRotation(position, Random.rotation);
            transform.localScale = Vector3.one;
            meshVisual.localScale = Vector3.one * (diameter / meshSize);
            meshVisual.localPosition = -meshCenter * (diameter / meshSize);
            GetComponent<SphereCollider>().radius = diameter * .48f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            body.position = position; body.rotation = transform.rotation;
            age = 0; IsFalling = false;
            gameObject.SetActive(true);
        }

        void Update()
        {
            age += Time.deltaTime;
            if (!IsFalling)
            {
                countdown -= Time.deltaTime;
                if (countdown <= 0)
                {
                    body.isKinematic = false;
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    body.linearVelocity = Vector3.down * fallSpeed;
                    body.angularVelocity = Random.insideUnitSphere * 1.2f;
                    IsFalling = true;
                    owner.NotifyRelease();
                }
            }
            if (age >= 18) owner.Recycle(this);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsFalling) return;
            owner.Impact(this, collision.contactCount > 0 ? collision.GetContact(0).point : transform.position,
                collision.gameObject.GetComponentInParent<Scenario4Swimmer>() != null);
        }
        public void HitSwimmer() { if (IsFalling) owner.Impact(this, transform.position, true); }
    }
}
