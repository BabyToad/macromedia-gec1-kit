using System.Collections.Generic;
using System.Text;

namespace Gec1.SetupCheck
{
    internal enum CheckStatus { Ok, Info, Warn, Fail }

    internal sealed class CheckResult
    {
        public CheckStatus Status;
        public string Title;
        public string Detail;
        /// <summary>What the student should do. Empty for passing checks.</summary>
        public string Fix;

        public CheckResult(CheckStatus status, string title, string detail, string fix = "")
        {
            Status = status;
            Title = title;
            Detail = detail;
            Fix = fix ?? "";
        }

        public static string Label(CheckStatus status)
        {
            switch (status)
            {
                case CheckStatus.Ok: return "OK";
                case CheckStatus.Info: return "INFO";
                case CheckStatus.Warn: return "WARNUNG";
                default: return "FEHLER";
            }
        }

        /// <summary>Plain-text report, meant to be pasted into a chat with the AI agent or the lecturer.</summary>
        public static string Report(IList<CheckResult> results, string header)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header);
            sb.AppendLine(Summary(results));
            sb.AppendLine();
            foreach (var r in results)
            {
                sb.Append('[').Append(Label(r.Status)).Append("] ").AppendLine(r.Title);
                if (!string.IsNullOrEmpty(r.Detail)) sb.Append("    ").AppendLine(r.Detail.Replace("\n", "\n    "));
                if (!string.IsNullOrEmpty(r.Fix)) sb.Append("    -> ").AppendLine(r.Fix.Replace("\n", "\n       "));
            }
            return sb.ToString();
        }

        public static string Summary(IList<CheckResult> results)
        {
            int fail = 0, warn = 0;
            foreach (var r in results)
            {
                if (r.Status == CheckStatus.Fail) fail++;
                else if (r.Status == CheckStatus.Warn) warn++;
            }
            if (fail == 0 && warn == 0) return "Alles bereit. Das Setup passt.";
            if (fail == 0) return "Funktioniert, aber " + warn + (warn == 1 ? " Warnung" : " Warnungen") + " – bitte kurz lesen.";
            return fail + (fail == 1 ? " Fehler" : " Fehler") + (warn > 0 ? ", " + warn + (warn == 1 ? " Warnung" : " Warnungen") : "")
                   + ". Die Fehler von oben nach unten beheben, dann erneut prüfen.";
        }
    }
}
