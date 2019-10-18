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

		public ConfigHandler(Config config, string configPath, string rootDirectory) : base()
		{
			Config = config;
			Config.ConfigPath = configPath;
			Config.RootDirectory = rootDirectory;

			_fileSystemWatcher = new FileSystemWatcher(config.RootDirectory, Path.GetFileName(config.ConfigPath))
			{
				EnableRaisingEvents = true,
				NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.CreationTime
			};
			_fileSystemWatcher.Changed += ConfigChanged;
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