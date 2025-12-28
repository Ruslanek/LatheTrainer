using UnityEngine;
using LatheTrainer.Machine;

public class KeyboardAxisInput : MonoBehaviour
{
    [SerializeField] private AxisMotionController axisMotion;

    private bool _moving;

    private void Update()
    {
        if (!axisMotion) return;

        // kierunki
        float dirX = 0f;
        float dirZ = 0f;

        // ⬆ / ⬇ → X
        if (Input.GetKey(KeyCode.UpArrow)) dirX += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) dirX -= 1f;

        // ⬅ / ➡ → Z
        if (Input.GetKey(KeyCode.RightArrow)) dirZ += 1f;
        if (Input.GetKey(KeyCode.LeftArrow)) dirZ -= 1f;

        bool anyKey = dirX != 0f || dirZ != 0f;

        // 🔹 TRYB INKREMENTALNY
        if (axisMotion.IsIncrementMode())
        {
            if (anyKey && !_moving)
            {
                axisMotion.StepMove(dirX, dirZ);
                _moving = true;   // blokujemy ponowne wyzwolenie, dopóki nie zostanie zwolnione
            }

            if (!anyKey)
                _moving = false;

            return;
        }

        // 🔹 TRYB CIĄGŁY (Jog / Rapid)
        if (anyKey)
        {
            axisMotion.StartMove(dirX, dirZ);
            _moving = true;
        }
        else if (_moving)
        {
            axisMotion.StopMove();
            _moving = false;
        }
    }
}