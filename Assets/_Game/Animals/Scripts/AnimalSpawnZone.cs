using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Animals
{
    public enum ZoneType
    {
        RiverBank,
        GrassField,
        Forest,
        Ruins
    }

    public class AnimalSpawnZone : MonoBehaviour
    {
        [Header("Zone")]
        public ZoneType zoneType = ZoneType.GrassField;
        [Min(1f)] public float radius = 45f;
        [Min(1)] public int maxAliveInZone = 20;
        [Min(0.1f)] public float spawnInterval = 3f;
        [Range(0f, 60f)] public float maxSlope = 42f;
        public LayerMask groundMask = ~0;

        [Header("Spawn Set")]
        public List<AnimalSpeciesConfig> species = new();

        [Header("Butterfly Cluster")]
        public bool enableClusterSpawn = true;
        [Min(1)] public int clusterMin = 5;
        [Min(1)] public int clusterMax = 8;

        public int AliveCount { get; private set; }
        public float NextSpawnTime { get; set; }

        public void MarkSpawned() => AliveCount++;
        public void MarkDespawned() => AliveCount = Mathf.Max(0, AliveCount - 1);

        public bool IsZoneRelevant(Vector3 playerPosition, float maxSpeciesDist)
        {
            var reach = maxSpeciesDist + radius;
            return (playerPosition - transform.position).sqrMagnitude <= reach * reach;
        }

        public bool TryGetSpawnPoint(out Vector3 pos, out Vector3 normal)
        {
            for (var i = 0; i < 10; i++)
            {
                var sample = Random.insideUnitCircle * radius;
                var origin = transform.position + new Vector3(sample.x, 50f, sample.y);
                if (Physics.Raycast(origin, Vector3.down, out var hit, 120f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    var slope = Vector3.Angle(hit.normal, Vector3.up);
                    if (slope <= maxSlope)
                    {
                        pos = hit.point;
                        normal = hit.normal;
                        return true;
                    }
                }
            }

            pos = transform.position;
            normal = Vector3.up;
            return false;
        }

        public bool PassesVisibilityGate(Camera cam, Vector3 spawnPos, float minDist, float maxDist)
        {
            if (!cam)
            {
                return true;
            }

            var to = spawnPos - cam.transform.position;
            var dist = to.magnitude;
            if (dist < minDist || dist > maxDist)
            {
                return false;
            }

            var viewport = cam.WorldToViewportPoint(spawnPos);
            if (viewport.z <= 0f)
            {
                return false;
            }

            return viewport.x > 0.08f && viewport.x < 0.92f && viewport.y > 0.05f && viewport.y < 0.95f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = GetZoneColor();
            Gizmos.DrawWireSphere(transform.position, radius);
            Handles.color = Gizmos.color;
            Handles.Label(transform.position + Vector3.up * 2f, $"{zoneType}  Alive:{AliveCount}/{maxAliveInZone}");
        }

        private Color GetZoneColor()
        {
            return zoneType switch
            {
                ZoneType.RiverBank => new Color(0.3f, 0.7f, 1f, 1f),
                ZoneType.GrassField => new Color(0.4f, 1f, 0.4f, 1f),
                ZoneType.Forest => new Color(0.2f, 0.6f, 0.2f, 1f),
                ZoneType.Ruins => new Color(0.9f, 0.8f, 0.4f, 1f),
                _ => Color.white
            };
        }
#endif
    }
}
