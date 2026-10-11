using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Menu screen: Previous / Next through the roster with a rotating 3D preview rendered into a RawImage.
public class CharacterSelect : MonoBehaviour
{
    [SerializeField] private CharacterRoster roster;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text counterText;

    [Header("3D Preview")]
    [SerializeField] private RawImage previewImage;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private Transform previewAnchor;
    [Tooltip("Plays the idle animation on the preview model.")]
    [SerializeField] private RuntimeAnimatorController previewAnimator;
    [SerializeField] private int previewLayer = 31;
    [SerializeField] private int previewResolution = 768;
    [SerializeField] private float rotateSpeed = 35f;

    private int index;
    private GameObject previewInstance;
    private RenderTexture previewTexture;

    private void Awake()
    {
        if (previewCamera != null && previewImage != null)
        {
            previewTexture = new RenderTexture(previewResolution, previewResolution, 24);
            previewTexture.name = "CharacterPreview";
            previewCamera.targetTexture = previewTexture;
            previewCamera.cullingMask = 1 << previewLayer;
            previewImage.texture = previewTexture;
        }
    }

    private void OnEnable()
    {
        if (roster == null || roster.Count == 0)
        {
            return;
        }
        index = roster.SelectedIndex;
        if (previewCamera != null)
        {
            previewCamera.enabled = true;
        }
        Show();
    }

    private void OnDisable()
    {
        if (previewCamera != null)
        {
            previewCamera.enabled = false;
        }
        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }
    }

    private void OnDestroy()
    {
        if (previewTexture != null)
        {
            previewTexture.Release();
            Destroy(previewTexture);
        }
    }

    private void Update()
    {
        if (previewInstance != null)
        {
            previewInstance.transform.Rotate(0f, rotateSpeed * Time.unscaledDeltaTime, 0f, Space.World);
        }
    }

    public void Next()
    {
        Select(index + 1);
    }

    public void Previous()
    {
        Select(index - 1);
    }

    private void Select(int newIndex)
    {
        if (roster == null || roster.Count == 0)
        {
            return;
        }
        index = (newIndex % roster.Count + roster.Count) % roster.Count;
        roster.SelectedIndex = index;
        Show();
    }

    private void Show()
    {
        CharacterRoster.Entry entry = roster.Get(index);
        if (nameText != null)
        {
            nameText.text = entry.displayName;
        }
        if (counterText != null)
        {
            counterText.SetText("{0} / {1}", index + 1, roster.Count);
        }

        if (previewInstance != null)
        {
            Destroy(previewInstance);
        }
        if (entry.prefab == null || previewAnchor == null)
        {
            return;
        }

        previewInstance = Instantiate(entry.prefab, previewAnchor.position, previewAnchor.rotation);
        SetLayerRecursively(previewInstance.transform, previewLayer);
        Animator animator = previewInstance.GetComponent<Animator>();
        if (animator != null && previewAnimator != null)
        {
            animator.runtimeAnimatorController = previewAnimator;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
