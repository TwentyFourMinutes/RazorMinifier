using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DulcisX.Core;
using DulcisX.Nodes;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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

        #region Package Members

        public Config Config { get; private set; }

        public const string ConfigName = "rminify.config";

        public RazorMinifier()
        {
            base.OnInitializeAsync += RazorMinifier_OnInitializeAsync;
        }

        private async Task RazorMinifier_OnInitializeAsync(CancellationToken arg1, IProgress<ServiceProgressData> arg2)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            if (Solution.IsTempSolution())
                return;

            await TrySetConfigFileAsync().ConfigureAwait(false);

            await ToggleRazorMinifier.InitializeAsync(this).ConfigureAwait(false);
        }

        public async Task TrySetConfigFileAsync()
        {
            var config = await GetConfigFileAsync().ConfigureAwait(false);

            TrySetConfigFile(config);
        }

        public void TrySetLocalConfigFile(ProjectNode projectNode)
        {
            var config = GetConfigFile(projectNode);

            TrySetConfigFile(config);
        }

        private void TrySetConfigFile(DocumentNode config)
        {
            if (config is object)
            {
                Config = new Config()
                {
                    FullName = config.GetFullName()
                };
            }
        }

        public async Task<DocumentNode> GetConfigFileAsync()
        {
            var file = (await Solution.GetAllChildrenAsync(IsConfig).ConfigureAwait(false)).FirstOrDefault();

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