using LatheTrainer.Machine;
using TMPro;
using UnityEngine;

namespace LatheTrainer.UI
{
    public class WorkpieceSetupPanel : MonoBehaviour
    {
        [Header("Refs")]
        public LatheStateController lathe;
        public TopBarUI topBarUi;

        [Header("UI")]
        public TMP_InputField diameterInput;
        public TMP_InputField lengthInput;

       

        private MaterialType _selected = MaterialType.Steel;

        public void SelectSteel() => _selected = MaterialType.Steel;
        public void SelectAlu() => _selected = MaterialType.Aluminium;
        public void SelectBrass() => _selected = MaterialType.Brass;

        public void OnOk()
        {
            if (lathe == null)
            {
                Debug.LogError("WorkpieceSetupPanel: brak przypisanej referencji lathe");
                return;
            }
           
            float dMm = ParseOrDefault(diameterInput, 100f);
            float lMm = ParseOrDefault(lengthInput, 150f);

            // Aktualnie przekazujemy wartości w mm (najczystsza opcja).
            // Przeliczenie na jednostki niech wykonuje WorkpieceController (jedno źródło prawdy).
            lathe.ApplyWorkpieceMm(_selected, dMm, lMm);

            lathe.ExitSafeState();

            // W razie potrzeby zamknij okno tak jak wcześniej, za pomocą topBarUi
            // topBarUi?.CloseAll();
        }

        private float ParseOrDefault(TMP_InputField input, float def)
        {
            if (input == null) return def;
            return float.TryParse(input.text, out var v) ? v : def;
        }
    }
}