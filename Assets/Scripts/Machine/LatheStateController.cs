using UnityEngine;
using LatheTrainer.Core;
using LatheTrainer.UI;

namespace LatheTrainer.Machine
{
    public class LatheStateController : MonoBehaviour
    {
        [Header("Refs")]
        public ChuckSpindleVisual spindleVisual;
        public WorkpieceController workpieceController;
        public ToolPositionController toolPosition;

        [Header("Safe park (mm)")]
        public float parkXmm = 0f;
        public float parkZmm = 300f;

        [SerializeField] public LatheButtonsUI latheButtonsUI;


       /* public void EnterSafeState()
        {
            latheButtonsUI?.PressStopExternal();

            if (toolPosition != null)
            {
                toolPosition.SetInputEnabled(false);
                toolPosition.MoveToMm(parkXmm, parkZmm, instant: true);
            }
        }*/
        
        public void EnterSafeState()
        {
           /* if (spindleVisual != null)
            {
                spindleVisual.SetCommandedRpm(0f); // zatrzymać wrzeciono
            }*/

            if (spindleVisual == null) return;
            PressStop();

            //latheButtonsUI.PressStopExternal();



            if (toolPosition != null)
            {
                toolPosition.SetInputEnabled(false);
                toolPosition.MoveToMm(parkXmm, parkZmm, instant: true);
            }
        }



        public void ExitSafeState()
        {
            if (toolPosition != null)
                toolPosition.SetInputEnabled(true);
        }

        public void ApplyWorkpiece(WorkpieceParams p)
        {
            if (workpieceController != null)
                workpieceController.ApplyParams(p);
            // Zaciskanie teraz uruchamia się automatycznie wewnątrz ApplyParams -> SelectWorkpiece

            Debug.Log("Metoda public void ApplyWorkpiece(WorkpieceParams p) w LatheStateController");
        }

        // StartClampAnimation nie jest już potrzebna




        public void ApplyWorkpieceMm(MaterialType material, float diameterMm, float lengthMm)
        {
            if (workpieceController != null)
                workpieceController.ApplyFromMm(material, diameterMm, lengthMm);
        }

        public bool PressStart()
        {
            if (spindleVisual == null) return false;
            return spindleVisual.TryStartSpindle();
        }

        public void PressStop()
        {
            if (latheButtonsUI != null)
                latheButtonsUI.PressStopExternal(); // wykonuje to samo, co przycisk
            else if (spindleVisual != null)
                spindleVisual.StopSpindle();
        }

        public void PressReverseToggle(bool reverseOn)
        {
            if (spindleVisual == null) return;
            spindleVisual.SetSpinDirection(reverseOn);
        }

        public void EnterCrashState(string message)
        {
            // 1) blokujemy
            LatheSafetyLock.Lock();

            // 2) zatrzymujemy wszystko i parkujemy
            EnterSafeState();

            // 3) wyświetlamy okno; po zamknięciu zdejmujemy blokadę i zezwalamy na sterowanie
            if (CrashPopupUI.Instance != null)
            {
                CrashPopupUI.Instance.Show(message, () =>
                {
                    ExitSafeState();
                    LatheSafetyLock.Unlock();
                });
            }
        }
    }
}