using System.Collections.Generic;
using System.IO;

namespace RazorMinifier.Models
{
    public class Config
    {
        public string FullName { get; set; }

        public UserSettings UserSettings { get; set; }
    }

    public class UserSettings
    {
        public List<MinifiedFile> Files { get; set; }
    }

    public class MinifiedFile
    {
        public string SourceFile { get; set; }
        public string OutputFile { get; set; }

        public string GetFullSourcePath(string root)
            => GetFullPath(root, SourceFile);

        public string GetFullOutputPath(string root)
            => GetFullPath(root, OutputFile);

        private string GetFullPath(string root, string path)
            => Path.IsPathRooted(path) ? path : Path.Combine(root, path);
    }
}
