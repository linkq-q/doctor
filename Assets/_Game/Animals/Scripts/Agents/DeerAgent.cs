using UnityEngine;
using UnityEngine.AI;

namespace Game.Animals
{
    public class DeerAgent : AnimalAgentBase
    {
        private enum DeerState { Wander, Flee }

        private DeerState _state;
        private NavMeshAgent _agent;
        private float _stateTimer;
        private Vector3 _fleeSource;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            _state = DeerState.Wander;
            _stateTimer = Random.Range(0.5f, 2f);

            if (_agent)
            {
                _agent.enabled = cfg.useNavMesh;
                _agent.speed = cfg.walkSpeed;
                _agent.angularSpeed = cfg.turnSpeed;
            }

            ChooseWanderPoint();
        }

        public void PanicFrom(Vector3 source)
        {
            _state = DeerState.Flee;
            _fleeSource = source;
            _stateTimer = cfg.deerFleeDurationSeconds;
            SetAnimFlee(true);
            MoveToFleePoint();
        }

        protected override void Tick(float dt)
        {
            if (Mathf.Sqrt(sqrDistToPlayer) < cfg.fleeDist)
            {
                PanicFrom(player.position);
            }

            _stateTimer -= dt;
            switch (_state)
            {
                case DeerState.Wander:
                    SetAnimFlee(false);
                    SetAnimSpeed(cfg.walkSpeed);
                    if (ReachedDestination() || _stateTimer <= 0f)
                    {
                        ChooseWanderPoint();
                        _stateTimer = Random.Range(1f, 2.6f);
                    }
                    break;

                case DeerState.Flee:
                    var runSpeed = cfg.runSpeed * Mathf.Max(1f, cfg.deerFleeSpeedMultiplier);
                    SetAnimFlee(true);
                    SetAnimSpeed(runSpeed);
                    if (_agent && _agent.enabled)
                    {
                        _agent.speed = runSpeed;
                    }

                    if (ReachedDestination() || _stateTimer <= 0f)
                    {
                        _state = DeerState.Wander;
                        _stateTimer = Random.Range(1f, 2.5f);
                        SetAnimFlee(false);
                        ChooseWanderPoint();
                    }
                    break;
            }
        }

        private void ChooseWanderPoint()
        {
            var radius = Mathf.Max(4f, cfg.alertDist);
            var rnd = Random.insideUnitCircle * radius;
            var target = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            MoveTo(target, cfg.walkSpeed);
        }

        private void MoveToFleePoint()
        {
            var away = (transform.position - _fleeSource).normalized;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            var target = transform.position + (away + Random.insideUnitSphere * 0.25f).normalized * Mathf.Max(8f, cfg.alertDist);
            MoveTo(target, cfg.runSpeed * Mathf.Max(1f, cfg.deerFleeSpeedMultiplier));
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
            if (!_agent || !_agent.enabled || !_agent.isOnNavMesh) return false;
            return !_agent.pathPending && _agent.remainingDistance <= Mathf.Max(0.4f, _agent.stoppingDistance);
        }
    }
}
