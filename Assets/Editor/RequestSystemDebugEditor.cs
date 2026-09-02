using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// RequestSystem用のデバッグメニューです。
/// 通常の15%抽選を無視して、その場で必ず依頼を1件発生させます。
/// </summary>
public static class RequestSystemDebugEditor
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("CONTEXT/RequestSystem/DEBUG: 依頼を確実に発生（ランダム）")]
    private static void ForceOfferRandomRequest(MenuCommand command)
    {
        RequestSystem requestSystem = command.context as RequestSystem;
        if (requestSystem == null)
            return;

        if (!Application.isPlaying)
        {
            Debug.LogWarning("依頼の強制発生デバッグはPlayモード中に実行してください。");
            return;
        }

        if (requestSystem.HasActiveRequest)
        {
            Debug.LogWarning($"すでに依頼が存在します：{requestSystem.CurrentRequest?.title}");
            return;
        }

        MethodInfo offerRandom = typeof(RequestSystem).GetMethod("OfferRandomRequest", PrivateInstance);
        MethodInfo offerByType = typeof(RequestSystem).GetMethod("OfferRequest", PrivateInstance);

        if (offerRandom == null || offerByType == null)
        {
            Debug.LogError("RequestSystemDebugEditor: 依頼発生メソッドが見つかりません。RequestSystemの実装を確認してください。");
            return;
        }

        // まず通常と同じ「全依頼から等確率」の抽選を1回行う。
        offerRandom.Invoke(requestSystem, null);

        // 謎のお通げを引いたものの所持花が0だった場合など、生成に失敗した時だけ
        // 花束依頼へフォールバックして、デバッグでは必ず1件発生させる。
        if (!requestSystem.HasActiveRequest)
            offerByType.Invoke(requestSystem, new object[] { RequestType.BouquetOrder });

        if (requestSystem.HasActiveRequest)
        {
            Debug.Log($"DEBUG強制発生：{requestSystem.CurrentRequest.title} / {requestSystem.CurrentRequest.requesterName} / {requestSystem.CurrentRequest.requestId}");
            EditorUtility.SetDirty(requestSystem);
        }
        else
        {
            Debug.LogError("依頼を強制発生できませんでした。RequestSystemの参照設定を確認してください。");
        }
    }
}
