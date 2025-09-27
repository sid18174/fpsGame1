using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(EnemyController))]
    public class EnemyExploder : MonoBehaviour
    {
        [Header("Explosion Settings")]
        [Tooltip("Time in seconds after entering attack range before detonation")]
        public float FuseTime = 2f;

        [Tooltip("Damage dealt at the center of the explosion")]
        public float ExplosionDamage = 75f;

        [Tooltip("Radius of the explosion")]
        public float ExplosionRadius = 6f;

        [Tooltip("Layers affected by the explosion")]
        public LayerMask ExplosionLayers = -1;

        [Tooltip("Falloff curve applied based on distance to the explosion center")]
        public AnimationCurve DamageFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Feedback")]
        [Tooltip("Looped audio clip while the fuse is burning")]
        public AudioClip FuseLoopSfx;

        [Tooltip("Audio clip played on detonation")]
        public AudioClip ExplosionSfx;

        [Tooltip("Optional VFX spawned when the enemy explodes")]
        public GameObject ExplosionVfx;

        [Tooltip("Offset applied to the VFX spawn position")] public Vector3 ExplosionVfxOffset = Vector3.up;

        [Tooltip("Additional speed multiplier applied while charging the player")]
        public float ChargeSpeedMultiplier = 1.35f;

        EnemyController m_EnemyController;
        AudioSource m_AudioSource;
        float m_FuseTimer;
        bool m_IsDetonating;
        float m_DefaultSpeed;

        void Awake()
        {
            m_EnemyController = GetComponent<EnemyController>();
            DebugUtility.HandleErrorIfNullGetComponent<EnemyController, EnemyExploder>(m_EnemyController, this,
                gameObject);

            m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
                m_AudioSource.playOnAwake = false;
            }

            if (m_EnemyController.NavMeshAgent != null)
            {
                m_DefaultSpeed = m_EnemyController.NavMeshAgent.speed;
            }
        }

        void OnEnable()
        {
            m_EnemyController.onLostTarget += HandleLostTarget;
            m_EnemyController.onDetectedTarget += HandleDetectedTarget;
        }

        void OnDisable()
        {
            m_EnemyController.onLostTarget -= HandleLostTarget;
            m_EnemyController.onDetectedTarget -= HandleDetectedTarget;
            StopFuseSound();
        }

        void Update()
        {
            if (m_EnemyController.KnownDetectedTarget == null)
            {
                CancelDetonation();
                return;
            }

            if (m_EnemyController.IsTargetInAttackRange)
            {
                if (!m_IsDetonating)
                {
                    BeginDetonation();
                }

                m_FuseTimer += Time.deltaTime;
                if (m_FuseTimer >= FuseTime)
                {
                    Detonate();
                }
            }
            else
            {
                CancelDetonation();
            }
        }

        void BeginDetonation()
        {
            m_IsDetonating = true;
            m_FuseTimer = 0f;

            if (m_EnemyController.NavMeshAgent != null)
            {
                m_EnemyController.NavMeshAgent.speed = m_DefaultSpeed * ChargeSpeedMultiplier;
            }

            PlayFuseSound();
        }

        void CancelDetonation()
        {
            if (!m_IsDetonating)
            {
                return;
            }

            m_IsDetonating = false;
            m_FuseTimer = 0f;
            StopFuseSound();

            if (m_EnemyController.NavMeshAgent != null)
            {
                m_EnemyController.NavMeshAgent.speed = m_DefaultSpeed;
            }
        }

        void HandleLostTarget()
        {
            CancelDetonation();
        }

        void HandleDetectedTarget()
        {
            // intentionally left blank, handled in Update
        }

        void Detonate()
        {
            StopFuseSound();

            if (ExplosionVfx != null)
            {
                GameObject vfxInstance = Instantiate(ExplosionVfx, transform.position + ExplosionVfxOffset,
                    Quaternion.identity);
                Destroy(vfxInstance, 5f);
            }

            if (ExplosionSfx != null)
            {
                AudioUtility.CreateSFX(ExplosionSfx, transform.position, AudioUtility.AudioGroups.EnemyAttack, 1f);
            }

            Collider[] colliders = Physics.OverlapSphere(transform.position, ExplosionRadius, ExplosionLayers,
                QueryTriggerInteraction.Ignore);
            foreach (var collider in colliders)
            {
                Damageable damageable = collider.GetComponent<Damageable>();
                if (damageable == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, damageable.transform.position);
                float normalizedDistance = Mathf.Clamp01(distance / ExplosionRadius);
                float damageMultiplier = DamageFalloff.Evaluate(normalizedDistance);
                float damage = ExplosionDamage * damageMultiplier;
                damageable.InflictDamage(damage, true, gameObject);
            }

            // Kill the owner, which will trigger the regular enemy death flow
            Health health = GetComponent<Health>();
            if (health != null)
            {
                health.Kill();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void PlayFuseSound()
        {
            if (FuseLoopSfx == null || m_AudioSource == null)
            {
                return;
            }

            m_AudioSource.clip = FuseLoopSfx;
            m_AudioSource.loop = true;
            m_AudioSource.Play();
        }

        void StopFuseSound()
        {
            if (m_AudioSource != null && m_AudioSource.isPlaying)
            {
                m_AudioSource.Stop();
                m_AudioSource.clip = null;
            }
        }
    }
}
