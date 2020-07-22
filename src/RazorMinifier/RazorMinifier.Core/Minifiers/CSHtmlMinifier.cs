using PreMailer.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RazorMinifier.Core.Minifiers
{
    public static class CSHtmlMinifier
    {
        private static readonly Regex _emptyLineRegex;
        private static readonly Regex _multiLineCommentRegex;
        private static readonly Regex _razorSectionRegex;
        private static readonly Regex _razorFunctionsRegex;

        static CSHtmlMinifier()
        {
            _emptyLineRegex = new Regex(@"(^(\s)+|(\s)*(\v|\n|\r))", RegexOptions.Multiline);
            _multiLineCommentRegex = new Regex(@"(<!--(.|\n)*?-->|\/\*(.|\n)*?\*\/|@\*(.|\n)*?\*@)");
            _razorSectionRegex = new Regex(@"@section\s\w+\s?{");
            _razorFunctionsRegex = new Regex(@"@functions\s\w+\s?{");
        }

        public static Task<MinifyResult> MinifyFileAsync(string source, string output, bool usePreMailer)
        {
            return Task.Run(() =>
            {
                var content = File.ReadAllText(source);

                var result = Minify(content, usePreMailer);

                File.WriteAllText(output, result.Item1);

                return result.Item2;
            });
        }

        private static (string, MinifyResult) Minify(string input, bool usePreMailer)
        {
            var headers = new List<string>();

            while (true)
            {
                if (input.StartsWith("@"))
                {
                    var index = input.IndexOf(Environment.NewLine);

                    if (index == -1)
                        break;

                    var lastChar = input[index - 1];

                    if (lastChar == '{' || lastChar == '}')
                        break;

                    var content = input.Substring(0, index);
                    input = input.Remove(0, index + 2);

                    headers.Add(content);
                }
                else
                {
                    break;
                }
            }

            InlineResult inlineResult = null;

            if (usePreMailer)
            {
                inlineResult = PreMailer.Net.PreMailer.MoveCssInline(input, stripIdAndClassAttributes: true);
                input = inlineResult.Html;
            }

            input = _multiLineCommentRegex.Replace(input, string.Empty);

            input = _emptyLineRegex.Replace(input, string.Empty);

            foreach (Match match in _razorSectionRegex.Matches(input))
            {
                input = input.Insert(match.Index, Environment.NewLine);
            }

            foreach (Match match in _razorFunctionsRegex.Matches(input))
            {
                input = input.Insert(match.Index, Environment.NewLine);
            }

            foreach (var header in headers)
            {
                input = string.Concat(header, Environment.NewLine, input);
            }

            return (input, new MinifyResult { Success = inlineResult is null || (inlineResult != null && inlineResult.Warnings.Count == 0), Message = inlineResult is null ? null : string.Join(", ", inlineResult.Warnings) });
        }
    }
}
