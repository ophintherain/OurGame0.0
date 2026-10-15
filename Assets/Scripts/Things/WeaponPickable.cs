using UnityEngine;
public class WeaponPickable : MonoBehaviour
{
    [Header("拾取设置")]
    public BackpackManager backpackManager;
    private WeaponData weaponData;

    private void Awake()
    {
        weaponData = GetComponent<WeaponData>();
    }

    void OnMouseDown()
    {
        if (weaponData == null || backpackManager == null)
        {
            Debug.LogWarning("武器缺少WeaponData或者背包引用");
            return;
        }
        //拾取武器，加入背包
        backpackManager.PickupWeapon(weaponData);
        //拾取后销毁武器物体（可选，注释掉可以保留武器）
        Destroy(gameObject);
    }
}
