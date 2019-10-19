using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RazorMinifier.Core.Models;

namespace RazorMinifier.Core
{
	public class ConfigHandler : IDisposable
	{
		internal Config Config { get; private set; }
		internal bool IsDisposed { get; private set; }

		internal event Func<List<MinifiedRazorFile>, List<MinifiedRazorFile>, Task> ConfigUpdated;

		private readonly FileSystemWatcher _fileSystemWatcher;

		public ConfigHandler(string configPath, string rootDirectory)
		{
			Config = new Config
			{
				ConfigPath = configPath,
				RootDirectory = rootDirectory,
				Files = new HashSet<MinifiedRazorFile>()
			};

			_fileSystemWatcher = Init();
		}

		public ConfigHandler(Config config, string configPath, string rootDirectory)
		{
			Config = config;
			Config.ConfigPath = configPath;
			Config.RootDirectory = rootDirectory;

			_fileSystemWatcher = Init();
		}

		private FileSystemWatcher Init()
		{
			var fileSystemWatcher = new FileSystemWatcher(Config.RootDirectory, Path.GetFileName(Config.ConfigPath))
			{
				EnableRaisingEvents = true,
				NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.CreationTime
			};

			fileSystemWatcher.Changed += ConfigChanged;

			return fileSystemWatcher;
		}

		private async void ConfigChanged(object sender, FileSystemEventArgs e)
		{
			try
			{
				var content = string.Empty;

				using (var sr = new StreamReader(Config.ConfigPath))
				{
					content = await sr.ReadToEndAsync();

					sr.Close();
				}

				var tempConfig = JsonConvert.DeserializeObject<Config>(content);

				if (Config.Files.Count == tempConfig.Files.Count)
					return;

				var toRemove = new List<MinifiedRazorFile>();
				var toAdd = new List<MinifiedRazorFile>();

				foreach (var file in Config.Files)
				{
					if (!tempConfig.Files.Any(x => x == file))
					{
						toRemove.Add(file);
					}
				}

				foreach (var file in tempConfig.Files)
				{
					if (!Config.Files.Any(x => x == file))
					{
						toAdd.Add(file);
					}
				}

				Config.Files = tempConfig.Files;

				_ = ConfigUpdated?.Invoke(toRemove, toAdd);
			}
			catch { }
		}

		public void Dispose()
		{
			if (!IsDisposed)
			{
				_fileSystemWatcher.Changed -= ConfigChanged;
				_fileSystemWatcher.Dispose();
				IsDisposed = true;
			}
		}
	}
}