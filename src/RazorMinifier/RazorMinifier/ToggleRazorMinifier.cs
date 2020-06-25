using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using DulcisX.Nodes;
using Microsoft.VisualStudio.Shell;
using RazorMinifier.Models.Enums;
using Task = System.Threading.Tasks.Task;

namespace RazorMinifier.VSIX
{
    internal sealed class ToggleRazorMinifier
    {
        public const int CommandId = 0x0100;

        public static readonly Guid CommandSet = new Guid("06dfdf0f-f7a9-4b90-ad45-503948c33a8c");

        private readonly RazorMinifier _package;

        private ToggleRazorMinifier(RazorMinifier package, OleMenuCommandService commandService)
        {
            this._package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID)
            {
                Supported = false
            };

            commandService.AddCommand(menuItem);
        }

        public static ToggleRazorMinifier Instance
        {
            get;
            private set;
        }

        public static async Task InitializeAsync(RazorMinifier package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;

            Instance = new ToggleRazorMinifier(package, commandService);
        }

        private async void Execute(object sender, EventArgs e)
        {
            if (!_package.Solution.IsSolutionFullyLoaded())
            {
                return;
            }

            var selectedNodes = _package.Solution.SelectedNodes.ToList();

            if (_package.Config is null &&
                !await _package.TryLoadConfigFileAsync())
            {
                var result = await _package.InfoBar.NewMessage(false)
                                           .WithErrorImage()
                                           .WithText("The current solution does not contain a ")
                                           .WithText(RazorMinifier.ConfigName, true)
                                           .WithText(" do you want to create one?")
                                           .WithButton<bool>("Yes", true)
                                           .WithButton("No", false)
                                           .Publish()
                                           .WaitForResultAsync();

                if (!result.TryGetResult(out var state) || !state)
                {
                    return;
                }

                _package.CreateConfig();
            }

            var rootPath = _package.Solution.GetFullName();

            foreach (var selectedNode in selectedNodes)
            {
                if (selectedNode is DocumentNode node)
                {
                    var path = node.GetFullName();

                    var relativePath = PathHelper.GetRelativePath(rootPath, path);

                    var minifyType = Path.GetExtension(path) == ".js" ? MinifyType.Js : MinifyType.CSHtml;

                    var file = _package.Config.FindFile(path, relativePath, minifyType);

                    if (file is object)
                    {
                        _package.RemoveFromConfigFile(file);
                    }
                    else
                    {
                        await _package.AddToConfigFile(node, path, relativePath, minifyType);
                    }
                }
            }
        }
    }
}
