using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 仕入先キャラクターの吹き出しテキストを管理します。
/// 花を仕入れた時、その花に対応する短い豆知識・概要を表示します。
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

    /// <summary>
    /// ShowFlowerComment（ショー・フラワー・コメント）
    /// 購入した花に対応する一言を吹き出しへ表示します。
    /// 色違いでも同じ花名なら同じ一言を使用します。
    /// </summary>
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
    /// ゲーム開始時・翌日の仕入れ開始時に、待機セリフをランダムで表示します。
    /// </summary>
    [ContextMenu("待機メッセージを表示")]
    public void ShowDefaultMessage()
    {
        if (speechText == null)
            return;

        int count = AdditionalDefaultMessages.Length + 1;
        int index = Random.Range(0, count);

        speechText.text = index == 0
            ? defaultMessage
            : AdditionalDefaultMessages[index - 1];
    }
}
