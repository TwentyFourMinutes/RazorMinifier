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
using RazorMinifier.Models;
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
    termValues: new[] { VSConstants.UICONTEXT.SolutionExists_string, VSConstants.UICONTEXT.SolutionHasSingleProject_string, VSConstants.UICONTEXT.SolutionHasMultipleProjects_string, @"HierSingleSelectionName:(?<!\.edit)\.cshtml$" })]
    public sealed class RazorMinifier : PackageX
    {
        public const string UiContextSupportedFiles = "24551deb-f034-43e9-a279-0e541241687e";
        public const string PackageGuidString = "f4ac4e92-8fc5-47de-80b0-2d35594bc824";

        public Config Config { get; private set; }

        public const string ConfigName = "rminify.json";

        public RazorMinifier()
        {
            base.OnInitializeAsync += RazorMinifier_OnInitializeAsync;
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

            if (!Solution.IsSolutionFullyLoaded())
            {
                return;
            }

            await TryLoadConfigFileAsync();
        }

        private void OnDocumentsAdded(IEnumerable<AddedPhysicalNode<DocumentNode, VSADDFILEFLAGS>> addedDocuments)
        {
            if (Config is object)
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

            var minifiedFile = Config.UserSettings.Files.Find(x => x.GetFullSourcePath(root) == fullName);

            if (minifiedFile is null)
            {
                if (fileName == ConfigName)
                {
                    TryUpdateConfigFile(fullName);
                }

                return;
            }

            MinifyFile(minifiedFile);
        }

        public void AddToConfigFile(DocumentNode document, string fullName, string relativePath)
        {
            if (Config is null)
                return;

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

            var minifiedFile = new MinifiedFile
            {
                OutputFile = relativePath,
                SourceFile = newPath
            };

            Config.UserSettings.Files.Add(minifiedFile);

            MinifyFile(minifiedFile);

            SetConfigFile(Config.FullName);
        }

        public void RemoveFromConfigFile(MinifiedFile file)
        {
            Config.UserSettings.Files.Remove(file);

            SetConfigFile(Config.FullName);
        }

        public void MinifyFile(MinifiedFile minifiedFile)
        {
            var root = Path.GetDirectoryName(Solution.GetFullName());

            var content = File.ReadAllText(minifiedFile.GetFullSourcePath(root));

            content = Minifier.Minify(content);

            File.WriteAllText(minifiedFile.GetFullOutputPath(root), content);
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

            var success = project.CreateDocument(RazorMinifier.ConfigName);

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

            UserSettings config = Config?.UserSettings ?? new UserSettings { Files = new List<MinifiedFile>() };

            var content = JsonConvert.SerializeObject(config, Formatting.Indented);

            File.WriteAllText(fullName, content);
        }

        #region LoadConfig

        public async Task<bool> TryLoadConfigFileAsync()
        {
            var config = await GetConfigFileAsync();

            if (config is null)
                return false;

            return TryLoadConfigFile(config);
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
                settings = JsonConvert.DeserializeObject<UserSettings>(File.ReadAllText(fullName));

                foreach (var file in settings.Files)
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
    }
}