using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 文本透明度循环渐变组件，在最小与最大透明度之间来回淡入淡出。
/// </summary>
public class AlphaLoop : MonoBehaviour
{
    // 渐变目标文本
    [SerializeField]
    private TextMeshProUGUI textComponent;

    // 单程渐变时长
    [SerializeField]
    private float fadeSpeed = 1.0f;

    // 最小透明度
    [SerializeField]
    private float minAlpha = 0.2f;

    // 最大透明度
    [SerializeField]
    private float maxAlpha = 1.0f;

    /// <summary>
    /// 启动渐变循环。
    /// </summary>
    private void Start()
    {
        if (textComponent == null)
        {
            textComponent = GetComponent<TextMeshProUGUI>();
        }

        if (textComponent == null)
        {
            Debug.LogError($"[AlphaLoop] 缺少文本组件，节点名:{name}");
            return;
        }

        StartCoroutine(FadeLoop());
    }

    /// <summary>
    /// 在最小与最大透明度之间循环渐变。
    /// </summary>
    private IEnumerator FadeLoop()
    {
        bool fadeOut = true;
        while (true)
        {
            yield return FadeTo(fadeOut ? minAlpha : maxAlpha, fadeSpeed);
            fadeOut = !fadeOut;
        }
    }

    /// <summary>
    /// 将文本透明度渐变到目标值。
    /// </summary>
    /// <param name="targetAlpha">目标透明度。</param>
    /// <param name="duration">渐变时长。</param>
    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = textComponent.color.a;
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            textComponent.color = new Color(textComponent.color.r, textComponent.color.g, textComponent.color.b, alpha);
            yield return null;
        }
    }
}
