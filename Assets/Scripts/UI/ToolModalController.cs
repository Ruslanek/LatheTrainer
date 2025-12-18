using System;
using UnityEngine;
using UnityEngine.UI;


namespace LatheTrainer.UI
{
    public class ToolModalController : MonoBehaviour
    {
        [Header("Root")]
        // [SerializeField] private GameObject modalOverlay; // ModalOverlay (włączanie / wyłączanie)
        [SerializeField] private Button btnOk;
        [SerializeField] private Button btnCancel;

        [Header("Tool buttons")]
        [SerializeField] private ToolButton[] toolButtons;

        public GameObject SelectedPrefab { get; private set; }

        public event Action<GameObject>? OnConfirmed;

        private ToolButton? _selected;

        private GameObject _lastSelectedPrefab;

        private void Awake()
        {
           
            Debug.Log($"[ToolModal] Awake on '{gameObject.name}'");

            btnOk.onClick.AddListener(Confirm);
            

            foreach (var tb in toolButtons)
                tb.Init(Select);

          

            btnOk.interactable = false;
        }

       

        private void Select(ToolButton button)
        {
            Debug.Log($"[ToolModal] Select() click. Prefab={(button.ToolPrefab != null ? button.ToolPrefab.name : "NULL")}");


            if (_selected != null)
                _selected.SetSelected(false);

            _selected = button;
            _selected.SetSelected(true);

            SelectedPrefab = button.ToolPrefab;
            btnOk.interactable = SelectedPrefab != null;
        }

        private void Confirm()
        {
            Debug.Log($"[ToolModal] Confirm() Selected={(SelectedPrefab != null ? SelectedPrefab.name : "NULL")}");

            if (SelectedPrefab == null) return;

            Debug.Log("[ToolModal] Invoking OnConfirmed");
            OnConfirmed?.Invoke(SelectedPrefab);
            _lastSelectedPrefab = SelectedPrefab;
        }

        public void SetSelectedTool(GameObject prefab)
        {
            if (prefab == null) return;

            // znajdź przycisk, którego ToolPrefab == prefab
            foreach (var tb in toolButtons)
            {
                if (tb != null && tb.ToolPrefab == prefab)
                {
                    Select(tb); // spowoduje podświetlenie i aktywuje przycisk OK
                    return;
                }
            }
        }

        private void OnEnable()
        {
            // po wyświetleniu okna przywracamy podświetlenie
            if (_lastSelectedPrefab != null)
                SetSelectedTool(_lastSelectedPrefab);
        }
    }

    [Serializable]
    public class ToolButton
    {
        [SerializeField] private Button button;
        [SerializeField] private Image highlight;        // tło podświetlenia
        [SerializeField] private Image icon;             // ikona narzędzia (PNG)
        [SerializeField] private Image highlightFrame;   // ramka (opcjonalnie)
        [SerializeField] private GameObject toolPrefab;
        [SerializeField] private GameObject highlightGO; // obiekt Highlight (Image)

        [SerializeField]
        private Color selectedColor = new Color(0.7f, 1f, 0.7f, 1f);

        public GameObject ToolPrefab => toolPrefab;

        private Action<ToolButton>? _onClick;


        public void Init(Action<ToolButton> onClick)
        {
            _onClick = onClick;

            if (button == null)
            {
                Debug.LogError("[ToolModal] ToolButton has NULL Button reference!");
                return;
            }

            button.onClick.AddListener(() => _onClick?.Invoke(this));

            SetSelected(false);
        }



        public void SetSelected(bool selected)
        {
            if (highlightGO != null)
                highlightGO.SetActive(selected);
        }
    }
}