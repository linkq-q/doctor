using UnityEngine;
using UnityEngine.AI;

namespace Game.Animals
{
    public class FoxAgent : AnimalAgentBase
    {
        private enum FoxState { Patrol, Observe, Chase }

        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float chaseTimeout = 8f;

        private FoxState _state;
        private NavMeshAgent _agent;
        private float _stateTimer;
        private float _scanTimer;
        private float _chaseTimer;
        private RabbitAgent _target;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            _state = FoxState.Patrol;
            _stateTimer = Random.Range(1f, 2.5f);
            _scanTimer = 0.25f;
            _target = null;
            if (_agent)
            {
                _agent.enabled = cfg.useNavMesh;
                _agent.speed = cfg.walkSpeed;
                _agent.angularSpeed = cfg.turnSpeed;
            }
            ChoosePatrolPoint();
        }

        protected override void Tick(float dt)
        {
            _scanTimer -= dt;
            if (_scanTimer <= 0f)
            {
                _scanTimer = 0.6f;
                TryAcquireRabbit();
            }

            _stateTimer -= dt;

            switch (_state)
            {
                case FoxState.Patrol:
                    SetAnimSpeed(cfg.walkSpeed);
                    if (ReachedDestination() || _stateTimer <= 0f)
                    {
                        _state = FoxState.Observe;
                        _stateTimer = Random.Range(2f, 5f);
                    }
                    break;
                case FoxState.Observe:
                    SetAnimSpeed(0f);
                    if (_stateTimer <= 0f)
                    {
                        _state = FoxState.Patrol;
                        _stateTimer = Random.Range(1.5f, 3f);
                        ChoosePatrolPoint();
                    }
                    break;
                case FoxState.Chase:
                    UpdateChase(dt);
                    break;
            }
        }

        private void TryAcquireRabbit()
        {
            if (_state == FoxState.Chase && _target) return;
            var hits = Physics.OverlapSphere(transform.position, cfg.chaseDist, ~0, QueryTriggerInteraction.Ignore);
            RabbitAgent nearest = null;
            var nearestSqr = float.MaxValue;
            foreach (var h in hits)
            {
                if (!h.TryGetComponent(out RabbitAgent rabbit)) continue;
                var sqr = (rabbit.transform.position - transform.position).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearest = rabbit;
                    nearestSqr = sqr;
                }
            }

            if (nearest)
            {
                _target = nearest;
                _state = FoxState.Chase;
                _chaseTimer = chaseTimeout;
                SetAnimFlee(true);
            }
        }

        private void UpdateChase(float dt)
        {
            if (!_target)
            {
                GiveUp();
                return;
            }

            _chaseTimer -= dt;
            var dist = Vector3.Distance(transform.position, _target.transform.position);
            if (dist > cfg.chaseGiveUpDist || _chaseTimer <= 0f)
            {
                GiveUp();
                return;
            }

            SetAnimSpeed(cfg.runSpeed);
            if (_agent && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.speed = cfg.runSpeed;
                _agent.SetDestination(_target.transform.position);
            }

            _target.ForceFlee(transform.position);
            if (dist <= attackRange)
            {
                TriggerAttack();
            }
        }

        private void GiveUp()
        {
            _target = null;
            _state = FoxState.Patrol;
            _stateTimer = Random.Range(1.2f, 2.4f);
            SetAnimFlee(false);
            ChoosePatrolPoint();
        }

        private void ChoosePatrolPoint()
        {
            if (!zone) return;
            var offset2 = Random.insideUnitCircle * zone.radius;
            var candidate = zone.transform.position + new Vector3(offset2.x, 0f, offset2.y);
            if (_agent && _agent.enabled && _agent.isOnNavMesh && NavMesh.SamplePosition(candidate, out var hit, 5f, NavMesh.AllAreas))
            {
                _agent.speed = cfg.walkSpeed;
                _agent.SetDestination(hit.position);
            }
        }

        private bool ReachedDestination()
        {
            if (!_agent || !_agent.enabled || !_agent.isOnNavMesh) return false;
            return !_agent.pathPending && _agent.remainingDistance <= Mathf.Max(0.5f, _agent.stoppingDistance);
        }
    }
}
