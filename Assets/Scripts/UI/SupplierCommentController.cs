using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 仕入先キャラクターの吹き出しテキストを管理します。
/// 花を仕入れた時は花の短い豆知識、家具の「効果説明」を押した時は家具効果を表示します。
/// デイリートレンド発生日は、朝の待機セリフでその日の傾向を示唆します。
/// </summary>
public class SupplierCommentController : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("仕入先キャラクターの吹き出しにあるTMPテキストを設定します。")]
    [SerializeField] private TMP_Text speechText;

    [Header("待機時")]
    [TextArea(2, 4)]
    [SerializeField] private string defaultMessage = "今日はどのお花を仕入れる？";

    private static readonly string[] AdditionalDefaultMessages =
    {
        "いらっしゃーい！",
        "今日もいいお花、そろってるよ！"
    };

    private static readonly Dictionary<string, string> FlowerComments = new()
    {
        { "ガーベラ", "明るく元気な印象の花、実は小さな花でできてるんだって！" },
        { "カスミソウ", "霞がかかっている様に見えるから「霞草」っていうんだって！" },
        { "バラ", "定番の花だね！「花の女王」とも呼ばれるんだってさ" },
        { "カーネーション", "この花といえば母の日だよね！色んな色があるんだよ" },
        { "チューリップ", "球根植物といえば！だよね、色によって花言葉の意味が超変わってくるんだ" },
        { "パンジー", "花壇を彩る花だね！すっごい多くの色・模様があるよ" },
        { "スイセン", "スラッとしてる花だね、実は毒を持ってるから食べちゃだめだよ！" },
        { "ヒマワリ", "夏の花といえば！！花言葉は「あなただけを見つめる」、健気だねっ" },
        { "パキラ", "金運を呼ぶ木って言われてるんだって、贈り物にも人気なんだよ" },
        { "ユリ", "大きくて香りも強い花だね、白いユリは「純潔」の象徴なんだって" },
        { "スイートピー", "ひらひらした花びらが蝶みたい！花言葉は「門出」なんだよ" },
        { "アジサイ", "土の性質で花の色が変わるんだって、不思議だよね" },
        { "モンステラ", "穴のあいた大きな葉っぱが目印！南国っぽさ満点だね" },
        { "オジギソウ", "触ると葉っぱを閉じておじぎするよ、ついつい触りたくなるね" },
        { "コスモス", "秋風にゆらゆら揺れる花だね、「秋桜」って書くのも素敵だよね" },
        { "シクラメン", "冬の鉢花の定番だね、花びらが上にくるんっと反ってるんだよ" },
        { "ダリア", "花びらがぎゅっと重なってて豪華！「華麗」って花言葉もぴったりだね" },
        { "レモンスライス", "黄色と白の模様がレモンの輪切りみたい！見てるだけで爽やかだね" },
        { "ポインセチア", "クリスマスの定番だね！赤いところ、実は花じゃなくて葉っぱなんだって" },
        { "桜（枝）", "日本の春といえば桜だよね！枝ごと飾ると一気に春らしくなるよ" },
        { "トロピカルフラワー", "南国らしい派手な色と形が魅力！ひとつあるだけで雰囲気が変わるね" },
        { "ウツボカズラ", "袋の中に虫を落として栄養にするんだって、ちょっと怖くて面白いね" },
        { "花麒麟", "かわいい花と鋭いトゲのギャップがすごい！意外とたくましい植物なんだよ" },
        { "ファイヤーワークスペラルゴニウム", "名前の通り花火みたいに咲くんだよ、ぱっと弾けたような花だね" },
        { "サギソウ", "白鷺が飛んでるみたいでしょ？本当に鳥そっくりな花なんだよ" },
        { "ショクダイオオコンニャク", "世界最大級の花なんだって！でも咲くとものすごい匂いがするらしいよ" },
        { "月下美人", "一晩だけ咲く真っ白な花なんだよ、だからこそ特別に感じるね" },
        { "青バラ", "昔は「不可能」の象徴だったんだって、今の花言葉は「夢かなう」だよ" },
        { "黒バラ", "真っ黒に見えるけど実はすごく濃い赤なんだって、なんとも妖しい花だね" }
    };

    private void Start()
    {
        ShowDefaultMessage();
    }

    public void ShowFlowerComment(FlowerData flower)
    {
        if (speechText == null || flower == null)
            return;

        if (FlowerComments.TryGetValue(flower.flowerName, out string comment))
        {
            speechText.text = comment;
            return;
        }

        speechText.text = $"{flower.flowerName}だね！大事に扱ってあげてね。";
    }

    /// <summary>
    /// 家具商品の「効果説明」ボタンから呼びます。
    /// 一覧側には効果を直接書かず、仕入先キャラクターの吹き出しで説明します。
    /// </summary>
    public void ShowFurnitureEffectComment(FurnitureData furniture)
    {
        if (speechText == null || furniture == null)
            return;

        List<string> effects = new();

        if (furniture.visitorBonusPercent != 0f)
            effects.Add($"来客率が{FormatSignedPercent(furniture.visitorBonusPercent)}");

        if (furniture.budgetBonusPercent != 0f)
            effects.Add($"お客さんの予算が{FormatSignedPercent(furniture.budgetBonusPercent)}");

        if (furniture.summerVisitorBonusPercent != 0f)
            effects.Add($"夏はさらに来客率が{FormatSignedPercent(furniture.summerVisitorBonusPercent)}");

        if (furniture.rainyVisitorBonusPercent != 0f)
            effects.Add($"雨の日はさらに来客率が{FormatSignedPercent(furniture.rainyVisitorBonusPercent)}");

        if (furniture.rainyBudgetBonusPercent != 0f)
            effects.Add($"雨の日はお客さんの予算が{FormatSignedPercent(furniture.rainyBudgetBonusPercent)}");

        if (furniture.rainyVisitorPenaltyFloorPercent < 0f)
            effects.Add($"雨の日の来客率減少ペナルティを{furniture.rainyVisitorPenaltyFloorPercent * 100f:0.#}%まで軽減");

        if (effects.Count == 0)
        {
            speechText.text = $"{furniture.displayName}は特別な効果はないみたい。";
            return;
        }

        speechText.text = $"{furniture.displayName}は、{string.Join("、", effects)}するよ！";
    }

    private static string FormatSignedPercent(float value)
    {
        float percent = value * 100f;
        return percent >= 0f ? $"+{percent:0.#}%" : $"{percent:0.#}%";
    }

    [ContextMenu("待機メッセージを表示")]
    public void ShowDefaultMessage()
    {
        ShowDefaultMessage(null);
    }

    /// <summary>
    /// デイリートレンドがある日は、その内容を示唆する専用セリフを優先します。
    /// 通常日は従来の待機セリフからランダム表示します。
    /// </summary>
    public void ShowDefaultMessage(ShopManager shopManager)
    {
        if (speechText == null)
            return;

        string trendMessage = TrendSystem.GetDailySupplierMessage(shopManager);
        if (!string.IsNullOrWhiteSpace(trendMessage))
        {
            speechText.text = trendMessage;
            return;
        }

        int count = AdditionalDefaultMessages.Length + 1;
        int index = Random.Range(0, count);

        speechText.text = index == 0
            ? defaultMessage
            : AdditionalDefaultMessages[index - 1];
    }
}
