using Newtonsoft.Json;
using RazorMinifier.Core.Minifiers;
using RazorMinifier.Models.Enums;
using System.Collections.Generic;
using System.IO;
using System;

namespace RazorMinifier.Models
{
    public class Config
    {
        public string FullName { get; set; }

        public UserSettings UserSettings { get; set; }

        public void RemoveFile(MinifiedFile file)
        {
            switch (file.MinifyType)
            {
                case MinifyType.CSHtml:
                    UserSettings.CSHtmlFiles.Remove(file);
                    break;
                case MinifyType.Js:
                    UserSettings.JsFiles.Remove((JsMinifiedFile)file);
                    break;
                default:
                    throw new InvalidOperationException("Please create an issue on GitHub, if this throws.");
            }
        }

        public MinifiedFile FindFile(string path, string relativePath, MinifyType minifyType)
        {
            if (minifyType == MinifyType.CSHtml)
            {

                return UserSettings.CSHtmlFiles.Find(x => x.SourceFile == relativePath ||
                                                     x.SourceFile == path ||
                                                     x.OutputFile == relativePath ||
                                                     x.OutputFile == path);
            }
            else if (minifyType == MinifyType.Js)
            {

                return UserSettings.JsFiles.Find(x => x.SourceFile == relativePath ||
                                                 x.SourceFile == path ||
                                                 x.OutputFile == relativePath ||
                                                 x.OutputFile == path);
            }
            else
            {
                return null;
            }
        }

        public MinifiedFile FindFileByFullName(string root, string fullName, MinifyType minifyType)
        {
            if (minifyType == MinifyType.CSHtml)
            {

                return UserSettings.CSHtmlFiles.Find(x => x.GetFullSourcePath(root) == fullName);
            }
            else if (minifyType == MinifyType.Js)
            {

                return UserSettings.JsFiles.Find(x => x.GetFullSourcePath(root) == fullName);
            }
            else
            {
                return null;
            }
        }
    }

    public class UserSettings
    {
        public List<MinifiedFile> CSHtmlFiles { get; set; }
        public List<JsMinifiedFile> JsFiles { get; set; }
    }

    public class MinifiedFile
    {
        public string SourceFile { get; set; }
        public string OutputFile { get; set; }

        private MinifyType _minifyType;

        [JsonIgnore]
        public MinifyType MinifyType
        {
            get
            {
                if (_minifyType == 0)
                {
                    _minifyType = Path.GetExtension(SourceFile) == ".js" ? MinifyType.Js : MinifyType.CSHtml;
                }

                return _minifyType;
            }
        }

        public string GetFullSourcePath(string root)
            => GetFullPath(root, SourceFile);

        public string GetFullOutputPath(string root)
            => GetFullPath(root, OutputFile);

        private string GetFullPath(string root, string path)
            => Path.IsPathRooted(path) ? path : Path.Combine(root, path);
    }

    public class JsMinifiedFile : MinifiedFile
    {
        public bool ShortenSyntax { get; set; }
        public bool ShortenIdentifiers { get; set; }
        public bool RemoveWhitespaces { get; set; }

        [JsonIgnore]
        public JsMinifier.Options Options
        {
            get
            {
                var options = (JsMinifier.Options)0;

                if (ShortenSyntax)
                    options |= JsMinifier.Options.ShortenSyntax;

                if (ShortenIdentifiers)
                    options |= JsMinifier.Options.ShortenIdentifiers;

                if (RemoveWhitespaces)
                    options |= JsMinifier.Options.RemoveWhitespaces;

                return options;
            }
        }
    }
}
