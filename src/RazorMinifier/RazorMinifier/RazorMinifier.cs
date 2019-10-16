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
	public sealed class RazorMinifier : AsyncPackage
	{
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

			var startupProjects = ((Array)DTE2.Solution.SolutionBuild.StartupProjects).Cast<string>();
			var startupProject = startupProjects.FirstOrDefault();

			if (startupProject != null)
			{
				var projects = DTE2.Solution.Projects.Cast<Project>();
				var project = projects.FirstOrDefault(x =>
				{
					ThreadHelper.ThrowIfNotOnUIThread();
					return x.UniqueName == startupProject;
				});

				if (project != null)
				{
					var items = project.ProjectItems.Cast<ProjectItem>();
					var minifyFile = items.FirstOrDefault(x =>
					{
						ThreadHelper.ThrowIfNotOnUIThread();
						return x.Name == ConfigName;
					});

					SetProjectItemBuildAction(minifyFile);

					var configPath = Path.Combine(Path.GetDirectoryName(project.FullName), minifyFile.Name);

					if (!File.Exists(configPath))
						return;

					var configString = string.Empty;

					using (var reader = new StreamReader(configPath))
					{
						configString = await reader.ReadToEndAsync();
					}

					Config = JsonConvert.DeserializeObject<Config>(configString);
					FileHandler = new FileHandler(Config, project.FullName);
					
					foreach (var editFile in FileHandler.GetAllEditFiles())
					{
						var editProjectItem = DTE2.Solution.FindProjectItem(editFile);

						SetProjectItemBuildAction(editProjectItem);
					}
				}
			}
		}

		private void SetProjectItemBuildAction(ProjectItem projectItem, prjBuildAction buildAction = prjBuildAction.prjBuildActionNone)
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
