using UnityEngine;

// Swaps the Hero's visual model for the character chosen in the menu.
// HeroCharacterController, CharacterController and the Animator (with its controller) stay on the Hero;
// only the model and its humanoid avatar change, so the same animations retarget onto the new body.
[RequireComponent(typeof(Animator))]
public class CharacterSkinLoader : MonoBehaviour
{
    [SerializeField] private CharacterRoster roster;
    [Tooltip("The model currently under the Hero (it is replaced).")]
    [SerializeField] private Transform currentModel;

    private void Awake()
    {
        if (roster == null || roster.Count == 0 || currentModel == null)
        {
            return;
        }

        CharacterRoster.Entry entry = roster.Get(roster.SelectedIndex);
        if (entry == null || entry.prefab == null)
        {
            return;
        }

        Transform model = Instantiate(entry.prefab, currentModel.parent).transform;
        model.name = entry.prefab.name;
        model.SetSiblingIndex(currentModel.GetSiblingIndex());
        model.localPosition = currentModel.localPosition;
        model.localRotation = currentModel.localRotation;
        model.localScale = currentModel.localScale;

        // The Hero's root Animator drives the new rig; the model's own Animator must not
        Avatar avatar = null;
        Animator modelAnimator = model.GetComponent<Animator>();
        if (modelAnimator != null)
        {
            avatar = modelAnimator.avatar;
            modelAnimator.enabled = false;
            Destroy(modelAnimator);
        }

        // Detach the old model right away so the Animator cannot bind to its bones
        currentModel.gameObject.SetActive(false);
        currentModel.SetParent(null);
        Destroy(currentModel.gameObject);
        currentModel = model;

        Animator heroAnimator = GetComponent<Animator>();
        if (heroAnimator != null)
        {
            if (avatar != null && avatar.isHuman)
            {
                heroAnimator.avatar = avatar;
            }
            heroAnimator.Rebind();
        }
    }
}
