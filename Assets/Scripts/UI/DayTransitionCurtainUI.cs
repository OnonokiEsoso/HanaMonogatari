using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 1日の切り替わりを、画面上から降りてくる黒いUI Imageで隠して演出します。
/// Imageが画面全体を覆った瞬間に、渡された翌日処理を実行し、
/// その後Imageを画面下へ抜いて次の日の画面を見せます。
/// </summary>
public class DayTransitionCurtainUI : MonoBehaviour
{
    [Header("黒幕")]
    [Tooltip("画面全体を覆う黒いImageのRectTransformを設定します。")]
    [SerializeField] private RectTransform curtainRect;

    [Header("演出時間")]
    [Min(0.01f)] [SerializeField] private float slideInDuration = 0.55f;
    [Min(0f)] [SerializeField] private float coveredHoldDuration = 0.12f;
    [Min(0.01f)] [SerializeField] private float slideOutDuration = 0.55f;

    [Header("移動距離")]
    [Tooltip("0なら親Rectと黒幕の高さから自動計算します。通常は0のままでOKです。")]
    [Min(0f)] [SerializeField] private float overrideTravelDistance = 0f;

    private bool isPlaying;
    private float baseX;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        if (curtainRect == null)
            curtainRect = transform as RectTransform;

        if (curtainRect == null)
            return;

        baseX = curtainRect.anchoredPosition.x;
        MoveImmediately(GetTravelDistance());
        curtainRect.gameObject.SetActive(false);
    }

    /// <summary>
    /// 黒幕を上から降ろし、全面黒になった瞬間にmidpointActionを実行してから下へ抜きます。
    /// </summary>
    public IEnumerator PlayTransition(Action midpointAction)
    {
        if (isPlaying || curtainRect == null)
            yield break;

        isPlaying = true;
        curtainRect.gameObject.SetActive(true);
        curtainRect.SetAsLastSibling();

        float travel = GetTravelDistance();
        MoveImmediately(travel);

        yield return SlideY(travel, 0f, slideInDuration);

        // 画面が完全に黒で覆われている間に、日付更新・UI更新などを済ませる。
        midpointAction?.Invoke();

        if (coveredHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(coveredHoldDuration);

        yield return SlideY(0f, -travel, slideOutDuration);

        curtainRect.gameObject.SetActive(false);
        MoveImmediately(travel);
        isPlaying = false;
    }

    private IEnumerator SlideY(float fromY, float toY, float duration)
    {
        if (duration <= 0f)
        {
            MoveImmediately(toY);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // 少し滑らかに加減速する。
            float eased = t * t * (3f - 2f * t);
            MoveImmediately(Mathf.LerpUnclamped(fromY, toY, eased));
            yield return null;
        }

        MoveImmediately(toY);
    }

    private float GetTravelDistance()
    {
        if (overrideTravelDistance > 0f)
            return overrideTravelDistance;

        float curtainHeight = curtainRect != null ? curtainRect.rect.height : 1080f;
        float parentHeight = curtainHeight;

        if (curtainRect != null && curtainRect.parent is RectTransform parentRect)
            parentHeight = parentRect.rect.height;

        // 中央位置から完全に画面外へ出るために必要な距離。
        return Mathf.Max(1f, (parentHeight + curtainHeight) * 0.5f);
    }

    private void MoveImmediately(float y)
    {
        if (curtainRect == null) return;
        curtainRect.anchoredPosition = new Vector2(baseX, y);
    }
}
