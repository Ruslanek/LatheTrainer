using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LatheTrainer.UI
{
    public class TutorialWizard : MonoBehaviour
    {
        [Header("Pages (7 panels)")]
        [SerializeField] private GameObject[] pages;

        [Header("Header UI")]
        [SerializeField] private TMP_Text stepText;
        [SerializeField] private TMP_Text titleText;

        [Header("Buttons")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextButtonText;

       

        private int index;

        private const string PrefKey = "TutorialDontShowAgain";

        public bool CanAutoShow()
        {
            return PlayerPrefs.GetInt(PrefKey, 0) == 0;
        }

        private void OnEnable()
        {
            // Za każdym razem przy otwarciu zaczynamy od kroku 1
            index = 0;
            Show(index);
        }

        public void Next()
        {
            if (index < pages.Length - 1)
            {
                index++;
                Show(index);
            }
            else
            {
                // ostatni krok – zamknięcie
                Close();
            }
        }

        public void Back()
        {
            if (index > 0)
            {
                index--;
                Show(index);
            }
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void Show(int i)
        {
            if (pages == null || pages.Length == 0) return;

            for (int p = 0; p < pages.Length; p++)
                if (pages[p] != null) pages[p].SetActive(p == i);

            if (stepText != null)
                stepText.text = $"Krok {i + 1}/{pages.Length}";

            if (backButton != null)
                backButton.interactable = i > 0;

            bool isLast = (i == pages.Length - 1);
            if (nextButtonText != null)
                nextButtonText.text = isLast ? "Gotowe" : "Dalej";

            if (titleText != null)
                titleText.text = "Samouczek";
        }
    }
}