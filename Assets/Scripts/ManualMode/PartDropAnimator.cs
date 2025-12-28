using UnityEngine;

public class PartDropAnimator : MonoBehaviour
{
    [SerializeField] private float gravity = 25f;
    [SerializeField] private float angularSpeedDeg = 180f;
    [SerializeField] private float extraOffscreenMargin = 1.5f;

    private Transform part;
    private Vector3 velocity;
    private System.Action onFinished;
    private bool playing;

    private Vector3 initialLocalScale;
    private Transform initialParent;

    public void Play(Transform partTransform, System.Action onFinishedCallback)
    {
        part = partTransform;
        onFinished = onFinishedCallback;
        velocity = Vector3.zero;
        playing = true;

        initialLocalScale = part.localScale;
        initialParent = part.parent;

        Debug.Log($"[Drop] Play on {part.name} pos={part.position} scale={part.localScale} parent={(initialParent ? initialParent.name : "null")}");
    }

    private void Update()
    {
        if (!playing || part == null) return;

        // jeśli ktoś nagle zmieni parent lub scale — przywracamy poprzednie wartości
        if (part.parent != initialParent) part.SetParent(initialParent, true);
        if (part.localScale != initialLocalScale) part.localScale = initialLocalScale;

        velocity.y -= gravity * Time.deltaTime;
        part.position += velocity * Time.deltaTime;
        part.Rotate(0f, 0f, angularSpeedDeg * Time.deltaTime);

        if (IsBelowScreen(part.position))
        {
            playing = false;

            Debug.Log("[Drop] Finished");

            onFinished?.Invoke();
            onFinished = null;

            Destroy(part.gameObject);
            part = null;
        }
    }

    private bool IsBelowScreen(Vector3 worldPos)
    {
        Vector3 vp = Camera.main.WorldToViewportPoint(worldPos);
        return vp.y < -extraOffscreenMargin;
    }
}