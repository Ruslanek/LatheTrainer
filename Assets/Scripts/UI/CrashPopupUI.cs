using System;
using UnityEngine;
using UnityEngine.UI;
using LatheTrainer.Core;

namespace LatheTrainer.UI
{
    public class CrashPopupUI : MonoBehaviour
    {
        public static CrashPopupUI Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private GameObject root;   // CrashPopup (sam obiekt)
        [SerializeField] private Text messageText;  // Message
        [SerializeField] private Button okButton;   // OkButton

        [Header("Freeze")]
        [SerializeField] private bool freezeByTimeScale = true;

        private Action _onClose;
        private float _prevTimeScale = 1f;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (root == null) root = gameObject;

            okButton.onClick.AddListener(Close);
            root.SetActive(false);
        }

        public void Show(string message, Action onClose = null)
        {
            _onClose = onClose;

            if (messageText) messageText.text = message;

            LatheSafetyLock.Lock();   // <—
            root.SetActive(true);

            if (freezeByTimeScale)
            {
                _prevTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
        }

        public void Close()
        {
            if (freezeByTimeScale)
                Time.timeScale = _prevTimeScale;

            root.SetActive(false);

            LatheSafetyLock.Unlock(); // <—

            _onClose?.Invoke();
            _onClose = null;
        }
    }
}