using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RazorMinifier.Core
{
	internal static class Minifier
	{
		private static readonly Regex _emptyLineRegex;
		private static readonly Regex _simpleCommentRegex;
		private static readonly Regex _multiLineCommentRegex;

		static Minifier()
		{
			_emptyLineRegex = new Regex(@"(\n|\t|\s\s)");
			_simpleCommentRegex = new Regex(@"(?<!:)\/\/.*");
			_multiLineCommentRegex = new Regex(@"(<!--.*-->|\/\*.*\*\/)");
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

			input = _simpleCommentRegex.Replace(input, string.Empty);

			input = _multiLineCommentRegex.Replace(input, string.Empty);

			input = _emptyLineRegex.Replace(input, string.Empty);

			foreach (var header in headers)
			{
				input = string.Concat(header, Environment.NewLine, input);
			}

			return input;
		}
	}
}
