using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホーム画面から開く「開発」パネルの表示・非表示だけを担当します。
/// 開発内容そのものの表示や操作は、今後このパネル内へ追加していきます。
/// </summary>
public class DevelopmentPanelUI : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("開発パネル全体のルート。未設定ならこのGameObjectを使用します。")]
    [SerializeField] private GameObject panelRoot;

    [Header("ボタン")]
    [Tooltip("パネルを閉じるボタン。任意。")]
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);

        // 初期表示はHierarchy側のActive状態で管理します。
        // AwakeでHidePanelを呼ぶと、初回クリック時に表示が打ち消されるため行いません。
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HidePanel);
    }

    public void ShowPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
