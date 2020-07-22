using StringyEnums;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace RazorMinifier.Core.Minifiers
{
    public static class JsMinifier
    {
        static JsMinifier()
        {
            EnumCore.Init(cache => cache.InitWith<Options>(), false);
        }

        [Flags]
        public enum Options
        {
            None = 0,
            [StringRepresentation("--minify-whitespace")]
            RemoveWhitespaces = 1,
            [StringRepresentation("--minify-identifiers")]
            ShortenIdentifiers = 2,
            [StringRepresentation("--minify-syntax")]
            ShortenSyntax = 4,
            All = RemoveWhitespaces | ShortenIdentifiers | ShortenSyntax
        }

        public static async Task<bool> TryMinifyFileAsync(string esbuildPath, string source, string output, Options options = Options.All)
        {
            var arguments = ComposeArguments(source, output, options);

            var proccessStartInfo = new ProcessStartInfo
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                Arguments = arguments,
                CreateNoWindow = true,
                FileName = esbuildPath,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            var process = new Process
            {
                StartInfo = proccessStartInfo
            };

            await process.StartAndWaitForExitAsync(false);

            var processOutputError = await process.StandardError.ReadToEndAsync();

            process.Dispose();

            if (!string.IsNullOrWhiteSpace(processOutputError))
            {
                return false;
            }

            return true;
        }

        private static string ComposeArguments(string source, string output, Options options = Options.All)
        {
            var argumentsBuilder = new StringBuilder();

            argumentsBuilder.Append(source);

            foreach (var stringOption in options.GetFlagRepresentation())
            {
                argumentsBuilder.Append(' ');
                argumentsBuilder.Append(stringOption);
            }

            argumentsBuilder.Append(" --outfile=");

            argumentsBuilder.Append(output);

            return argumentsBuilder.ToString();
        }
    }
}
