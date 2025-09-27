using UnityEngine;

namespace Unity.FPS.AI
{
    public class EnemyStrafeMobile : EnemyMobile
    {
        [Header("Strafe Settings")]
        [Tooltip("Preferred distance from the target while attacking")]
        public float DesiredAttackDistance = 12f;

        [Tooltip("Distance tolerance before the enemy repositions closer/further")]
        public float DistanceBuffer = 2f;

        [Tooltip("Sideways offset applied while orbiting the player")]
        public float StrafeOffset = 4f;

        [Tooltip("Time between strafe direction changes")]
        public float StrafeDirectionChangeInterval = 3f;

        [Tooltip("Multiplier applied to agent speed while repositioning")]
        public float RepositionSpeedMultiplier = 1.15f;

        float m_DefaultSpeed;
        float m_CurrentStrafeSign = 1f;
        float m_NextStrafeFlipTime = float.NegativeInfinity;

        void Awake()
        {
            if (m_EnemyController == null)
            {
                m_EnemyController = GetComponent<EnemyController>();
            }

            // cache the default speed so we can tweak the navmesh agent on the fly
            if (m_EnemyController != null && m_EnemyController.NavMeshAgent != null)
            {
                m_DefaultSpeed = m_EnemyController.NavMeshAgent.speed;
            }
        }

        protected override void UpdateCurrentAiState()
        {
            if (AiState != AIState.Attack || m_EnemyController.KnownDetectedTarget == null)
            {
                ResetSpeed();
                base.UpdateCurrentAiState();
                return;
            }

            Vector3 targetPosition = m_EnemyController.KnownDetectedTarget.transform.position;
            Vector3 toTarget = targetPosition - transform.position;
            float distanceToTarget = toTarget.magnitude;

            if (distanceToTarget <= float.Epsilon)
            {
                base.UpdateCurrentAiState();
                return;
            }

            Vector3 forward = toTarget.normalized;
            Vector3 orbitCenter = targetPosition - forward * DesiredAttackDistance;

            // Adjust distance if we are too far/close
            bool needsReposition = false;
            if (distanceToTarget > DesiredAttackDistance + DistanceBuffer)
            {
                needsReposition = true;
            }
            else if (distanceToTarget < Mathf.Max(1f, DesiredAttackDistance - DistanceBuffer))
            {
                needsReposition = true;
                orbitCenter = transform.position - forward * (DesiredAttackDistance - distanceToTarget);
            }

            if (needsReposition)
            {
                BoostSpeed();
                m_EnemyController.SetNavDestination(orbitCenter);
            }
            else
            {
                MaintainDefaultSpeed();

                if (Time.time >= m_NextStrafeFlipTime)
                {
                    m_CurrentStrafeSign *= -1f;
                    float randomized = Random.Range(0.6f, 1.4f);
                    m_NextStrafeFlipTime = Time.time + StrafeDirectionChangeInterval * randomized;
                }

                Vector3 strafeDir = Vector3.Cross(Vector3.up, forward).normalized * m_CurrentStrafeSign;
                Vector3 strafeDestination = orbitCenter + strafeDir * StrafeOffset;
                m_EnemyController.SetNavDestination(strafeDestination);
            }

            m_EnemyController.OrientTowards(targetPosition);
            m_EnemyController.OrientWeaponsTowards(targetPosition);
            m_EnemyController.TryAtack(targetPosition);
        }

        void BoostSpeed()
        {
            if (m_EnemyController.NavMeshAgent == null)
            {
                return;
            }

            if (Mathf.Approximately(m_DefaultSpeed, 0f))
            {
                m_DefaultSpeed = m_EnemyController.NavMeshAgent.speed;
            }

            m_EnemyController.NavMeshAgent.speed = m_DefaultSpeed * RepositionSpeedMultiplier;
        }

        void MaintainDefaultSpeed()
        {
            if (m_EnemyController.NavMeshAgent == null)
            {
                return;
            }

            if (!Mathf.Approximately(m_EnemyController.NavMeshAgent.speed, m_DefaultSpeed))
            {
                m_EnemyController.NavMeshAgent.speed = m_DefaultSpeed;
            }
        }

        void ResetSpeed()
        {
            MaintainDefaultSpeed();
        }
    }
}
