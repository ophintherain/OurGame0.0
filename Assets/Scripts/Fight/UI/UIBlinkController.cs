using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIBlinkController : MonoBehaviour
{
    [Header("UI引用")]
    public Image headImage;     // 角色头像
    public Image hpFillImage;    // 血条填充图片
    public Image hpBarImage;   // 血条图片

    [Header("闪烁参数")]
    public float blinkFrequency = 0.5f; // 闪烁一次（亮→暗→亮）的间隔，越小闪越快
    public float blinkFullTime = 1.0f;   // 闪烁的总时间，闪烁次数等于blinkFullTime/blinkFrequency向下取整
    private Coroutine _blinkCoroutine;

    /// <summary>
    /// 调用这个函数，开启头像+血条一起闪烁
    /// </summary>
    /// <param name="blinkTotalTime">闪烁持续总时间（秒）</param>
    /// 

    public void StartBlink(float blinkTotalTime)
    {
        // 如果正在闪烁，先停止旧的闪烁协程，防止叠加
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            SetUIVisible(true);
        }

        _blinkCoroutine = StartCoroutine(BlinkCoroutine(blinkTotalTime));
    }

    /// <summary>
    /// 调用这个函数，按脚本设定的时间(blinkFullTime)开始闪烁
    /// </summary>

    public void AutoBilnk()
    {
        StartBlink(blinkFullTime);
    }


    /// <summary>
    /// 停止闪烁，强制恢复显示
    /// </summary>
    public void StopBlink()
    {
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }
        SetUIVisible(true);
    }

    private IEnumerator BlinkCoroutine(float totalTime)
    {
        float timer = 0f;
        bool isShow = true;

        while (timer < totalTime)
        {
            isShow = !isShow;
            SetUIVisible(isShow);

            timer += blinkFrequency;
            yield return new WaitForSeconds(blinkFrequency);
        }

        // 闪烁结束强制恢复可见
        SetUIVisible(true);
        _blinkCoroutine = null;
    }

    // 同时设置头像和血条的显隐（透明度）
    private void SetUIVisible(bool visible)
    {
        if (headImage != null)
        {
            Color c = headImage.color;
            c.a = visible ? 1f : 0f;
            headImage.color = c;
        }

        if (hpBarImage != null)
        {
            Color c = hpBarImage.color;
            c.a = visible ? 1f : 0f;
            hpBarImage.color = c;
        }

        if (hpFillImage != null)
        {
            Color c = hpFillImage.color;
            c.a = visible ? 1f : 0f;
            hpFillImage.color = c;
        }

    }
}