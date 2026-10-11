using UnityEngine;
using UnityEngine.EventSystems;

// Mobile hold-to-fire button. Put on a UI Image (Raycast Target on).
public class FireButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private WeaponController weapon;

    private void Awake()
    {
        if (weapon == null)
        {
            weapon = FindFirstObjectByType<WeaponController>();
        }
    }

    private void OnDisable()
    {
        if (weapon != null) weapon.SetTriggerHeld(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (weapon == null) return;
        weapon.Fire(); // first shot immediately
        weapon.SetTriggerHeld(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (weapon != null) weapon.SetTriggerHeld(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (weapon != null) weapon.SetTriggerHeld(false);
    }
}
