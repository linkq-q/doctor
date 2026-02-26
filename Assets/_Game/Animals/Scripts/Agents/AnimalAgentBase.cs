using System.Collections.Generic;
using UnityEngine;

namespace Game.Animals
{
    public abstract class AnimalAgentBase : MonoBehaviour
    {
        protected AnimalSpeciesConfig cfg;
        protected Animator anim;
        protected Transform player;
        protected Camera mainCam;
        protected AnimalPool pool;
        protected AnimalSpawnZone zone;
        protected AnimalSpawnManager manager;

        protected bool isSleeping;
        protected float sqrDistToPlayer;

        private readonly HashSet<string> _missingAnimParams = new();
        private float _sleepProbeTimer;

        public void Init(AnimalSpeciesConfig config, Transform playerTransform, Camera cam, AnimalPool p, AnimalSpawnZone z, AnimalSpawnManager m)
        {
            cfg = config;
            player = playerTransform;
            mainCam = cam;
            pool = p;
            zone = z;
            manager = m;
            anim = GetComponentInChildren<Animator>(true);
        }

        public virtual void OnSpawn()
        {
            isSleeping = false;
            _sleepProbeTimer = 0f;
        }

        public virtual void OnDespawn()
        {
        }

        public void ManagedTick(float dt)
        {
            if (!player || cfg == null)
            {
                return;
            }

            sqrDistToPlayer = (transform.position - player.position).sqrMagnitude;
            var dist = Mathf.Sqrt(sqrDistToPlayer);

            if (dist > cfg.despawnDist)
            {
                manager.RequestDespawn(this);
                return;
            }

            ApplyLOD(dist, dt);

            if (isSleeping)
            {
                return;
            }

            Tick(dt);
        }

        protected abstract void Tick(float dt);

        protected virtual void ApplyLOD(float distance, float dt)
        {
            if (anim)
            {
                anim.enabled = distance <= cfg.stopAnimatorDist;
            }

            if (distance > cfg.sleepDist)
            {
                _sleepProbeTimer -= dt;
                if (_sleepProbeTimer <= 0f)
                {
                    _sleepProbeTimer = 0.5f;
                    isSleeping = true;
                }
            }
            else
            {
                isSleeping = false;
                _sleepProbeTimer = 0f;
            }
        }

        protected bool HasAnimParam(string param, AnimatorControllerParameterType type)
        {
            if (anim == null || string.IsNullOrWhiteSpace(param))
            {
                return false;
            }

            foreach (var p in anim.parameters)
            {
                if (p.name == param && p.type == type)
                {
                    return true;
                }
            }

            if (_missingAnimParams.Add(param))
            {
                Debug.LogWarning($"[{name}] Missing animator param '{param}' on species '{cfg.speciesId}'. Fallback to default loop.");
            }

            return false;
        }

        protected void SetAnimSpeed(float value)
        {
            if (HasAnimParam(cfg.speedParam, AnimatorControllerParameterType.Float))
            {
                anim.SetFloat(cfg.speedParam, value);
            }
        }

        protected void SetAnimFlee(bool value)
        {
            if (HasAnimParam(cfg.fleeBoolParam, AnimatorControllerParameterType.Bool))
            {
                anim.SetBool(cfg.fleeBoolParam, value);
            }
        }

        protected void SetAnimIdleVariant(int variant)
        {
            if (HasAnimParam(cfg.idleVariantParam, AnimatorControllerParameterType.Int))
            {
                anim.SetInteger(cfg.idleVariantParam, variant);
            }
        }

        protected void TriggerAttack()
        {
            if (HasAnimParam(cfg.attackTriggerParam, AnimatorControllerParameterType.Trigger))
            {
                anim.SetTrigger(cfg.attackTriggerParam);
            }
        }
    }
}
