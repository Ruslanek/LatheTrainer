using UnityEngine;

namespace LatheTrainer.UI
{
    public class TopBarUI : MonoBehaviour
    {
        public LatheTrainer.Machine.LatheStateController lathe;

        [Header("Modal root")]
        public GameObject modalOverlay;

        [Header("Modals")]
        public GameObject machineModal;
        public GameObject workpieceModal;
        public GameObject toolModal;

        public void OpenMachine()
        {
            Open(machineModal);
        }

        public void OpenWorkpiece()
        {
            Open(workpieceModal);
        }

        public void OpenTool()
        {
            Open(toolModal);
        }

        public void CloseAll()
        {
            if (machineModal) machineModal.SetActive(false);
            if (workpieceModal) workpieceModal.SetActive(false);
            if (toolModal) toolModal.SetActive(false);
            if (modalOverlay) modalOverlay.SetActive(false);

            if (lathe != null)
                lathe.ExitSafeState();
        }

        private void Open(GameObject modal)
        {
            if (lathe != null)
                lathe.EnterSafeState();

            if (modalOverlay) modalOverlay.SetActive(true);

            if (machineModal) machineModal.SetActive(false);
            if (workpieceModal) workpieceModal.SetActive(false);
            if (toolModal) toolModal.SetActive(false);

            if (modal) modal.SetActive(true);
        }
    }
}