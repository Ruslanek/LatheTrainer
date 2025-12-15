using UnityEngine;
using LatheTrainer.Core;

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

        public void EnterSafeState()
        {
            if (spindleVisual != null)
            {
                spindleVisual.SetCommandedRpm(0f); // zatrzymać wrzeciono
            }

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
            if (spindleVisual == null) return;
            spindleVisual.StopSpindle();
        }

        public void PressReverseToggle(bool reverseOn)
        {
            if (spindleVisual == null) return;
            spindleVisual.SetSpinDirection(reverseOn);
        }
    }
}