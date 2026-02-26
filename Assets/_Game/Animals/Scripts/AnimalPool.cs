using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Animals
{
    public class AnimalPool : MonoBehaviour
    {
        private readonly Dictionary<GameObject, Queue<GameObject>> _pool = new();
        private readonly Dictionary<GameObject, GameObject> _instanceToPrefab = new();

        public void Prewarm(GameObject prefab, int count)
        {
            if (!prefab || count <= 0) return;
            Ensure(prefab);
            for (var i = 0; i < count; i++)
            {
                var obj = CreateInstance(prefab);
                _pool[prefab].Enqueue(obj);
            }
        }

        public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (!prefab) return null;
            Ensure(prefab);
            var q = _pool[prefab];
            var go = q.Count > 0 ? q.Dequeue() : CreateInstance(prefab);
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
            ResetSpawnState(go);
            return go;
        }

        public void Despawn(GameObject go)
        {
            if (!go) return;
            if (!_instanceToPrefab.TryGetValue(go, out var prefab))
            {
                Destroy(go);
                return;
            }

            ResetDespawnState(go);
            go.SetActive(false);
            _pool[prefab].Enqueue(go);
        }

        private void Ensure(GameObject prefab)
        {
            if (!_pool.ContainsKey(prefab))
            {
                _pool[prefab] = new Queue<GameObject>();
            }
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            var go = Instantiate(prefab, transform);
            _instanceToPrefab[go] = prefab;
            go.SetActive(false);
            return go;
        }

        private static void ResetSpawnState(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = true;

            foreach (var anim in go.GetComponentsInChildren<Animator>(true))
            {
                anim.enabled = true;
                anim.Rebind();
                anim.Update(0f);
            }

            foreach (var agent in go.GetComponentsInChildren<NavMeshAgent>(true))
            {
                agent.enabled = true;
                if (agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    agent.velocity = Vector3.zero;
                }
            }
        }

        private static void ResetDespawnState(GameObject go)
        {
            foreach (var agent in go.GetComponentsInChildren<NavMeshAgent>(true))
            {
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    agent.velocity = Vector3.zero;
                }
            }
        }
    }
}
