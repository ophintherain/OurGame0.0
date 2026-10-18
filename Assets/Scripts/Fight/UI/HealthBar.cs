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
        float previousHealth = currentHealth;
        currentHealth = currentHealth + hp;
        SetHealth(currentHealth);

        if(currentHealth < previousHealth)
        {
            Spotlight.UISoundPlayer.Play(Spotlight.UISoundCue.ExplorationDamage);
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


}