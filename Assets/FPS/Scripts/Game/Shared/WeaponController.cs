using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Game
{
    public enum WeaponShootType
    {
        Manual,
        Automatic,
        Charge,
        Burst,
        Beam,
    }

    [System.Serializable]
    public struct CrosshairData
    {
        [Tooltip("The image that will be used for this weapon's crosshair")]
        public Sprite CrosshairSprite;

        [Tooltip("The size of the crosshair image")]
        public int CrosshairSize;

        [Tooltip("The color of the crosshair image")]
        public Color CrosshairColor;
    }

    [RequireComponent(typeof(AudioSource))]
    public class WeaponController : MonoBehaviour
    {
        [Header("Information")] [Tooltip("The name that will be displayed in the UI for this weapon")]
        public string WeaponName;

        [Tooltip("The image that will be displayed in the UI for this weapon")]
        public Sprite WeaponIcon;

        [Tooltip("Default data for the crosshair")]
        public CrosshairData CrosshairDataDefault;

        [Tooltip("Data for the crosshair when targeting an enemy")]
        public CrosshairData CrosshairDataTargetInSight;

        [Header("Internal References")]
        [Tooltip("The root object for the weapon, this is what will be deactivated when the weapon isn't active")]
        public GameObject WeaponRoot;

        [Tooltip("Tip of the weapon, where the projectiles are shot")]
        public Transform WeaponMuzzle;

        [Header("Shoot Parameters")] [Tooltip("The type of weapon wil affect how it shoots")]
        public WeaponShootType ShootType;

        [Tooltip("The projectile prefab")] public ProjectileBase ProjectilePrefab;

        [Tooltip("Minimum duration between two shots")]
        public float DelayBetweenShots = 0.5f;

        [Tooltip("Angle for the cone in which the bullets will be shot randomly (0 means no spread at all)")]
        public float BulletSpreadAngle = 0f;

        [Tooltip("Amount of bullets per shot")]
        public int BulletsPerShot = 1;

        [Tooltip("Force that will push back the weapon after each shot")] [Range(0f, 2f)]
        public float RecoilForce = 1;

        [Tooltip("Ratio of the default FOV that this weapon applies while aiming")] [Range(0f, 1f)]
        public float AimZoomRatio = 1f;

        [Tooltip("Translation to apply to weapon arm when aiming with this weapon")]
        public Vector3 AimOffset;

        [Header("Ammo Parameters")]
        [Tooltip("Should the player manually reload")]
        public bool AutomaticReload = true;
        [Tooltip("Has physical clip on the weapon and ammo shells are ejected when firing")]
        public bool HasPhysicalBullets = false;
        [Tooltip("Number of bullets in a clip")]
        public int ClipSize = 30;
        [Tooltip("Bullet Shell Casing")]
        public GameObject ShellCasing;
        [Tooltip("Weapon Ejection Port for physical ammo")]
        public Transform EjectionPort;
        [Tooltip("Force applied on the shell")]
        [Range(0.0f, 5.0f)] public float ShellCasingEjectionForce = 2.0f;
        [Tooltip("Maximum number of shell that can be spawned before reuse")]
        [Range(1, 30)] public int ShellPoolSize = 1;
        [Tooltip("Amount of ammo reloaded per second")]
        public float AmmoReloadRate = 1f;

        [Tooltip("Delay after the last shot before starting to reload")]
        public float AmmoReloadDelay = 2f;

        [Tooltip("Maximum amount of ammo in the gun")]
        public int MaxAmmo = 8;

        [Header("Burst Parameters")] [Tooltip("Number of shots fired when the weapon uses burst fire")]
        [Range(1, 6)] public int BurstShotCount = 3;

        [Tooltip("Delay between individual shots in a burst")]
        public float BurstInterval = 0.08f;

        [Header("Beam Parameters")]
        [Tooltip("Maximum range of beam-based weapons")]
        public float BeamMaxDistance = 40f;

        [Tooltip("Damage dealt per second while the beam is firing")]
        public float BeamDamagePerSecond = 40f;

        [Tooltip("Ammo consumed per second while the beam is firing")]
        public float BeamAmmoUsageRate = 5f;

        [Tooltip("Layers that the beam can collide with")] public LayerMask BeamCollisionLayers = ~0;

        [Tooltip("Optional line renderer prefab visualising the beam")]
        public LineRenderer BeamLineRendererPrefab;

        [Tooltip("Optional VFX prefab spawned at the beam impact point")]
        public GameObject BeamImpactVfxPrefab;

        [Tooltip("Smoothing applied when updating the beam end position")] [Range(1f, 50f)]
        public float BeamEffectSmoothing = 12f;

        [Header("Charging parameters (charging weapons only)")]
        [Tooltip("Trigger a shot when maximum charge is reached")]
        public bool AutomaticReleaseOnCharged;

        [Tooltip("Duration to reach maximum charge")]
        public float MaxChargeDuration = 2f;

        [Tooltip("Initial ammo used when starting to charge")]
        public float AmmoUsedOnStartCharge = 1f;

        [Tooltip("Additional ammo used when charge reaches its maximum")]
        public float AmmoUsageRateWhileCharging = 1f;

        [Header("Audio & Visual")] 
        [Tooltip("Optional weapon animator for OnShoot animations")]
        public Animator WeaponAnimator;

        [Tooltip("Prefab of the muzzle flash")]
        public GameObject MuzzleFlashPrefab;

        [Tooltip("Unparent the muzzle flash instance on spawn")]
        public bool UnparentMuzzleFlash;

        [Tooltip("sound played when shooting")]
        public AudioClip ShootSfx;

        [Tooltip("Sound played when changing to this weapon")]
        public AudioClip ChangeWeaponSfx;

        [Tooltip("Continuous Shooting Sound")] public bool UseContinuousShootSound = false;
        public AudioClip ContinuousShootStartSfx;
        public AudioClip ContinuousShootLoopSfx;
        public AudioClip ContinuousShootEndSfx;
        AudioSource m_ContinuousShootAudioSource = null;
        bool m_WantsToShoot = false;

        public UnityAction OnShoot;
        public event Action OnShootProcessed;

        int m_CarriedPhysicalBullets;
        float m_CurrentAmmo;
        float m_LastTimeShot = Mathf.NegativeInfinity;
        public float LastChargeTriggerTimestamp { get; private set; }
        Vector3 m_LastMuzzlePosition;

        public GameObject Owner { get; set; }
        public GameObject SourcePrefab { get; set; }
        public bool IsCharging { get; private set; }
        public float CurrentAmmoRatio { get; private set; }
        public bool IsWeaponActive { get; private set; }
        public bool IsCooling { get; private set; }
        public float CurrentCharge { get; private set; }
        public Vector3 MuzzleWorldVelocity { get; private set; }

        public float GetAmmoNeededToShoot() =>
            (ShootType != WeaponShootType.Charge ? 1f : Mathf.Max(1f, AmmoUsedOnStartCharge)) /
            (MaxAmmo * BulletsPerShot);

        public int GetCarriedPhysicalBullets() => m_CarriedPhysicalBullets;
        public int GetCurrentAmmo() => Mathf.FloorToInt(m_CurrentAmmo);

        AudioSource m_ShootAudioSource;

        public bool IsReloading { get; private set; }

        const string k_AnimAttackParameter = "Attack";

        private Queue<Rigidbody> m_PhysicalAmmoPool;
        bool m_IsBurstShooting;
        int m_BurstShotsRemaining;
        float m_NextBurstShotTime;

        bool m_IsBeamFiring;
        LineRenderer m_BeamLineInstance;
        GameObject m_BeamImpactInstance;
        Vector3 m_BeamEndPoint;

        void Awake()
        {
            m_CurrentAmmo = MaxAmmo;
            m_CarriedPhysicalBullets = HasPhysicalBullets ? ClipSize : 0;
            m_LastMuzzlePosition = WeaponMuzzle.position;

            m_ShootAudioSource = GetComponent<AudioSource>();
            DebugUtility.HandleErrorIfNullGetComponent<AudioSource, WeaponController>(m_ShootAudioSource, this,
                gameObject);

            if (UseContinuousShootSound)
            {
                m_ContinuousShootAudioSource = gameObject.AddComponent<AudioSource>();
                m_ContinuousShootAudioSource.playOnAwake = false;
                m_ContinuousShootAudioSource.clip = ContinuousShootLoopSfx;
                m_ContinuousShootAudioSource.outputAudioMixerGroup =
                    AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.WeaponShoot);
                m_ContinuousShootAudioSource.loop = true;
            }

            if (HasPhysicalBullets)
            {
                m_PhysicalAmmoPool = new Queue<Rigidbody>(ShellPoolSize);

                for (int i = 0; i < ShellPoolSize; i++)
                {
                    GameObject shell = Instantiate(ShellCasing, transform);
                    shell.SetActive(false);
                    m_PhysicalAmmoPool.Enqueue(shell.GetComponent<Rigidbody>());
                }
            }
        }

        public void AddCarriablePhysicalBullets(int count) => m_CarriedPhysicalBullets = Mathf.Max(m_CarriedPhysicalBullets + count, MaxAmmo);

        void ShootShell()
        {
            Rigidbody nextShell = m_PhysicalAmmoPool.Dequeue();

            nextShell.transform.position = EjectionPort.transform.position;
            nextShell.transform.rotation = EjectionPort.transform.rotation;
            nextShell.gameObject.SetActive(true);
            nextShell.transform.SetParent(null);
            nextShell.collisionDetectionMode = CollisionDetectionMode.Continuous;
            nextShell.AddForce(nextShell.transform.up * ShellCasingEjectionForce, ForceMode.Impulse);

            m_PhysicalAmmoPool.Enqueue(nextShell);
        }

        void PlaySFX(AudioClip sfx) => AudioUtility.CreateSFX(sfx, transform.position, AudioUtility.AudioGroups.WeaponShoot, 0.0f);


        void Reload()
        {
            if (m_CarriedPhysicalBullets > 0)
            {
                m_CurrentAmmo = Mathf.Min(m_CarriedPhysicalBullets, ClipSize);
            }

            IsReloading = false;
        }

        public void StartReloadAnimation()
        {
            if (m_CurrentAmmo < m_CarriedPhysicalBullets)
            {
                GetComponent<Animator>().SetTrigger("Reload");
                IsReloading = true;
            }
        }

        void Update()
        {
            UpdateAmmo();
            UpdateCharge();
            UpdateContinuousShootSound();
            UpdateBurstShooting();
            UpdateBeam();

            if (Time.deltaTime > 0)
            {
                MuzzleWorldVelocity = (WeaponMuzzle.position - m_LastMuzzlePosition) / Time.deltaTime;
                m_LastMuzzlePosition = WeaponMuzzle.position;
            }
        }

        void UpdateAmmo()
        {
            if (AutomaticReload && m_LastTimeShot + AmmoReloadDelay < Time.time && m_CurrentAmmo < MaxAmmo && !IsCharging)
            {
                // reloads weapon over time
                m_CurrentAmmo += AmmoReloadRate * Time.deltaTime;

                // limits ammo to max value
                m_CurrentAmmo = Mathf.Clamp(m_CurrentAmmo, 0, MaxAmmo);

                IsCooling = true;
            }
            else
            {
                IsCooling = false;
            }

            if (MaxAmmo == Mathf.Infinity)
            {
                CurrentAmmoRatio = 1f;
            }
            else
            {
                CurrentAmmoRatio = m_CurrentAmmo / MaxAmmo;
            }
        }

        void UpdateCharge()
        {
            if (IsCharging)
            {
                if (CurrentCharge < 1f)
                {
                    float chargeLeft = 1f - CurrentCharge;

                    // Calculate how much charge ratio to add this frame
                    float chargeAdded = 0f;
                    if (MaxChargeDuration <= 0f)
                    {
                        chargeAdded = chargeLeft;
                    }
                    else
                    {
                        chargeAdded = (1f / MaxChargeDuration) * Time.deltaTime;
                    }

                    chargeAdded = Mathf.Clamp(chargeAdded, 0f, chargeLeft);

                    // See if we can actually add this charge
                    float ammoThisChargeWouldRequire = chargeAdded * AmmoUsageRateWhileCharging;
                    if (ammoThisChargeWouldRequire <= m_CurrentAmmo)
                    {
                        // Use ammo based on charge added
                        UseAmmo(ammoThisChargeWouldRequire);

                        // set current charge ratio
                        CurrentCharge = Mathf.Clamp01(CurrentCharge + chargeAdded);
                    }
                }
            }
        }

        void UpdateContinuousShootSound()
        {
            if (UseContinuousShootSound)
            {
                if (m_WantsToShoot && m_CurrentAmmo >= 1f)
                {
                    if (!m_ContinuousShootAudioSource.isPlaying)
                    {
                        m_ShootAudioSource.PlayOneShot(ShootSfx);
                        m_ShootAudioSource.PlayOneShot(ContinuousShootStartSfx);
                        m_ContinuousShootAudioSource.Play();
                    }
                }
                else if (m_ContinuousShootAudioSource.isPlaying)
                {
                    m_ShootAudioSource.PlayOneShot(ContinuousShootEndSfx);
                    m_ContinuousShootAudioSource.Stop();
                }
            }
        }

        public void ShowWeapon(bool show)
        {
            WeaponRoot.SetActive(show);

            if (show && ChangeWeaponSfx)
            {
                m_ShootAudioSource.PlayOneShot(ChangeWeaponSfx);
            }

            IsWeaponActive = show;
        }

        public void UseAmmo(float amount)
        {
            m_CurrentAmmo = Mathf.Clamp(m_CurrentAmmo - amount, 0f, MaxAmmo);
            m_CarriedPhysicalBullets -= Mathf.RoundToInt(amount);
            m_CarriedPhysicalBullets = Mathf.Clamp(m_CarriedPhysicalBullets, 0, MaxAmmo);
            m_LastTimeShot = Time.time;
        }

        public bool HandleShootInputs(bool inputDown, bool inputHeld, bool inputUp)
        {
            m_WantsToShoot = inputDown || inputHeld;
            switch (ShootType)
            {
                case WeaponShootType.Manual:
                    if (inputDown)
                    {
                        return TryShoot();
                    }

                    return false;

                case WeaponShootType.Automatic:
                    if (inputHeld)
                    {
                        return TryShoot();
                    }

                    return false;

                case WeaponShootType.Charge:
                    if (inputHeld)
                    {
                        TryBeginCharge();
                    }

                    // Check if we released charge or if the weapon shoot autmatically when it's fully charged
                    if (inputUp || (AutomaticReleaseOnCharged && CurrentCharge >= 1f))
                    {
                        return TryReleaseCharge();
                    }

                    return false;

                case WeaponShootType.Burst:
                    if (inputDown)
                    {
                        return TryBeginBurst();
                    }

                    return false;

                case WeaponShootType.Beam:
                    if (inputDown)
                    {
                        TryStartBeam();
                    }

                    if (inputUp)
                    {
                        StopBeam();
                    }

                    return m_IsBeamFiring;

                default:
                    return false;
            }
        }

        bool TryShoot()
        {
            if (m_CurrentAmmo >= 1f
                && m_LastTimeShot + DelayBetweenShots < Time.time)
            {
                HandleShoot();
                m_CurrentAmmo -= 1f;

                return true;
            }

            return false;
        }

        bool TryBeginCharge()
        {
            if (!IsCharging
                && m_CurrentAmmo >= AmmoUsedOnStartCharge
                && Mathf.FloorToInt((m_CurrentAmmo - AmmoUsedOnStartCharge) * BulletsPerShot) > 0
                && m_LastTimeShot + DelayBetweenShots < Time.time)
            {
                UseAmmo(AmmoUsedOnStartCharge);

                LastChargeTriggerTimestamp = Time.time;
                IsCharging = true;

                return true;
            }

            return false;
        }

        bool TryReleaseCharge()
        {
            if (IsCharging)
            {
                HandleShoot();

                CurrentCharge = 0f;
                IsCharging = false;

                return true;
            }

            return false;
        }

        bool TryBeginBurst()
        {
            if (m_IsBurstShooting)
            {
                return false;
            }

            if (m_CurrentAmmo >= 1f && m_LastTimeShot + DelayBetweenShots < Time.time)
            {
                m_IsBurstShooting = true;
                m_BurstShotsRemaining = Mathf.Min(Mathf.FloorToInt(m_CurrentAmmo), Mathf.Max(1, BurstShotCount));

                if (m_BurstShotsRemaining <= 0)
                {
                    m_IsBurstShooting = false;
                    return false;
                }

                FireBurstShot();
                m_BurstShotsRemaining--;
                m_NextBurstShotTime = Time.time + BurstInterval;

                if (m_BurstShotsRemaining <= 0)
                {
                    m_IsBurstShooting = false;
                }

                return true;
            }

            return false;
        }

        void FireBurstShot()
        {
            HandleShoot();
            m_CurrentAmmo = Mathf.Max(0f, m_CurrentAmmo - 1f);
        }

        void UpdateBurstShooting()
        {
            if (!m_IsBurstShooting)
            {
                return;
            }

            if (m_BurstShotsRemaining <= 0 || m_CurrentAmmo < 1f)
            {
                m_IsBurstShooting = false;
                return;
            }

            if (Time.time >= m_NextBurstShotTime)
            {
                FireBurstShot();
                m_BurstShotsRemaining--;
                m_NextBurstShotTime = Time.time + BurstInterval;

                if (m_BurstShotsRemaining <= 0)
                {
                    m_IsBurstShooting = false;
                }
            }
        }

        void TryStartBeam()
        {
            if (m_IsBeamFiring || m_CurrentAmmo <= 0f || m_LastTimeShot + DelayBetweenShots > Time.time)
            {
                return;
            }

            m_IsBeamFiring = true;
            EnsureBeamVisuals();
            UpdateBeamVisuals(WeaponMuzzle.position, WeaponMuzzle.position + WeaponMuzzle.forward * BeamMaxDistance, false);

            OnShoot?.Invoke();
            OnShootProcessed?.Invoke();
        }

        void StopBeam()
        {
            if (!m_IsBeamFiring)
            {
                return;
            }

            m_IsBeamFiring = false;
            m_WantsToShoot = false;

            if (m_BeamLineInstance != null)
            {
                m_BeamLineInstance.enabled = false;
            }

            if (m_BeamImpactInstance != null)
            {
                m_BeamImpactInstance.SetActive(false);
            }
        }

        void UpdateBeam()
        {
            if (!m_IsBeamFiring)
            {
                return;
            }

            if (m_CurrentAmmo <= 0f)
            {
                StopBeam();
                return;
            }

            Vector3 origin = WeaponMuzzle.position;
            Vector3 direction = WeaponMuzzle.forward;
            Vector3 targetPoint = origin + direction * BeamMaxDistance;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, BeamMaxDistance, BeamCollisionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;

                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null && (Owner == null || !hit.collider.transform.IsChildOf(Owner.transform)))
                {
                    health.TakeDamage(BeamDamagePerSecond * Time.deltaTime, Owner);
                }

                if (m_BeamImpactInstance != null)
                {
                    m_BeamImpactInstance.transform.position = targetPoint;
                    m_BeamImpactInstance.transform.forward = hit.normal;
                    if (!m_BeamImpactInstance.activeSelf)
                    {
                        m_BeamImpactInstance.SetActive(true);
                    }
                }
            }
            else if (m_BeamImpactInstance != null && m_BeamImpactInstance.activeSelf)
            {
                m_BeamImpactInstance.SetActive(false);
            }

            UpdateBeamVisuals(origin, targetPoint, true);

            float ammoConsumed = BeamAmmoUsageRate * Time.deltaTime;
            m_CurrentAmmo = Mathf.Max(0f, m_CurrentAmmo - ammoConsumed);
            m_LastTimeShot = Time.time;
        }

        void EnsureBeamVisuals()
        {
            if (BeamLineRendererPrefab != null && m_BeamLineInstance == null)
            {
                m_BeamLineInstance = Instantiate(BeamLineRendererPrefab, WeaponMuzzle);
            }

            if (m_BeamLineInstance != null)
            {
                m_BeamLineInstance.positionCount = 2;
                m_BeamLineInstance.enabled = true;
            }

            if (BeamImpactVfxPrefab != null && m_BeamImpactInstance == null)
            {
                m_BeamImpactInstance = Instantiate(BeamImpactVfxPrefab);
                m_BeamImpactInstance.SetActive(false);
            }
        }

        void UpdateBeamVisuals(Vector3 origin, Vector3 targetPoint, bool smooth)
        {
            if (m_BeamLineInstance == null)
            {
                return;
            }

            if (!smooth)
            {
                m_BeamEndPoint = targetPoint;
            }
            else
            {
                m_BeamEndPoint = Vector3.Lerp(m_BeamEndPoint, targetPoint, BeamEffectSmoothing * Time.deltaTime);
            }

            m_BeamLineInstance.SetPosition(0, origin);
            m_BeamLineInstance.SetPosition(1, m_BeamEndPoint);
        }

        void OnDisable()
        {
            StopBeam();
        }

        void HandleShoot()
        {
            int bulletsPerShotFinal = ShootType == WeaponShootType.Charge
                ? Mathf.CeilToInt(CurrentCharge * BulletsPerShot)
                : BulletsPerShot;

            // spawn all bullets with random direction
            for (int i = 0; i < bulletsPerShotFinal; i++)
            {
                Vector3 shotDirection = GetShotDirectionWithinSpread(WeaponMuzzle);
                ProjectileBase newProjectile = Instantiate(ProjectilePrefab, WeaponMuzzle.position,
                    Quaternion.LookRotation(shotDirection));
                newProjectile.Shoot(this);
            }

            // muzzle flash
            if (MuzzleFlashPrefab != null)
            {
                GameObject muzzleFlashInstance = Instantiate(MuzzleFlashPrefab, WeaponMuzzle.position,
                    WeaponMuzzle.rotation, WeaponMuzzle.transform);
                // Unparent the muzzleFlashInstance
                if (UnparentMuzzleFlash)
                {
                    muzzleFlashInstance.transform.SetParent(null);
                }

                Destroy(muzzleFlashInstance, 2f);
            }

            if (HasPhysicalBullets)
            {
                ShootShell();
                m_CarriedPhysicalBullets--;
            }

            m_LastTimeShot = Time.time;

            // play shoot SFX
            if (ShootSfx && !UseContinuousShootSound)
            {
                m_ShootAudioSource.PlayOneShot(ShootSfx);
            }

            // Trigger attack animation if there is any
            if (WeaponAnimator)
            {
                WeaponAnimator.SetTrigger(k_AnimAttackParameter);
            }

            OnShoot?.Invoke();
            OnShootProcessed?.Invoke();
        }

        public Vector3 GetShotDirectionWithinSpread(Transform shootTransform)
        {
            float spreadAngleRatio = BulletSpreadAngle / 180f;
            Vector3 spreadWorldDirection = Vector3.Slerp(shootTransform.forward, UnityEngine.Random.insideUnitSphere,
                spreadAngleRatio);

            return spreadWorldDirection;
        }
    }
}
