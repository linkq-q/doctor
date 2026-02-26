using UnityEngine;
using UnityEngine.AI;

namespace Game.Animals
{
    public class RabbitAgent : AnimalAgentBase
    {
        private enum RabbitState { Idle, Wander, Flee }

        private RabbitState _state;
        private NavMeshAgent _agent;
        private float _stateTimer;
        private Vector3 _fleeFrom;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            _state = RabbitState.Idle;
            _stateTimer = Random.Range(1f, 2.5f);
            SetAnimIdleVariant(Random.Range(0, 3));

            if (_agent)
            {
                _agent.enabled = cfg.useNavMesh;
                _agent.speed = cfg.walkSpeed;
                _agent.angularSpeed = cfg.turnSpeed;
            }
        }

        public void ForceFlee(Vector3 from)
        {
            _fleeFrom = from;
            _state = RabbitState.Flee;
            _stateTimer = 2.5f;
            SetAnimFlee(true);
            MoveToFleePoint();
        }

        protected override void Tick(float dt)
        {
            if (Mathf.Sqrt(sqrDistToPlayer) < cfg.fleeDist)
            {
                ForceFlee(player.position);
            }

            _stateTimer -= dt;
            switch (_state)
            {
                case RabbitState.Idle:
                    SetAnimSpeed(0f);
                    if (_stateTimer <= 0f)
                    {
                        _state = RabbitState.Wander;
                        _stateTimer = Random.Range(1.2f, 3f);
                        MoveRandom(cfg.alertDist * 0.6f, cfg.walkSpeed);
                    }
                    break;
                case RabbitState.Wander:
                    SetAnimFlee(false);
                    SetAnimSpeed(cfg.walkSpeed);
                    if (_stateTimer <= 0f || ReachedDestination())
                    {
                        _state = RabbitState.Idle;
                        _stateTimer = Random.Range(0.8f, 1.8f);
                    }
                    break;
                case RabbitState.Flee:
                    SetAnimFlee(true);
                    SetAnimSpeed(cfg.runSpeed);
                    if (_stateTimer <= 0f || ReachedDestination())
                    {
                        _state = RabbitState.Idle;
                        _stateTimer = Random.Range(1f, 2f);
                        SetAnimFlee(false);
                    }
                    break;
            }
        }

        private void MoveToFleePoint()
        {
            var away = (transform.position - _fleeFrom).normalized;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            var desired = transform.position + (away + Random.insideUnitSphere * 0.35f).normalized * cfg.alertDist;
            MoveTo(desired, cfg.runSpeed);
        }

        private void MoveRandom(float radius, float speed)
        {
            var rand = Random.insideUnitSphere * radius;
            rand.y = 0f;
            MoveTo(transform.position + rand, speed);
        }

        private void MoveTo(Vector3 world, float speed)
        {
            if (_agent && _agent.enabled && _agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(world, out var hit, 4f, NavMesh.AllAreas))
                {
                    _agent.speed = speed;
                    _agent.SetDestination(hit.position);
                }
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, world, speed * Time.deltaTime);
        }

        private bool ReachedDestination()
        {
            if (!_agent || !_agent.enabled || !_agent.isOnNavMesh)
            {
                return false;
            }

            return !_agent.pathPending && _agent.remainingDistance <= Mathf.Max(0.3f, _agent.stoppingDistance);
        }
    }
}
