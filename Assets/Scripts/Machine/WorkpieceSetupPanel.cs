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
        public TMP_Text errorText;   

        [Header("Limits (mm)")]
        [SerializeField] private float minDiameter = 100f;
        [SerializeField] private float maxDiameter = 400f;
        [SerializeField] private float minLength = 100f;
        [SerializeField] private float maxLength = 700f;

        private MaterialType _selected = MaterialType.Aluminium;

        public void SelectSteel() => _selected = MaterialType.Stal;
        public void SelectAlu() => _selected = MaterialType.Aluminium;
        public void SelectBrass() => _selected = MaterialType.Mosiądz;

        public void OnOk()
        {
            if (lathe == null)
            {
                Debug.LogError("WorkpieceSetupPanel: brak przypisanej referencji lathe");
                return;
            }

            float dMm = ParseOrDefault(diameterInput, -1f);
            float lMm = ParseOrDefault(lengthInput, -1f);

            // ❌ nieprawidłowe dane wejściowe
            if (dMm <= 0 || lMm <= 0)
            {
                ShowError("Wprowadź poprawne wartości liczbowe średnicy i długości.");
                return;
            }

            // ❌ poza zakresem
            if (dMm < minDiameter || dMm > maxDiameter ||
                lMm < minLength || lMm > maxLength)
            {
                ShowError(
                    $"Brak wybranego materiału na magazynie.\n" +
                    $"Dostępne wymiary:\n" +
                    $"Średnica: {minDiameter}–{maxDiameter} mm\n" +
                    $"Długość: {minLength}–{maxLength} mm"
                        );
                return;
            }

            // ✅ wszystko OK — czyścimy błąd
            HideError();

            // przekazujemy dane
            lathe.ApplyWorkpieceMm(_selected, dMm, lMm);
            lathe.ExitSafeState();

            // W razie potrzeby zamknij okno tak jak wcześniej, za pomocą topBarUi 
             topBarUi?.CloseAll();
        }

        private float ParseOrDefault(TMP_InputField input, float def)
        {
            if (input == null) return def;
            return float.TryParse(input.text, out var v) ? v : def;
        }

        private void ShowError(string msg)
        {
            if (errorText == null) return;
            errorText.text = msg;
            errorText.gameObject.SetActive(true);
        }

        private void HideError()
        {
            if (errorText == null) return;
            errorText.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            HideError();
            diameterInput.text = "100";
            lengthInput.text = "150";
        }
    }
}