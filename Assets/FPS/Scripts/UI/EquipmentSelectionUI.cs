using UnityEngine;
using UnityEngine.Events;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;

namespace Unity.FPS.UI
{
    public class EquipmentSelectionUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Player weapons manager that will receive the selected loadout")]
        public PlayerWeaponsManager PlayerWeaponsManager;

        [Tooltip("Canvas containing the equipment selection UI")]
        public Canvas EquipmentCanvas;

        [Header("Available Equipment")]
        public GameObject EquipmentRoot;
        public WeaponController[] AvailableMeleeWeapons;
        public WeaponController[] AvailableRangedWeapons;

        [Header("Events")]
        public UnityEvent OnEquipmentConfirmed;

        int m_SelectedMeleeIndex = -1;
        int m_SelectedRangedIndex = -1;
        bool m_IsSelectionActive;

        void Start()
        {
            ActivateSelection();

            if (AvailableMeleeWeapons != null && AvailableMeleeWeapons.Length > 0)
            {
                SelectMelee(0);
            }

            if (AvailableRangedWeapons != null && AvailableRangedWeapons.Length > 0)
            {
                SelectRanged(0);
            }
        }

        public void SelectMelee(int index)
        {
            if (!IsValidIndex(index, AvailableMeleeWeapons))
                return;

            m_SelectedMeleeIndex = index;
        }

        public void SelectRanged(int index)
        {
            if (!IsValidIndex(index, AvailableRangedWeapons))
                return;

            m_SelectedRangedIndex = index;
        }

        public void ConfirmSelection()
        {
            if (!m_IsSelectionActive)
                return;

            var melee = GetSelection(m_SelectedMeleeIndex, AvailableMeleeWeapons);
            var ranged = GetSelection(m_SelectedRangedIndex, AvailableRangedWeapons);

            if (PlayerWeaponsManager != null)
            {
                PlayerWeaponsManager.InitializeLoadout(melee, ranged);
            }

            DeactivateSelection();

            OnEquipmentConfirmed?.Invoke();
        }

        void ActivateSelection()
        {
            m_IsSelectionActive = true;
            SetCursorState(true);

            if (EquipmentCanvas != null)
            {
                EquipmentCanvas.enabled = true;
            }

            if (EquipmentRoot != null)
            {
                EquipmentRoot.SetActive(true);
            }

            Time.timeScale = 0f;
        }

        void DeactivateSelection()
        {
            m_IsSelectionActive = false;
            SetCursorState(false);

            if (EquipmentCanvas != null)
            {
                EquipmentCanvas.enabled = false;
            }

            if (EquipmentRoot != null)
            {
                EquipmentRoot.SetActive(false);
            }

            Time.timeScale = 1f;
        }

        void SetCursorState(bool unlocked)
        {
            Cursor.lockState = unlocked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = unlocked;
        }

        bool IsValidIndex(int index, WeaponController[] collection)
        {
            return collection != null && index >= 0 && index < collection.Length;
        }

        WeaponController GetSelection(int index,
            WeaponController[] collection)
        {
            if (!IsValidIndex(index, collection))
            {
                return null;
            }

            return collection[index];
        }
    }
}
