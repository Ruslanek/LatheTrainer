using System.Collections;
using UnityEngine;
using LatheTrainer.Machine;
using LatheTrainer.Core;
using LatheTrainer.UI;

public class ToolChangeAnimator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AxisMotionController motion;
    [SerializeField] private ToolPositionController toolPos;
    [SerializeField] private Transform toolMountPoint;
    [SerializeField] private ToolModalController toolModal;
    [SerializeField] private TopBarUI topBar;

    [Header("Animation")]
    [SerializeField] private float downOffsetY = 3.0f;
    [SerializeField] private float outTime = 0.35f;
    [SerializeField] private float inTime = 0.35f;

    private GameObject _currentTool;
    private Coroutine _routine;

    [Header("Default tool")]
    [SerializeField] private GameObject defaultToolPrefab;

   

    private void Awake()
    {
        if (toolModal != null)
            toolModal.OnConfirmed += RequestChange;
    }

    private void Start()
    {
        //if (_currentTool == null && toolMountPoint != null && toolMountPoint.childCount > 0)
        //     _currentTool = toolMountPoint.GetChild(0).gameObject;

        // Jeżeli w scenie jest już narzędzie — ustawiamy je jako aktualne
        if (toolMountPoint.childCount > 0)
        {
            _currentTool = toolMountPoint.GetChild(0).gameObject;
            return;
        }

        // Jeżeli brak narzędzia — tworzymy domyślne
        if (defaultToolPrefab != null)
        {
            _currentTool = Instantiate(defaultToolPrefab, toolMountPoint);
            _currentTool.transform.localPosition = Vector3.zero;
            _currentTool.transform.localRotation = Quaternion.identity;
            _currentTool.transform.localScale = Vector3.one;

           

            Debug.Log("[ToolChange] Default tool spawned");
        }
        else
        {
            Debug.LogWarning("[ToolChange] No default tool assigned");
        }


    }

    public void RequestChange(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[ToolChange] Prefab is NULL");
            return;
        }

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ChangeRoutine(prefab));
    }

    private IEnumerator ChangeRoutine(GameObject newPrefab)
    {
        if (!motion || !toolMountPoint)
        {
            Debug.LogWarning("[ToolChange] Missing refs");
            _routine = null;
            yield break;
        }

        Debug.Log("[ToolChange] Start");

        // 1) Przejazd do HOME i oczekiwanie na zakończenie
        yield return StartCoroutine(motion.GoHomeRoutine());

        // 2) Cofnięcie aktualnego narzędzia w dół
        if (_currentTool != null)
            yield return MoveLocalY(_currentTool.transform, -downOffsetY, outTime);

        // 3) Usuwamy poprzednie narzędzie
        if (_currentTool != null)
            Destroy(_currentTool);

        // 4) Tworzymy nowe narzędzie poniżej i wprowadzamy je na punkt montażowy
        _currentTool = Instantiate(newPrefab, toolMountPoint);
        _currentTool.transform.localRotation = Quaternion.identity;
        _currentTool.transform.localScale = Vector3.one;
        _currentTool.transform.localPosition = new Vector3(0f, -downOffsetY, 0f);

       

        // 5) Przemieszczamy narzędzie w górę do pozycji (Y = 0)
        yield return MoveLocalY(_currentTool.transform, 0f, inTime, absoluteTarget: true);

        // 6) Zamykamy okno
        if (topBar != null)
            topBar.CloseAll();

        Debug.Log("[ToolChange] Done");
        _routine = null;
    }

    private IEnumerator MoveLocalY(Transform tr, float y, float time, bool absoluteTarget = false)
    {
        if (!tr) yield break;

        Vector3 start = tr.localPosition;
        Vector3 target = absoluteTarget
            ? new Vector3(start.x, y, start.z)
            : new Vector3(start.x, start.y + y, start.z);

        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / time);
            tr.localPosition = Vector3.Lerp(start, target, k);
            yield return null;
        }

        tr.localPosition = target;
    }
}