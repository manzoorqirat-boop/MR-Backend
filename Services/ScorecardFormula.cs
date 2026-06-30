using System.Globalization;
using System.Text.RegularExpressions;

namespace SiteReportApp.Services
{
    // Tiny, safe arithmetic evaluator for the scorecard's computed columns.
    //
    // Formulas look like "={a}/{b}", "=({a}-{b})/{a}", "=SUM({a}:{b})", "={a}*{b}*{c}".
    // It supports + - * / parentheses and the single function SUM(x:y) (treated as x+y,
    // matching how the template uses it). Division by zero returns null (the UI shows "-"),
    // which mirrors the template's #DIV/0! cells being meaningless rather than an error.
    public static class ScorecardFormula
    {
        private static readonly Regex RefToken = new(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);
        private static readonly Regex SumCall = new(@"SUM\(([^)]*)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Evaluate a computed column. `values` maps column key -> numeric value (already
        // resolved, including previously-computed columns). Returns null on div-by-zero
        // or any non-numeric input.
        public static double? Evaluate(string formula, IReadOnlyDictionary<string, double?> values)
        {
            if (string.IsNullOrWhiteSpace(formula)) return null;
            var expr = formula.TrimStart('=').Trim();

            // Expand SUM(a:b) -> (a+b). The template only ever sums a contiguous pair.
            expr = SumCall.Replace(expr, mt =>
            {
                var inner = mt.Groups[1].Value;
                var parts = inner.Split(':', '+', ',');
                return "(" + string.Join("+", parts.Select(p => p.Trim())) + ")";
            });

            // Substitute {key} with the numeric value, or bail out if any ref is missing/non-numeric.
            bool missing = false;
            expr = RefToken.Replace(expr, mt =>
            {
                var key = mt.Groups[1].Value;
                if (values.TryGetValue(key, out var v) && v.HasValue)
                    return v.Value.ToString("R", CultureInfo.InvariantCulture);
                missing = true;
                return "0";
            });
            if (missing) return null;

            try
            {
                var result = new ExprParser(expr).Parse();
                if (double.IsNaN(result) || double.IsInfinity(result)) return null;
                return result;
            }
            catch
            {
                return null;
            }
        }

        // Given a metric's columns and the raw input cell values, returns a dictionary
        // of every column key -> value, with computed columns resolved in declaration order
        // (computed columns may reference earlier computed columns, e.g. totalOosClosed).
        public static Dictionary<string, double?> ComputeRow(ScMetric metric, IReadOnlyDictionary<string, string?> rawCells)
        {
            var nums = new Dictionary<string, double?>();
            foreach (var col in metric.Columns)
            {
                if (col.Type == ScColType.Number)
                {
                    if (rawCells.TryGetValue(col.Key, out var raw) &&
                        double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                        nums[col.Key] = n;
                    else
                        nums[col.Key] = null;
                }
            }
            foreach (var col in metric.Columns)
            {
                if (col.Type == ScColType.Computed && !string.IsNullOrWhiteSpace(col.Formula))
                    nums[col.Key] = Evaluate(col.Formula!, nums);
            }
            return nums;
        }

        // ---- Recursive-descent parser: + - * / and parentheses, division-by-zero throws ----
        private class ExprParser
        {
            private readonly string _s;
            private int _i;
            public ExprParser(string s) { _s = s; }

            public double Parse()
            {
                var v = ParseAddSub();
                SkipWs();
                if (_i < _s.Length) throw new FormatException("Unexpected trailing input");
                return v;
            }

            private double ParseAddSub()
            {
                var v = ParseMulDiv();
                while (true)
                {
                    SkipWs();
                    if (Match('+')) v += ParseMulDiv();
                    else if (Match('-')) v -= ParseMulDiv();
                    else return v;
                }
            }

            private double ParseMulDiv()
            {
                var v = ParseUnary();
                while (true)
                {
                    SkipWs();
                    if (Match('*')) v *= ParseUnary();
                    else if (Match('/'))
                    {
                        var d = ParseUnary();
                        if (d == 0) throw new DivideByZeroException();
                        v /= d;
                    }
                    else return v;
                }
            }

            private double ParseUnary()
            {
                SkipWs();
                if (Match('-')) return -ParseUnary();
                if (Match('+')) return ParseUnary();
                return ParsePrimary();
            }

            private double ParsePrimary()
            {
                SkipWs();
                if (Match('('))
                {
                    var v = ParseAddSub();
                    SkipWs();
                    if (!Match(')')) throw new FormatException("Missing )");
                    return v;
                }
                int start = _i;
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.' || _s[_i] == 'E' || _s[_i] == 'e' ||
                       ((_s[_i] == '+' || _s[_i] == '-') && _i > start && (_s[_i - 1] == 'E' || _s[_i - 1] == 'e'))))
                    _i++;
                var token = _s.Substring(start, _i - start);
                if (!double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
                    throw new FormatException($"Bad number '{token}'");
                return num;
            }

            private void SkipWs() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }
            private bool Match(char c)
            {
                SkipWs();
                if (_i < _s.Length && _s[_i] == c) { _i++; return true; }
                return false;
            }
        }
    }
}
