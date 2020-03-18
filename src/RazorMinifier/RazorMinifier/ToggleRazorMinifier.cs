using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using DulcisX.Core.Enums;
using DulcisX.Nodes;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using RazorMinifier.Core;
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

            package.Solution.OpenNodeEvents.OnSaved.Hook(NodeTypes.Document, OnDocumentSaved);
        }

        public static void OnDocumentSaved(IPhysicalNode node)
        {
            var path = node.GetFullName();

            _ = Task.Run(() =>
            {
                var content = File.ReadAllText(path);

                File.WriteAllText(path, Minifier.Minify(content));
            });
        }

        private async void Execute(object sender, EventArgs e)
        {
            if (_package.Config is null)
            {
                await _package.TrySetConfigFileAsync().ConfigureAwait(false);

                var result = await _package.InfoBar.NewMessage(false)
                                            .WithErrorImage()
                                            .WithText("The current solution does not contain a ")
                                            .WithText("RMinify.config", true)
                                            .WithText(" do you want to create one?")
                                            .WithButton<bool>("Yes", true)
                                            .WithButton("No", false)
                                            .Publish()
                                            .WaitForResultAsync().ConfigureAwait(false);

                if (!result.TryGetResult(out var state) || !state)
                {
                    return;
                }

                CreateConfig();
            }


        }

        private void CreateConfig()
        {
            var (project, _) = _package.Solution.GetStartupProjects().FirstOrDefault();

            if (project is null)
            {
                project = _package.Solution.GetAllProjects().FirstOrDefault();

                if (project is null)
                {
                    _package.InfoBar.NewMessage()
                                    .WithErrorImage()
                                    .WithText("There is no project present, in which the config file should be generated in.")
                                    .WithButton("Ok")
                                    .WithButton("Try again", CreateConfig)
                                    .Publish();

                    return;
                }
            }

            var success = project.AddDocument(RazorMinifier.ConfigName);

            if (success != VSADDRESULT.ADDRESULT_Success)
                return;

            _package.TrySetLocalConfigFile(project);
        }
    }
}
