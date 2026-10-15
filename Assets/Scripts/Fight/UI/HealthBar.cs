using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("填充图片组件")]
    public Image fillImage;

    [Header("最大血量")]
    public float maxHealth = 100f;
    [Header("当前血量")]
    public float currentHealth=100f;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthUI();
    }

    // 调用这个方法来扣血/回血

    public void SetHealth(float hp)
    {
        currentHealth = Mathf.Clamp(hp, 0, maxHealth);
        UpdateHealthUI();
    }

    public void ChangeHealth(float hp)
    {
        currentHealth = currentHealth + hp;
        SetHealth(currentHealth);

        if(hp<0.0f)
        {
            UIBlinkController blinkUI = GetComponent<UIBlinkController>();
            if (blinkUI != null)
            {
                blinkUI.AutoBilnk();
            }
        }
        
    }

    void UpdateHealthUI()
    {
        // FillAmount = 当前血量 / 最大血量
        fillImage.fillAmount = currentHealth / maxHealth;
    }

    // 测试用：按A扣血，按D回血
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            ChangeHealth(-10f);
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            ChangeHealth(10f);
        }
    }
}