using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

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

        public static void MinifyFile(string source, string output)
        {
            var content = File.ReadAllText(source);

            content = Minify(content);

            File.WriteAllText(output, content);
        }

        public static string Minify(string input)
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

            return input;
        }
    }
}
