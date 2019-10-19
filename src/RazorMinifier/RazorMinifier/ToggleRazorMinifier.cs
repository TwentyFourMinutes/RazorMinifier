using System;
using System.ComponentModel.Design;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using RazorMinifier.Core.Models;
using Task = System.Threading.Tasks.Task;

namespace RazorMinifier.VSIX
{
	internal sealed class ToggleRazorMinifier
	{
		public const int CommandId = 0x0100;

		public static readonly Guid CommandSet = new Guid("06dfdf0f-f7a9-4b90-ad45-503948c33a8c");

		private readonly RazorMinifier package;

		private ToggleRazorMinifier(RazorMinifier package, OleMenuCommandService commandService)
		{
			this.package = package ?? throw new ArgumentNullException(nameof(package));
			commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

			var menuCommandID = new CommandID(CommandSet, CommandId);
			var menuItem = new OleMenuCommand(this.Execute, menuCommandID);

			commandService.AddCommand(menuItem);
		}

		public static ToggleRazorMinifier Instance
		{
			get;
			private set;
		}

		private IAsyncServiceProvider ServiceProvider
		{
			get
			{
				return this.package;
			}
		}

		public static async Task InitializeAsync(RazorMinifier package)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

			OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;

			Instance = new ToggleRazorMinifier(package, commandService);
		}

		private async void Execute(object sender, EventArgs e)
		{
			try
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

				var uih = package.DTE2.ToolWindows.SolutionExplorer;

				var items = (Array)uih.SelectedItems;

				if (items is null || items.Length == 0)
					return;

				foreach (UIHierarchyItem item in items)
				{
					var prjItem = item.Object as ProjectItem;

					var path = prjItem.Properties.Item("FullPath").Value.ToString();

					path = path.Replace(package.Config.RootDirectory, "");

					path = path.TrimStart('\\');

					var file = new MinifiedRazorFile
					{
						OutputPath = path
					};

					var result = await package.FileHandler.AddToConfigFile(file);

					if (!result)
					{
						await package.FileHandler.RemoveFromConfigFile(file);
					}
					else
					{
						var editProjItem = package.DTE2.Solution.FindProjectItem(file.InputPath);

						package.SetProjectItemBuildAction(editProjItem);
					}
				}
			}
			catch { }
		}
	}
}
