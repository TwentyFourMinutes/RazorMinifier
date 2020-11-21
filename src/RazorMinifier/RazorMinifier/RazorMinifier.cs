using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DulcisX.Core;
using DulcisX.Core.Enums;
using DulcisX.Core.Enums.VisualStudio;
using DulcisX.Nodes;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using RazorMinifier.Core;
using RazorMinifier.Core.Minifiers;
using RazorMinifier.Models;
using RazorMinifier.Models.Enums;
using Task = System.Threading.Tasks.Task;

namespace RazorMinifier.VSIX
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(RazorMinifier.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideUIContextRule(UiContextSupportedFiles,
    name: "Supported Files",
    expression: "SolutionExists & (SingleProject | MultipleProjects) & DotCSharpHtml",
    termNames: new[] { "SolutionExists", "SingleProject", "MultipleProjects", "DotCSharpHtml" },
    termValues: new[] { VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, VSConstants.UICONTEXT.SolutionHasSingleProject_string, VSConstants.UICONTEXT.SolutionHasMultipleProjects_string, @"HierSingleSelectionName:((?<!\.edit)\.cshtml)|((?<!\.min)\.js)$" })]
    public sealed class RazorMinifier : PackageX
    {
        public const string UiContextSupportedFiles = "24551deb-f034-43e9-a279-0e541241687e";
        public const string PackageGuidString = "f4ac4e92-8fc5-47de-80b0-2d35594bc824";

        public Config Config { get; private set; }

        public const string ConfigName = "rminify.json";

        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);

        private volatile bool _ignoreAddedFile;

        public RazorMinifier()
        {
            base.OnInitializeAsync += RazorMinifier_OnInitializeAsync;
            base.OnDisposing += OnPackageDisposing;
        }

        private async Task RazorMinifier_OnInitializeAsync(CancellationToken arg1, IProgress<ServiceProgressData> arg2)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            if (Solution.IsTempSolution())
            {
                return;
            }

            await ToggleRazorMinifier.InitializeAsync(this);

            Solution.OpenNodeEvents.OnSaved.Hook(NodeTypes.Document, OnDocumentSaved);
            Solution.ProjectNodeChangedEvents.OnDocumentsAdded += OnDocumentsAdded;
            Solution.ProjectNodeChangedEvents.OnDocumentsRemoved += OnDocumentsRemoved;

            _ = JoinableTaskFactory.RunAsync(async () => await TryLoadConfigFileAsync());
        }

        private void OnDocumentsAdded(IEnumerable<AddedPhysicalNode<DocumentNode, VSADDFILEFLAGS>> addedDocuments)
        {
            if (Config is object || _ignoreAddedFile)
            {
                return;
            }

            var cfg = addedDocuments.FirstOrDefault(x => x.Node.GetFileName().ToLower() == ConfigName);

            if (cfg is object)
            {
                TryLoadConfigFile(cfg.Node, false);
            }
        }

        private void OnDocumentsRemoved(IEnumerable<RemovedPhysicalNode<PhysicalNodeRemovedFlags>> removedDocuments)
        {
            if (Config is object && removedDocuments.Any(x => x.FullName == Config.FullName))
            {
                Config = null;
            }
        }

        public void OnDocumentSaved(IPhysicalNode node)
        {
            if (Config is null)
                return;

            var root = Path.GetDirectoryName(Solution.GetFullName());

            var fullName = node.GetFullName();
            var fileName = node.GetFileName();

            if (fileName == ConfigName)
            {
                TryUpdateConfigFile(fullName);

                return;
            }

            var minifiedFile = Config.FindFileByFullName(root, fullName, Path.GetExtension(fileName) == ".js" ? MinifyType.Js : MinifyType.CSHtml);

            if (minifiedFile is null)
            {
                return;
            }

            _ = Task.Run(() => MinifyFileAsync(minifiedFile));
        }

        public async Task AddToConfigFileAsync(DocumentNode document, string fullName, string relativePath, MinifyType minifyType)
        {
            if (Config is null)
                return;

            MinifiedFile minifiedFile;

            switch (minifyType)
            {
                case MinifyType.Js:
                    minifiedFile = AddJsToConfigFile(relativePath);
                    Config.UserSettings.JsFiles.Add((JsMinifiedFile)minifiedFile);
                    break;
                case MinifyType.CSHtml:
                    minifiedFile = await ThreadHelper.JoinableTaskFactory.RunAsync(() =>
                    {
                        var file = AddCSHtmlToConfigFileAsync(document, fullName, relativePath);

                        return Task.FromResult(file);
                    });

                    Config.UserSettings.CSHtmlFiles.Add((CsHtmlMinifiedFile)minifiedFile);
                    break;
                default:
                    throw new InvalidOperationException("Please create an issue on GitHub, if this throws.");
            }

            await MinifyFileAsync(minifiedFile);

            SetConfigFile(Config.FullName);
        }

        private JsMinifiedFile AddJsToConfigFile(string relativePath)
        {
            return new JsMinifiedFile
            {
                OutputFile = Path.ChangeExtension(relativePath, "min.js"),
                SourceFile = relativePath,
                RemoveWhitespaces = true,
                ShortenIdentifiers = true,
                ShortenSyntax = true
            };

        }

        private MinifiedFile AddCSHtmlToConfigFileAsync(DocumentNode document, string fullName, string relativePath)
        {
            if (!File.Exists(Path.ChangeExtension(fullName, "edit.cshtml")))
            {
                document = document.ChangeExtension("edit.cshtml");

                var parent = document.GetParent();

                if (parent is ProjectNode project)
                {
                    project.CreateDocument(fullName);
                }
                else
                {
                    var folderNode = (FolderNode)parent;

                    project = document.GetParentProject();

                    project.CreateDocument(folderNode, fullName);
                }
            }

            var newPath = Path.ChangeExtension(relativePath, "edit.cshtml");

            return new CsHtmlMinifiedFile
            {
                OutputFile = relativePath,
                UsePreMailer = false,
                SourceFile = newPath
            };
        }

        public void RemoveFromConfigFile(MinifiedFile file)
        {
            Config.RemoveFile(file);

            SetConfigFile(Config.FullName);
        }

        public async Task MinifyFileAsync(MinifiedFile minifiedFile)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var root = Path.GetDirectoryName(Solution.GetFullName());

            var destinationFile = minifiedFile.GetFullOutputPath(root);

            if (!File.Exists(destinationFile))
            {
                InfoBar.NewMessage()
                       .WithErrorImage()
                       .WithText("The path ")
                       .WithText(minifiedFile.OutputFile, underline: true)
                       .WithText(" is not valid.")
                       .Publish();

                return;
            }

            MinifyResult result = null;

            switch (minifiedFile.MinifyType)
            {
                case MinifyType.CSHtml:
                    result = await CSHtmlMinifier.MinifyFileAsync(minifiedFile.GetFullSourcePath(root), destinationFile, ((CsHtmlMinifiedFile)minifiedFile).UsePreMailer);
                    break;
                case MinifyType.Js:
                    if (!await JsMinifier.TryMinifyFileAsync("esbuild.exe", minifiedFile.GetFullSourcePath(root), destinationFile, ((JsMinifiedFile)minifiedFile).Options))
                    {
                        InfoBar.NewMessage()
                               .WithErrorImage()
                               .WithText("The file ")
                               .WithText(minifiedFile.SourceFile, underline: true)
                               .WithText(" contains invalid js.")
                               .Publish();
                    }
                    break;
                default:
                    throw new InvalidOperationException("Please create an issue on GitHub, if this throws.");
            }

            if (result is object && !result.Success)
            {
                InfoBar.NewMessage()
                       .WithErrorImage()
                       .WithText("The file ")
                       .WithText(minifiedFile.SourceFile, underline: true)
                       .WithText(" produced warnings. ")
                       .WithText(result.Message)
                       .Publish();
            }
        }

        public void CreateConfig()
        {
            var (project, _) = Solution.GetStartupProjects().FirstOrDefault();

            if (project is null)
            {
                project = Solution.GetAllProjects().FirstOrDefault();

                if (project is null)
                {
                    InfoBar.NewMessage()
                           .WithErrorImage()
                           .WithText("There is no project present, in which the config file should be generated in.")
                           .WithButton("Ok")
                           .Publish();

                    return;
                }
            }

            _ignoreAddedFile = true;

            var success = project.CreateDocument(RazorMinifier.ConfigName);

            _ignoreAddedFile = false;

            if (success != VSADDRESULT.ADDRESULT_Success)
                return;

            var document = GetConfigFile(project);

            var fullName = document.GetFullName();

            SetConfigFile(fullName);

            TryLoadLocalConfigFile(project);
        }

        public void SetConfigFile(string fullName)
        {
            if (!File.Exists(fullName))
                return;

            UserSettings config = Config?.UserSettings ?? new UserSettings { CSHtmlFiles = new List<CsHtmlMinifiedFile>(), JsFiles = new List<JsMinifiedFile>() };

            var content = JsonConvert.SerializeObject(config, Formatting.Indented);

            File.WriteAllText(fullName, content);
        }

        #region LoadConfig

        public async Task<bool> TryLoadConfigFileAsync()
        {
            await _semaphoreSlim.WaitAsync();

            try
            {
                if (Config is object)
                    return true;

                var config = await GetConfigFileAsync();

                if (config is null)
                    return false;

                return TryLoadConfigFile(config);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public bool TryLoadLocalConfigFile(ProjectNode projectNode)
        {
            var config = GetConfigFile(projectNode);

            config.SetCopyToOutputDirectory(CopyToOutputDirectory.Never);

            return TryLoadConfigFile(config);
        }

        public bool TryLoadConfigFile(DocumentNode config, bool showMessageOnError = true)
        {
            if (config is null)
            {
                return false;
            }

            var fullName = config.GetFullName();

            Config = new Config()
            {
                FullName = fullName
            };

            return TryUpdateConfigFile(fullName, showMessageOnError);
        }

        public bool TryUpdateConfigFile(string fullName, bool showMessageOnError = true)
        {
            var root = Path.GetDirectoryName(Solution.GetFullName());

            UserSettings settings = null;

            try
            {
                var content = File.ReadAllText(fullName);

                // Updates older config files.
                content = content.Replace("\"Files\"", "\"CSHtmlFiles\"");

                settings = JsonConvert.DeserializeObject<UserSettings>(content);

                settings.CSHtmlFiles = settings.CSHtmlFiles ?? new List<CsHtmlMinifiedFile>();
                settings.JsFiles = settings.JsFiles ?? new List<JsMinifiedFile>();

                foreach (var file in settings.CSHtmlFiles.Cast<MinifiedFile>().Union(settings.JsFiles))
                {
                    var isSourceValid = File.Exists(file.GetFullSourcePath(root));
                    var isOutputValid = File.Exists(file.GetFullOutputPath(root));

                    if (!isSourceValid || !isOutputValid)
                    {
                        InfoBar.NewMessage()
                           .WithErrorImage()
                           .WithText("The path ")
                           .WithText(isSourceValid ? file.OutputFile : file.SourceFile, underline: true)
                           .WithText(" is not valid.")
                           .Publish();

                        return false;
                    }
                }
            }
            catch
            {
                if (showMessageOnError)
                {
                    InfoBar.NewMessage()
                           .WithErrorImage()
                           .WithText("The content of your ")
                           .WithText(RazorMinifier.ConfigName, true)
                           .WithText(" contains invalid json.")
                           .Publish();
                }

                return false;
            }

            Config.UserSettings = settings;

            return true;
        }

        public async Task<DocumentNode> GetConfigFileAsync()
        {
            var filteredItems = await Solution.GetAllChildrenAsync(IsConfig);

            var file = filteredItems.FirstOrDefault();

            return file as DocumentNode;
        }

        public DocumentNode GetConfigFile(ProjectNode projectNode)
        {
            var file = projectNode.GetChildren().FirstOrDefault(IsConfig);

            return file as DocumentNode;
        }

        private bool IsConfig(BaseNode baseNode)
        {
            if (!(baseNode is DocumentNode document))
                return false;

            var fileName = Path.GetFileName(document.GetFullName()).ToLower();

            return fileName == ConfigName;
        }

        #endregion

        private void OnPackageDisposing()
        {
            base.OnInitializeAsync -= RazorMinifier_OnInitializeAsync;
            base.OnDisposing -= OnPackageDisposing;

            Solution.OpenNodeEvents.OnSaved.UnHook(NodeTypes.Document, OnDocumentSaved);
            Solution.ProjectNodeChangedEvents.OnDocumentsAdded -= OnDocumentsAdded;
            Solution.ProjectNodeChangedEvents.OnDocumentsRemoved -= OnDocumentsRemoved;

            _semaphoreSlim.Dispose();
        }
    }
}