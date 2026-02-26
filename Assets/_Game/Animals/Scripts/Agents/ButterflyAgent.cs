using UnityEngine;

namespace Game.Animals
{
    public class ButterflyAgent : AnimalAgentBase
    {
        private Vector3 _center;
        private float _seed;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _center = transform.position;
            _seed = Random.value * 1000f;
        }

        protected override void Tick(float dt)
        {
            var t = Time.time + _seed;
            var drift = new Vector3(
                Mathf.PerlinNoise(t * 0.4f, _seed) - 0.5f,
                0f,
                Mathf.PerlinNoise(_seed, t * 0.35f) - 0.5f);

            var wander = Quaternion.Euler(0f, t * 35f, 0f) * Vector3.forward;
            var desired = _center + (wander + drift).normalized * 1.5f;
            desired.y = _center.y + Mathf.Lerp(0.5f, 2f, Mathf.PerlinNoise(t * 0.25f, _seed * 0.4f));

            var playerDist = Mathf.Sqrt(sqrDistToPlayer);
            var speed = cfg.walkSpeed;
            if (playerDist < cfg.fleeDist)
            {
                var away = (transform.position - player.position).normalized + Random.insideUnitSphere * 0.35f;
                away.y = 0.2f;
                desired = transform.position + away.normalized * 2f;
                speed = cfg.runSpeed;
            }

            transform.position = Vector3.Lerp(transform.position, desired, dt * speed);
            var lookDir = desired - transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                var targetRot = Quaternion.LookRotation(lookDir.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, cfg.turnSpeed * dt);
            }
        }
    }
}
