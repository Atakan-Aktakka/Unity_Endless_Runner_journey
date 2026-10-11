using UnityEngine;

// Selectable characters, shared by the menu (CharacterSelect) and the game scene (CharacterSkinLoader)
[CreateAssetMenu(fileName = "CharacterRoster", menuName = "Endless Runner/Character Roster")]
public class CharacterRoster : ScriptableObject
{
    public const string SelectedCharacterKey = "SelectedCharacter";

    [System.Serializable]
    public class Entry
    {
        public string displayName;
        [Tooltip("Humanoid model with a Bip001 rig (same skeleton as the Hero's current model).")]
        public GameObject prefab;
    }

    [SerializeField] private Entry[] characters;

    public int Count { get { return characters == null ? 0 : characters.Length; } }

    public Entry Get(int index)
    {
        return characters[Clamp(index)];
    }

    public int SelectedIndex
    {
        get { return Clamp(PlayerPrefs.GetInt(SelectedCharacterKey, 0)); }
        set
        {
            PlayerPrefs.SetInt(SelectedCharacterKey, Clamp(value));
            PlayerPrefs.Save();
        }
    }

    private int Clamp(int index)
    {
        return Count == 0 ? 0 : Mathf.Clamp(index, 0, Count - 1);
    }
}
