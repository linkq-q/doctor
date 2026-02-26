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

        [Header("Animator Param Mapping")]
        public string speedParam = "Speed";
        public string fleeBoolParam = "IsFlee";
        public string idleVariantParam = "IdleVariant";
        public string attackTriggerParam = "Attack";
    }
}
