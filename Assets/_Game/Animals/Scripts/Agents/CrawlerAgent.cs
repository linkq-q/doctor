using System.Collections;
using UnityEngine;

namespace Game.Animals
{
    public class CrawlerAgent : AnimalAgentBase
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Vector2 idleTimeRange = new(0.8f, 1.8f);
        [SerializeField] private float vanishAfterFleeSeconds = 2.2f;
        [SerializeField] private float fadeDuration = 0.5f;

        private Vector3 _moveDir;
        private float _stateTimer;
        private bool _moving;
        private float _fleeTimer;
        private bool _vanishing;
        private MaterialPropertyBlock _propertyBlock;

        public override void OnSpawn()
        {
            base.OnSpawn();
            StopAllCoroutines();
            _stateTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
            _moving = false;
            _fleeTimer = 0f;
            _vanishing = false;
            ApplyFadeAlpha(1f);
        }

        protected override void Tick(float dt)
        {
            if (_vanishing) return;
            var dist = Mathf.Sqrt(sqrDistToPlayer);
            var fleeing = dist < cfg.fleeDist;
            var speed = cfg.walkSpeed;

            if (fleeing)
            {
                _fleeTimer += dt;
                _moveDir = (transform.position - player.position).normalized;
                speed = cfg.runSpeed;
                if (_fleeTimer >= vanishAfterFleeSeconds)
                {
                    StartCoroutine(VanishRoutine());
                    return;
                }
            }
            else
            {
                _fleeTimer = 0f;
                _stateTimer -= dt;
                if (_stateTimer <= 0f)
                {
                    _moving = !_moving;
                    _stateTimer = _moving ? Random.Range(0.6f, 1.4f) : Random.Range(idleTimeRange.x, idleTimeRange.y);
                    if (_moving)
                    {
                        var v = Random.insideUnitCircle.normalized;
                        _moveDir = new Vector3(v.x, 0f, v.y);
                    }
                }
                if (!_moving)
                {
                    SetAnimSpeed(0f);
                    return;
                }
            }

            var target = transform.position + _moveDir * speed * dt;
            if (Physics.Raycast(target + Vector3.up * 1f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                target = hit.point;
            }
            transform.position = target;
            if (_moveDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_moveDir), cfg.turnSpeed * dt);
            }
            SetAnimSpeed(speed);
        }

        private IEnumerator VanishRoutine()
        {
            _vanishing = true;

            var t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                var alpha = 1f - Mathf.Clamp01(t / fadeDuration);
                ApplyFadeAlpha(alpha);
                yield return null;
            }

            manager.RequestDespawn(this);
        }

        private void ApplyFadeAlpha(float alpha)
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                var sharedMaterials = renderer.sharedMaterials;
                for (var materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    var mat = sharedMaterials[materialIndex];
                    if (!mat || !mat.HasProperty(ColorId)) continue;

                    renderer.GetPropertyBlock(_propertyBlock, materialIndex);
                    var color = mat.color;
                    color.a = alpha;
                    _propertyBlock.SetColor(ColorId, color);
                    renderer.SetPropertyBlock(_propertyBlock, materialIndex);
                }
            }
        }
    }
}
