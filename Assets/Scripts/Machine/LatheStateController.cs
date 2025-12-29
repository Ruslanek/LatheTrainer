using UnityEngine;
using LatheTrainer.Core;
using LatheTrainer.UI;
using System.Collections;

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

        private bool _isChangingWorkpiece;
        public bool IsChangingWorkpiece => _isChangingWorkpiece;

        private bool _startBlocked;
        public bool IsStartBlocked => _startBlocked;


        public void EnterSafeState()
        {
            if (spindleVisual == null) return;
            PressStop();

            latheButtonsUI.PressStopExternal();

            if (toolPosition != null)
            {
                toolPosition.SetInputEnabled(false);
               // toolPosition.MoveToMm(parkXmm, parkZmm, instant: true);
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

       
        public bool PressStart()
        {
            if (_isChangingWorkpiece) return false;
            if (LatheSafetyLock.IsLocked) return false;   // blok
            if (_startBlocked) return false;   // ✅ blokada uruchomienia podczas zmiany detalu
            if (spindleVisual == null) return false;
            return spindleVisual.TryStartSpindle();
        }

        public void PressStop()
        {
            if (spindleVisual != null)
                spindleVisual.StopSpindle();

            // if (latheButtonsUI != null)
            //    latheButtonsUI.PressStopExternal(); // wykonuje to samo, co przycisk
            // else if (spindleVisual != null)
            //    spindleVisual.StopSpindle();

            // opcjonalnie: odświeżenie lampek (ALE nie przez PressStopExternal)
            latheButtonsUI?.RefreshLampsExternal();
        }

        public void PressReverseToggle(bool reverseOn)
        {
            if (LatheSafetyLock.IsLocked) return;         // ✅ blok
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

        public void ApplyWorkpieceMm(MaterialType material, float diameterMm, float lengthMm)
        {
            StartCoroutine(ApplyWorkpieceRoutine(material, diameterMm, lengthMm));
        }

        private IEnumerator ApplyWorkpieceRoutine(MaterialType material, float dMm, float lMm)
        {
            _startBlocked = true;

            EnterSafeState(); // wewnątrz jest już wywołany PressStop()

            // czekamy na pełne zatrzymanie wrzeciona
            if (spindleVisual != null)
                yield return new WaitUntil(() => spindleVisual.CurrentRpm <= 0.5f);

            // zastosowanie detalu – wewnątrz WorkpieceController uruchomi się zaciskanie
            if (workpieceController != null)
                workpieceController.ApplyFromMm(material, dMm, lMm);

            ExitSafeState();

            _startBlocked = false;

            
            latheButtonsUI?.RefreshLampsExternal();
        }
    }
}