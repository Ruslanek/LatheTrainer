using UnityEngine;
using UnityEngine.EventSystems;
using LatheTrainer.Machine;

public class HoldMoveButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private AxisMotionController motion;
    [SerializeField] private float dirX; // -1/0/1
    [SerializeField] private float dirZ; // -1/0/1

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!motion) return;

        if (motion.IsIncrementMode())
        {
            motion.StepMove(dirX, dirZ);     // ✅ pojedynczy krok
        }
        else
        {
            motion.StartMove(dirX, dirZ);    // ✅ ruch ciągły (przytrzymanie)
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!motion) return;
        if (!motion.IsIncrementMode())
            motion.StopMove();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!motion) return;
        if (!motion.IsIncrementMode())
            motion.StopMove();
    }
}