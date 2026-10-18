using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class BackpackManager : MonoBehaviour
{
    [Header("背包UI引用")]
    [Tooltip("背包整体背景图片对象")]
    public Image backpackBg;
    [Tooltip("武器分类容器：所有武器格子生成在这个Transform下")]
    public Transform weaponGridRoot;
    [Tooltip("物品分类容器：所有物品格子生成在这个Transform下")]
    public Transform itemGridRoot;
    [Tooltip("单个物品格子预制体（包含Image组件用来显示物品图标）")]
    public GameObject itemCellPrefab;
    [Tooltip("武器分类切换按钮")]
    public Button btnWeaponTab;
    [Tooltip("物品分类切换按钮")]
    public Button btnItemTab;

    // 物品数据结构
    [System.Serializable]
    public class BackpackItem
    {
        public int itemId;
        public int count;
        public Sprite iconSprite;
    }

    // 存储背包内所有物品
    private List<BackpackItem> backpackItemList = new List<BackpackItem>();
    // 当前激活的分类，true=武器，false=物品
    private bool isWeaponTabActive = true;

    private void Start()
    {
        // 背包默认隐藏
        backpackBg.gameObject.SetActive(false);
        weaponGridRoot.gameObject.SetActive(true);
        itemGridRoot.gameObject.SetActive(false);

        // 绑定按钮事件
        GetComponent<Button>().onClick.AddListener(ToggleBackpack);
        btnWeaponTab.onClick.AddListener(() => SwitchTab(true));
        btnItemTab.onClick.AddListener(() => SwitchTab(false));
    }

    /// <summary>
    /// 背包按钮点击：打开/关闭背包面板
    /// </summary>
    void ToggleBackpack()
    {
        bool newState = !backpackBg.gameObject.activeSelf;
        backpackBg.gameObject.SetActive(newState);
        Spotlight.UISoundPlayer.Play(newState
            ? Spotlight.UISoundCue.OpenBackpack : Spotlight.UISoundCue.CloseBackpack);
        if (newState)
        {
            RefreshCurrentTabGrid();
        }
    }

    /// <summary>
    /// 切换武器/物品标签页
    /// </summary>
    /// <param name="isWeapon">true=武器分类，false=物品分类</param>
    void SwitchTab(bool isWeapon)
    {
        if (isWeaponTabActive == isWeapon) return;
        isWeaponTabActive = isWeapon;
        Spotlight.UISoundPlayer.Play(Spotlight.UISoundCue.PageTurn);
        weaponGridRoot.gameObject.SetActive(isWeaponTabActive);
        itemGridRoot.gameObject.SetActive(!isWeaponTabActive);
        RefreshCurrentTabGrid();
    }

    /// <summary>
    /// 刷新当前标签页格子，按ID从小到大排序
    /// </summary>
    void RefreshCurrentTabGrid()
    {
        // 清空当前容器旧格子
        Transform targetRoot = isWeaponTabActive ? weaponGridRoot : itemGridRoot;
        foreach (Transform child in targetRoot)
        {
            Destroy(child.gameObject);
        }

        // 筛选对应分类物品，按ID升序排序
        var filteredItems = backpackItemList
            .Where(item =>
            {
                int prefix = item.itemId / 1000; //取4位ID第一位
                if (isWeaponTabActive)
                    return prefix == 1;
                else
                    return prefix == 2;
            })
            .OrderBy(item => item.itemId);

        // 动态生成格子
        foreach (var item in filteredItems)
        {
            GameObject cellObj = Instantiate(itemCellPrefab, targetRoot);
            Image iconImg = cellObj.GetComponent<Image>();
            if (iconImg != null && item.iconSprite != null)
            {
                iconImg.sprite = item.iconSprite;
            }
        }
    }

    #region 对外接口
    /// <summary>
    /// 【原有接口保留】添加物品到背包
    /// </summary>
    /// <param name="itemId">4位物品ID：1开头武器，2开头物品</param>
    /// <param name="count">物品数量</param>
    public void AddItem(int itemId, int count)
    {
        // ID合法性校验
        if (itemId < 1000 || itemId > 2999)
        {
            Debug.LogWarning($"物品ID {itemId} 不符合规则，必须4位，1开头武器/2开头物品");
            return;
        }
        if (count <= 0)
        {
            Debug.LogWarning($"物品数量不能小于等于0");
            return;
        }

        // 查找背包里是否已有该物品
        BackpackItem existItem = backpackItemList.FirstOrDefault(x => x.itemId == itemId);
        if (existItem != null)
        {
            existItem.count += count;
        }
        else
        {
            BackpackItem newItem = new BackpackItem();
            newItem.itemId = itemId;
            newItem.count = count;
            newItem.iconSprite = null;
            backpackItemList.Add(newItem);
        }
        RefreshCurrentTabGrid();
    }

    /// <summary>
    /// 【新增】拾取武器，读取WeaponData信息存入背包
    /// </summary>
    public void PickupWeapon(WeaponData weaponData)
    {
        if (weaponData == null)
        {
            Debug.LogWarning("拾取武器失败，WeaponData为空");
            return;
        }
        int id = weaponData.weaponId;
        // 校验武器ID必须是1开头4位数
        if (id < 1000 || id >= 2000)
        {
            Debug.LogWarning($"武器ID {id} 不是1开头4位武器ID");
            return;
        }

        BackpackItem existItem = backpackItemList.FirstOrDefault(x => x.itemId == id);
        if (existItem != null)
        {
            existItem.count += 1;
        }
        else
        {
            BackpackItem newItem = new BackpackItem();
            newItem.itemId = id;
            newItem.count = 1;
            newItem.iconSprite = weaponData.backpackIcon;
            backpackItemList.Add(newItem);
        }
        RefreshCurrentTabGrid();
    }
    #endregion
}

