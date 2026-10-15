using UnityEngine;
public class WeaponData : MonoBehaviour
{
    [Header("武器数据")]
    [Tooltip("4位武器ID，1开头")]
    public int weaponId;
    public string weaponName;
    [TextArea(2, 4)]
    public string weaponDescription;
    [Tooltip("背包UI格子显示的图标Sprite")]
    public Sprite backpackIcon;
}
