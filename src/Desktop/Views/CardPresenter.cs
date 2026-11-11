using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TokenPet.Core;

namespace TokenPet.Desktop.Views
{
    internal static class CardPresenter
    {
        internal static void Paint(Border card, Summary summary)
        {
            var t = summary.Tokens;
            var latest = summary.Latest;
            var culture = CultureInfo.InvariantCulture;
            ((TextBlock)card.FindName("Total")).Text = t.Total == 0 && summary.Unavailable ? "—" : t.Total.ToString("N0", culture);
            string cost = "$" + summary.Dollars.ToString("0.00", culture) + "  ·  " +
                summary.Credits.ToString("0.00", culture) + " credits";
            if (summary.Unpriced > 0) cost = summary.Unpriced == t.Total ? "暂无可用费率" : cost + " *";
            if (t.Total == 0) cost = summary.Unavailable ? "暂时无法读取本机用量" : "今日暂无本机用量";
            var costText = (TextBlock)card.FindName("Cost");
            costText.Text = cost;
            costText.ToolTip = "美元按公开 API 费率估算，credits 按额度费率估算";
            var weeklyText = (TextBlock)card.FindName("Weekly");
            weeklyText.Text = summary.Weekly == null ? "周剩余 —" :
                "周剩余 " + summary.Weekly.RemainingPercent.ToString("0.#", culture) + "%";
            weeklyText.ToolTip = summary.Weekly == null ? "等待 Codex 写入有效的每周额度记录" :
                string.Format(culture, "额度重置 {0:MM-dd HH:mm} · 记录更新 {1:MM-dd HH:mm}",
                    summary.Weekly.ResetsAt.LocalDateTime, summary.Weekly.ObservedAt.LocalDateTime);
            double cached = t.Input == 0 ? 0 : 100.0 * t.Cached / t.Input;
            ((TextBlock)card.FindName("Input")).Text = t.Input.ToString("N0", culture);
            ((TextBlock)card.FindName("Output")).Text = t.Output.ToString("N0", culture);
            ((TextBlock)card.FindName("Cached")).Text = t.Cached.ToString("N0", culture);
            ((TextBlock)card.FindName("CacheRate")).Text = cached.ToString("0.#", culture) + "%";
            ((ProgressBar)card.FindName("CacheBar")).Value = cached;
            ((TextBlock)card.FindName("RecentTime")).Text = latest == null ? "等待用量写入" :
                latest.Time.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", culture);
            ((TextBlock)card.FindName("RecentTokens")).Text = latest == null ? "—" :
                latest.Tokens.Total.ToString("N0", culture) + " tok";
            ((TextBlock)card.FindName("RecentModel")).Text = latest == null ? "" :
                latest.Model.Length > 0 ? latest.Model : "模型未知";

            var note = (TextBlock)card.FindName("Note");
            string unpriced = summary.Unpriced > 0 ? "缺少费率的用量 " + summary.Unpriced.ToString("N0", culture) + " tok" : "";
            string unavailable = summary.Unavailable ? "部分日志暂时无法读取" : "";
            note.Text = string.Join("\n", new[] { unpriced, unavailable }.Where(text => text.Length > 0));
            note.Visibility = note.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

    }
}
