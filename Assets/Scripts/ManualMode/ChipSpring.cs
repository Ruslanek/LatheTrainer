using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ChipSpring : MonoBehaviour
{
    private LineRenderer lr;

    private float lifeTime;
    private float speed;
    private float spread;
    private float time;

    private Vector3 startPos;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 20;
    }

    public void Play(float speed, float spread, Color color, float lifeTime)
    {
        this.speed = speed;
        this.spread = spread;
        this.lifeTime = lifeTime;

        lr.startColor = color;
        lr.endColor = color;

        startPos = transform.position;
        time = 0f;
    }

    private void Update()
    {
        time += Time.deltaTime;

        float t = time * speed;

        for (int i = 0; i < lr.positionCount; i++)
        {
            float x = i * 0.03f;
            float y = Mathf.Sin(t + i * spread) * 0.02f;

            lr.SetPosition(i, startPos + new Vector3(x, y, 0));
        }

        // powoli „odleciało”
        transform.position += Vector3.down * Time.deltaTime * 0.3f;

        if (time >= lifeTime)
            Destroy(gameObject);
    }
}