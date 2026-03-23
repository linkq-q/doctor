using UnityEngine;

namespace Game.Animals
{
    [CreateAssetMenu(menuName = "Game/Animals/Species Config", fileName = "ASC_")]
    public class AnimalSpeciesConfig : ScriptableObject
    {
        [Header("Identity")]
        public string speciesId = "Unknown";
        public GameObject prefab;
        [Min(0.01f)] public float weight = 1f;
        [Min(0)] public int maxAlive = 5;

        [Header("Spawn & LOD")]
        [Min(0f)] public float spawnMinDist = 12f;
        [Min(0f)] public float spawnMaxDist = 45f;
        [Min(0f)] public float despawnDist = 70f;
        [Min(0f)] public float sleepDist = 55f;
        [Min(0f)] public float stopAnimatorDist = 45f;
        public bool requiresLineOfSight = true;
        public bool useNavMesh = true;

        [Header("Movement")]
        [Min(0f)] public float walkSpeed = 1.5f;
        [Min(0f)] public float runSpeed = 4.5f;
        [Min(0f)] public float turnSpeed = 360f;

        [Header("AI")]
        [Min(0f)] public float fleeDist = 6f;
        [Min(0f)] public float alertDist = 12f;
        [Min(0f)] public float chaseDist = 18f;
        [Min(0f)] public float chaseGiveUpDist = 30f;

        [Header("Random Scale")]
        public bool enableRandomScale = false;
        [Min(0.1f)] public float minScale = 0.9f;
        [Min(0.1f)] public float maxScale = 1.1f;

        [Header("Flying")]
        [Tooltip("是否飞行生物。若为 true，刷新高度与巡航高度参数生效。")]
        public bool isFlying = false;
        [Min(0f)] public float spawnHeightMin = 2f;
        [Min(0f)] public float spawnHeightMax = 5f;
        [Min(0f)] public float cruiseHeightMin = 2f;
        [Min(0f)] public float cruiseHeightMax = 5f;

        [Header("Predation (Bear)")]
        [Min(0f)] public float bearDetectRadius = 22f;
        [Min(0f)] public float bearGiveUpRadius = 36f;
        [Min(0f)] public float bearCatchDistance = 1.8f;
        [Min(0f)] public float bearMaxChaseTime = 10f;
        [Min(0f)] public float postCatchIdleSeconds = 2.5f;

        [Header("Predation (Deer)")]
        [Min(0.1f)] public float deerFleeSpeedMultiplier = 1.8f;
        [Min(0f)] public float deerFleeDurationSeconds = 3.5f;

        [Header("Animator Param Mapping")]
        public string speedParam = "Speed";
        public string fleeBoolParam = "IsFlee";
        public string idleVariantParam = "IdleVariant";
        public string attackTriggerParam = "Attack";
    }
}
