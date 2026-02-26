using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Animals
{
    public class AnimalSpawnManager : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AnimalPool pool;
        [SerializeField] private float tickInterval = 0.2f;
        [SerializeField] private int maxOpsPerTick = 8;
        [SerializeField] private int prewarmPerSpecies = 3;

        private readonly List<AnimalSpawnZone> _zones = new();
        private readonly List<AnimalAgentBase> _agents = new();
        private readonly Dictionary<AnimalAgentBase, InstanceRecord> _records = new();
        private readonly Dictionary<string, int> _aliveBySpecies = new();

        private float _tickTimer;

        public Vector3 LastSpawnPosition { get; private set; }
        public string LastSpawnSpeciesId { get; private set; } = "-";

        public IReadOnlyDictionary<string, int> AliveBySpecies => _aliveBySpecies;
        public IReadOnlyList<AnimalSpawnZone> Zones => _zones;
        public int TotalAlive => _agents.Count;

        private struct InstanceRecord
        {
            public AnimalSpeciesConfig cfg;
            public AnimalSpawnZone zone;
            public GameObject instance;
        }

        private void Awake()
        {
            if (!pool)
            {
                pool = FindAnyObjectByType<AnimalPool>();
            }

            if (!player)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged) player = tagged.transform;
            }

            if (!playerCamera)
            {
                playerCamera = Camera.main;
            }

            _zones.AddRange(FindObjectsByType<AnimalSpawnZone>(FindObjectsSortMode.None));
            Prewarm();
        }

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer < tickInterval)
            {
                return;
            }

            var dt = _tickTimer;
            _tickTimer = 0f;

            var opCount = 0;
            for (var i = _agents.Count - 1; i >= 0 && opCount < maxOpsPerTick; i--)
            {
                if (!_agents[i])
                {
                    _agents.RemoveAt(i);
                    continue;
                }
                _agents[i].ManagedTick(dt);
                opCount++;
            }

            foreach (var zone in _zones)
            {
                if (opCount >= maxOpsPerTick) break;
                TrySpawnInZone(zone, ref opCount);
            }
        }

        public void RequestDespawn(AnimalAgentBase agent)
        {
            if (!agent || !_records.TryGetValue(agent, out var record)) return;
            agent.OnDespawn();
            record.zone?.MarkDespawned();
            if (record.cfg != null && _aliveBySpecies.ContainsKey(record.cfg.speciesId))
            {
                _aliveBySpecies[record.cfg.speciesId] = Mathf.Max(0, _aliveBySpecies[record.cfg.speciesId] - 1);
            }
            _records.Remove(agent);
            _agents.Remove(agent);
            pool.Despawn(record.instance);
        }

        private void TrySpawnInZone(AnimalSpawnZone zone, ref int opCount)
        {
            if (!zone || zone.AliveCount >= zone.maxAliveInZone || Time.time < zone.NextSpawnTime)
            {
                return;
            }

            if (!player || !zone.species.Any())
            {
                zone.NextSpawnTime = Time.time + zone.spawnInterval;
                return;
            }

            var maxDist = zone.species.Max(s => s ? s.spawnMaxDist : 0f);
            if (!zone.IsZoneRelevant(player.position, maxDist))
            {
                return;
            }

            var cfg = PickSpecies(zone.species);
            if (!cfg || !cfg.prefab)
            {
                zone.NextSpawnTime = Time.time + zone.spawnInterval;
                return;
            }

            _aliveBySpecies.TryAdd(cfg.speciesId, 0);
            if (_aliveBySpecies[cfg.speciesId] >= cfg.maxAlive)
            {
                zone.NextSpawnTime = Time.time + zone.spawnInterval;
                return;
            }

            if (!zone.TryGetSpawnPoint(out var pos, out _))
            {
                zone.NextSpawnTime = Time.time + zone.spawnInterval;
                return;
            }

            if (cfg.requiresLineOfSight && !zone.PassesVisibilityGate(playerCamera, pos, cfg.spawnMinDist, cfg.spawnMaxDist))
            {
                zone.NextSpawnTime = Time.time + zone.spawnInterval * 0.5f;
                return;
            }

            var count = 1;
            if (zone.enableClusterSpawn && cfg.speciesId.ToLowerInvariant().Contains("butter"))
            {
                count = UnityEngine.Random.Range(zone.clusterMin, zone.clusterMax + 1);
            }

            for (var i = 0; i < count && opCount < maxOpsPerTick; i++)
            {
                SpawnOne(cfg, zone, pos + UnityEngine.Random.insideUnitSphere * 1.2f);
                opCount++;
            }

            zone.NextSpawnTime = Time.time + zone.spawnInterval;
        }

        private void SpawnOne(AnimalSpeciesConfig cfg, AnimalSpawnZone zone, Vector3 pos)
        {
            if (zone.AliveCount >= zone.maxAliveInZone) return;
            if (_aliveBySpecies[cfg.speciesId] >= cfg.maxAlive) return;

            var go = pool.Spawn(cfg.prefab, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            if (!go) return;

            if (!go.TryGetComponent(out AnimalAgentBase agent))
            {
                Debug.LogWarning($"Spawned {cfg.speciesId} lacks AnimalAgentBase and will be despawned.");
                pool.Despawn(go);
                return;
            }

            agent.Init(cfg, player, playerCamera, pool, zone, this);
            agent.OnSpawn();

            var record = new InstanceRecord { cfg = cfg, zone = zone, instance = go };
            _records[agent] = record;
            _agents.Add(agent);
            zone.MarkSpawned();
            _aliveBySpecies[cfg.speciesId]++;
            LastSpawnPosition = pos;
            LastSpawnSpeciesId = cfg.speciesId;
        }

        private AnimalSpeciesConfig PickSpecies(List<AnimalSpeciesConfig> set)
        {
            var total = 0f;
            foreach (var s in set)
            {
                if (!s || !s.prefab) continue;
                total += Mathf.Max(0.01f, s.weight);
            }

            if (total <= 0f) return null;
            var roll = UnityEngine.Random.Range(0f, total);
            foreach (var s in set)
            {
                if (!s || !s.prefab) continue;
                roll -= Mathf.Max(0.01f, s.weight);
                if (roll <= 0f) return s;
            }

            return set.FirstOrDefault(s => s && s.prefab);
        }

        private void Prewarm()
        {
            if (!pool) return;
            var uniquePrefabs = new HashSet<GameObject>();
            foreach (var z in _zones)
            {
                foreach (var cfg in z.species)
                {
                    if (cfg && cfg.prefab) uniquePrefabs.Add(cfg.prefab);
                    if (cfg) _aliveBySpecies.TryAdd(cfg.speciesId, 0);
                }
            }

            foreach (var prefab in uniquePrefabs)
            {
                pool.Prewarm(prefab, prewarmPerSpecies);
            }
        }
    }
}
