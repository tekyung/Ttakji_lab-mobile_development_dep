using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class CheckCard : MonoBehaviour
{
    private bool isPressed = false;
    private float pressTime = 0f;
    private const float requiredTime = 1.5f;

    public UnityEvent onLongPress;

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        pressTime = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPressed = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (isPressed)
        {
            pressTime += Time.deltaTime;
            if (pressTime >= requiredTime)
            {
                onLongPress.Invoke();
                isPressed = false;
            }
        }
    }
}
