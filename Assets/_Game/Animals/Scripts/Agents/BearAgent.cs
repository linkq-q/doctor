using UnityEngine;
using UnityEngine.AI;

namespace Game.Animals
{
    public class BearAgent : AnimalAgentBase
    {
        private enum BearState { Wander, Chase, PostCatchIdle }

        [SerializeField] private LayerMask preyMask = ~0;
        [SerializeField] private float scanInterval = 0.2f;

        private readonly Collider[] _scanHits = new Collider[32];
        private BearState _state;
        private NavMeshAgent _agent;
        private DeerAgent _target;
        private float _scanTimer;
        private float _stateTimer;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            _state = BearState.Wander;
            _scanTimer = 0f;
            _stateTimer = Random.Range(1f, 2.5f);
            _target = null;

            if (_agent)
            {
                _agent.enabled = cfg.useNavMesh;
                _agent.speed = cfg.walkSpeed;
                _agent.angularSpeed = cfg.turnSpeed;
            }

            ChooseWanderPoint();
        }

        protected override void Tick(float dt)
        {
            _scanTimer -= dt;
            if (_scanTimer <= 0f)
            {
                _scanTimer = scanInterval;
                TryAcquirePrey();
            }

            _stateTimer -= dt;
            switch (_state)
            {
                case BearState.Wander:
                    SetAnimFlee(false);
                    SetAnimSpeed(cfg.walkSpeed);
                    if (ReachedDestination() || _stateTimer <= 0f)
                    {
                        _stateTimer = Random.Range(1f, 2.5f);
                        ChooseWanderPoint();
                    }
                    break;

                case BearState.Chase:
                    ChaseTick(dt);
                    break;

                case BearState.PostCatchIdle:
                    SetAnimSpeed(0f);
                    if (_stateTimer <= 0f)
                    {
                        _state = BearState.Wander;
                        _stateTimer = Random.Range(1f, 2.5f);
                        ChooseWanderPoint();
                    }
                    break;
            }
        }

        private void TryAcquirePrey()
        {
            if (_state == BearState.Chase && _target) return;

            var detectRadius = Mathf.Max(1f, cfg.bearDetectRadius);
            var count = Physics.OverlapSphereNonAlloc(transform.position, detectRadius, _scanHits, preyMask, QueryTriggerInteraction.Ignore);
            DeerAgent nearest = null;
            var nearestSqr = float.MaxValue;

            for (var i = 0; i < count; i++)
            {
                var hit = _scanHits[i];
                if (!hit || !hit.TryGetComponent(out DeerAgent deer)) continue;
                var sqr = (deer.transform.position - transform.position).sqrMagnitude;
                if (sqr >= nearestSqr) continue;
                nearest = deer;
                nearestSqr = sqr;
            }

            if (!nearest) return;

            _target = nearest;
            _state = BearState.Chase;
            _stateTimer = cfg.bearMaxChaseTime;
            _target.PanicFrom(transform.position);
        }

        private void ChaseTick(float dt)
        {
            if (!_target)
            {
                ReturnToWander();
                return;
            }

            _stateTimer -= dt;
            var dist = Vector3.Distance(transform.position, _target.transform.position);
            if (_stateTimer <= 0f || dist > cfg.bearGiveUpRadius)
            {
                ReturnToWander();
                return;
            }

            var chaseSpeed = Mathf.Max(cfg.runSpeed, cfg.walkSpeed + 0.5f);
            SetAnimFlee(true);
            SetAnimSpeed(chaseSpeed);

            _target.PanicFrom(transform.position);

            if (_agent && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.speed = chaseSpeed;
                _agent.SetDestination(_target.transform.position);
            }

            if (dist <= cfg.bearCatchDistance)
            {
                TriggerAttack();
                manager.RequestDespawn(_target);
                _target = null;
                _state = BearState.PostCatchIdle;
                _stateTimer = Mathf.Max(0.2f, cfg.postCatchIdleSeconds);
                SetAnimFlee(false);
            }
        }

        private void ReturnToWander()
        {
            _target = null;
            _state = BearState.Wander;
            _stateTimer = Random.Range(1f, 2.5f);
            SetAnimFlee(false);
            ChooseWanderPoint();
        }

        private void ChooseWanderPoint()
        {
            var radius = zone ? zone.radius : Mathf.Max(8f, cfg.alertDist);
            var rnd = Random.insideUnitCircle * radius;
            var target = (zone ? zone.transform.position : transform.position) + new Vector3(rnd.x, 0f, rnd.y);

            if (_agent && _agent.enabled && _agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(target, out var hit, 5f, NavMesh.AllAreas))
                {
                    _agent.speed = cfg.walkSpeed;
                    _agent.SetDestination(hit.position);
                }
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, target, cfg.walkSpeed * Time.deltaTime);
        }

        private bool ReachedDestination()
        {
            if (!_agent || !_agent.enabled || !_agent.isOnNavMesh) return false;
            return !_agent.pathPending && _agent.remainingDistance <= Mathf.Max(0.5f, _agent.stoppingDistance);
        }
    }
}
