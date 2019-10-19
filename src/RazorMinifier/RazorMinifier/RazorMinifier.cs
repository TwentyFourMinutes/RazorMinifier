using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using RazorMinifier.Core;
using RazorMinifier.Core.Models;
using VSLangProj;
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
	expression: "DotCSharpHtml",
	termNames: new[] { "DotCSharpHtml" },
	termValues: new[] { "HierSingleSelectionName:(?<!.edit).cshtml$" })]
	public sealed class RazorMinifier : AsyncPackage
	{
		public const string UiContextSupportedFiles = "24551deb-f034-43e9-a279-0e541241687e";
		public const string PackageGuidString = "f4ac4e92-8fc5-47de-80b0-2d35594bc824";
		public const string ConfigName = "Rminify.json";
		public const string BuildAction = "BuildAction";

		public RazorMinifier()
		{

		}

		#region Package Members

		private DTE2 _dte2;

		public DTE2 DTE2
		{
			get
			{
				_dte2 = _dte2 ?? GetGlobalService(typeof(DTE)) as DTE2;
				return _dte2;
			}
		}

		public Config Config;
		public FileHandler FileHandler;

		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			var project = GetStartupProject();

			if (project != null)
			{
				var items = project.ProjectItems.Cast<ProjectItem>();
				var minifyConfigFile = items.FirstOrDefault(x =>
				{
					ThreadHelper.ThrowIfNotOnUIThread();
					return x.Name == ConfigName;
				});

				if (minifyConfigFile is null)
				{
					var configPath = Path.Combine(Path.GetDirectoryName(project.FullName), ConfigName);

					var configHandler = new ConfigHandler(configPath, Path.GetDirectoryName(project.FullName));

					FileHandler = new FileHandler(configHandler);
				}
				else
				{

					SetProjectItemBuildAction(minifyConfigFile);

					var configPath = Path.Combine(Path.GetDirectoryName(project.FullName), minifyConfigFile.Name);

					if (!File.Exists(configPath))
						return;

					var configString = string.Empty;

					using (var reader = new StreamReader(configPath))
					{
						configString = await reader.ReadToEndAsync();
					}

					Config = JsonConvert.DeserializeObject<Config>(configString);

					var configHandler = new ConfigHandler(Config, configPath, Path.GetDirectoryName(project.FullName));

					FileHandler = new FileHandler(configHandler);

					EnsureFileHandlerBuildActions();
				}
			}

			await AddToRazorMinifier.InitializeAsync(this);
		}

		public Project GetStartupProject()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var startupProjects = ((Array)DTE2.Solution.SolutionBuild.StartupProjects).Cast<string>();

			var startupProjectPath = startupProjects.FirstOrDefault();

			if (string.IsNullOrWhiteSpace(startupProjectPath))
				return null;

			var projects = DTE2.Solution.Projects.Cast<Project>();

			return projects.FirstOrDefault(x =>
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				return x.UniqueName == startupProjectPath;
			});
		}

		public void EnsureFileHandlerBuildActions()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			foreach (var editFile in FileHandler.GetAllEditFiles())
			{
				var editProjectItem = DTE2.Solution.FindProjectItem(editFile);

				SetProjectItemBuildAction(editProjectItem);
			}
		}

		public void SetProjectItemBuildAction(ProjectItem projectItem, prjBuildAction buildAction = prjBuildAction.prjBuildActionNone)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var props = projectItem.Properties.Cast<Property>();

			var prop = props.FirstOrDefault(x =>
			{
				ThreadHelper.ThrowIfNotOnUIThread();

				var match = false;
				try
				{
					match = x.Name == BuildAction;
				}
				catch { }

				return match;
			});

			if ((prjBuildAction)prop.Value != buildAction)
			{
				prop.Value = buildAction;
			}

		}

		protected override void Dispose(bool disposing)
		{
			FileHandler.Dispose();
			base.Dispose(disposing);
		}

		#endregion
	}
}
