using System.Text;
using UnityEngine;

namespace Game.Animals.DebugUI
{
    public class AnimalDebugHUD : MonoBehaviour
    {
        [SerializeField] private AnimalSpawnManager manager;
        [SerializeField] private KeyCode toggleKey = KeyCode.F8;
        [SerializeField] private bool visible = true;

        private float _delta;

        private void Awake()
        {
            if (!manager)
            {
                manager = FindAnyObjectByType<AnimalSpawnManager>();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;
            _delta += (Time.unscaledDeltaTime - _delta) * 0.1f;
        }

        private void OnGUI()
        {
            if (!visible || !manager) return;
            var fps = 1f / Mathf.Max(0.0001f, _delta);
            var sb = new StringBuilder();
            sb.AppendLine($"FPS: {fps:0}");
            sb.AppendLine($"Total Alive: {manager.TotalAlive}");
            sb.AppendLine("-- Species --");
            foreach (var kv in manager.AliveBySpecies)
            {
                sb.AppendLine($"{kv.Key}: {kv.Value}");
            }

            sb.AppendLine("-- Zones --");
            foreach (var z in manager.Zones)
            {
                if (!z) continue;
                sb.AppendLine($"{z.zoneType}: {z.AliveCount}/{z.maxAliveInZone}");
            }

            sb.AppendLine($"Last Spawn: {manager.LastSpawnSpeciesId} @ {manager.LastSpawnPosition}");
            GUI.Box(new Rect(20, 20, 360, 360), sb.ToString());
        }
    }
}
