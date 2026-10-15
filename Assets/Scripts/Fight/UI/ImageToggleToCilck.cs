using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))] // 自动挂载Button组件，没有会自动添加
public class ImageToggleOnClick : MonoBehaviour
{
    [Header("要切换的目标UI图片")]
    public Image targetImage;

    [Header("两张切换精灵")]
    public Sprite spriteA;
    public Sprite spriteB;

    private Button _button;
    private bool _isShowB = false;

    private void Awake()
    {
        // 获取自身身上的Button组件
        _button = GetComponent<Button>();
    }

    private void Start()
    {
        // 初始化默认显示图片A
        if (targetImage != null && spriteA != null)
        {
            targetImage.sprite = spriteA;
        }
        // 注册点击事件，点击本物体的按钮就触发切换
        _button.onClick.AddListener(ToggleSprite);
    }

    void ToggleSprite()
    {
        // 状态翻转
        _isShowB = !_isShowB;

        if (targetImage == null)
        {
            Debug.LogWarning("ImageToggleOnClick：targetImage没有赋值！", this);
            return;
        }

        targetImage.sprite = _isShowB ? spriteB : spriteA;
    }

    // 可选对外接口，外部脚本可以强制设置状态
    public void SetState(bool showB)
    {
        _isShowB = showB;
        if (targetImage != null)
        {
            targetImage.sprite = _isShowB ? spriteB : spriteA;
        }
    }
}

