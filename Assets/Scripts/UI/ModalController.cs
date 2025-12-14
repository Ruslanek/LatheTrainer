using UnityEngine;

public class ModalController : MonoBehaviour
{
    [Header("Overlay (tło)")]
    public GameObject modalOverlay;

    [Header("Okna")]
    public GameObject machineModal;
    public GameObject workpieceModal;
    public GameObject toolModal;

    void Awake()
    {
        HideAll();
    }

    public void ShowMachine()
    {
        ShowOnly(machineModal);
    }

    public void ShowWorkpiece()
    {
        ShowOnly(workpieceModal);
    }

    public void ShowTool()
    {
        ShowOnly(toolModal);
    }

    public void HideAll()
    {
        if (modalOverlay) modalOverlay.SetActive(false);
        if (machineModal) machineModal.SetActive(false);
        if (workpieceModal) workpieceModal.SetActive(false);
        if (toolModal) toolModal.SetActive(false);
    }

    private void ShowOnly(GameObject target)
    {
        if (modalOverlay) modalOverlay.SetActive(true);

        if (machineModal) machineModal.SetActive(false);
        if (workpieceModal) workpieceModal.SetActive(false);
        if (toolModal) toolModal.SetActive(false);

        if (target) target.SetActive(true);
    }
}