using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プレイヤーへ「知っておいてほしい出来事」を順番に表示する共通通知パネルです。
/// 開発完了・作成完了・レベルアップ・人数到達など、ゲーム中の各システムから
/// ShowMessage を呼ぶだけで利用できます。
/// 複数通知が同時に発生した場合はキューへ溜め、ボタンを押すたびに次の通知を表示します。
/// </summary>
public class NotificationPanelUI : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("実際にON/OFFする通知パネル本体。NotificationPanelUI自身とは別の子Objectを推奨します。")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("通知本文を表示するTMPテキスト。")]
    [SerializeField] private TMP_Text messageText;
    [Tooltip("通知を閉じる/次へ進む共通ボタン。")]
    [SerializeField] private Button closeButton;
    [Tooltip("CloseButton内のラベル。未設定なら子のTMP_Textを自動取得します。")]
    [SerializeField] private TMP_Text closeButtonText;

    [Header("ボタン文言")]
    [SerializeField] private string nextButtonLabel = "次へ";
    [SerializeField] private string closeButtonLabel = "閉じる";

    private readonly Queue<string> pendingMessages = new();
    private bool isShowing;

    public bool IsShowing => isShowing;
    public int PendingCount => pendingMessages.Count;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        AutoFindReferences();

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseCurrent);

        SetPanelVisible(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseCurrent);
    }

    /// <summary>
    /// 通知を1件追加します。
    /// 何も表示していない時は即表示、表示中なら次の通知として待機します。
    /// </summary>
    public void ShowMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        string normalized = message.Trim();

        if (isShowing)
        {
            pendingMessages.Enqueue(normalized);
            UpdateButtonLabel();
            return;
        }

        Display(normalized);
    }

    /// <summary>
    /// 見出し + 本文の形で通知したい時の補助メソッド。
    /// </summary>
    public void ShowMessage(string headline, string detail)
    {
        if (string.IsNullOrWhiteSpace(headline))
        {
            ShowMessage(detail);
            return;
        }

        if (string.IsNullOrWhiteSpace(detail))
        {
            ShowMessage(headline);
            return;
        }

        ShowMessage($"{headline.Trim()}\n{detail.Trim()}");
    }

    /// <summary>
    /// 従来のCloseButtonから呼ばれる共通処理。
    /// 次の通知がある間は「次へ」、最後だけ「閉じる」として動作します。
    /// </summary>
    public void CloseCurrent()
    {
        if (!isShowing)
            return;

        if (pendingMessages.Count > 0)
        {
            Display(pendingMessages.Dequeue());
            return;
        }

        isShowing = false;
        if (messageText != null)
            messageText.text = string.Empty;
        SetPanelVisible(false);
    }

    /// <summary>
    /// デバッグやシーン切替時に通知を全消去したい場合に使用します。
    /// </summary>
    public void ClearAll()
    {
        pendingMessages.Clear();
        isShowing = false;
        if (messageText != null)
            messageText.text = string.Empty;
        SetPanelVisible(false);
    }

    private void Display(string message)
    {
        AutoFindReferences();

        isShowing = true;
        if (messageText != null)
            messageText.text = message;

        SetPanelVisible(true);
        UpdateButtonLabel();

        // 他UIより前面へ出したいので、同じ親の中では最後尾へ移動します。
        if (panelRoot != null)
            panelRoot.transform.SetAsLastSibling();
    }

    private void UpdateButtonLabel()
    {
        AutoFindReferences();
        if (closeButtonText != null)
            closeButtonText.text = pendingMessages.Count > 0 ? nextButtonLabel : closeButtonLabel;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null && panelRoot.activeSelf != visible)
            panelRoot.SetActive(visible);
    }

    private void AutoFindReferences()
    {
        Transform searchRoot = panelRoot != null ? panelRoot.transform : transform;

        if (messageText == null)
        {
            TMP_Text[] texts = searchRoot.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text != null && (text.gameObject.name == "MessageText" || text.gameObject.name == "NotificationText"))
                {
                    messageText = text;
                    break;
                }
            }
        }

        if (closeButton == null)
        {
            Button[] buttons = searchRoot.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button != null && (button.gameObject.name == "CloseButton" || button.gameObject.name == "OKButton" || button.gameObject.name == "NextButton"))
                {
                    closeButton = button;
                    break;
                }
            }
        }

        if (closeButtonText == null && closeButton != null)
            closeButtonText = closeButton.GetComponentInChildren<TMP_Text>(true);
    }
}
