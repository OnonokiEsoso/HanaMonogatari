using System.Collections;
using UnityEngine;

/// <summary>
/// ゲーム開始後、指定秒数が経過したらこのGameObjectを非表示にします。
/// DebugManager上の一時表示テキストなどに使用します。
/// </summary>
public class HideAfterStart : MonoBehaviour
{
    [Min(0f)]
    [SerializeField] private float hideDelay = 0.5f;

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(hideDelay);
        gameObject.SetActive(false);
    }
}
