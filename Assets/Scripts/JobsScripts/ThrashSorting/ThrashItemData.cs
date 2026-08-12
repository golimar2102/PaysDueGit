using UnityEngine;

[System.Serializable]
public class TrashItemData
{
    public string itemID;
    public string itemName;
    public GameObject prefab;
    public Sprite icon;
    public Vector3 rotationOffset = Vector3.zero;

    public TrashItemData() { }

    public TrashItemData(string id, string name, GameObject prefab, Sprite icon)
    {
        this.itemID = id;
        this.itemName = name;
        this.prefab = prefab;
        this.icon = icon;
    }
}