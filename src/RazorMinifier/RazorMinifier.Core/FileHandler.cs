using Newtonsoft.Json;
using RazorMinifier.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RazorMinifier.Core
{
	public class FileHandler : IDisposable
	{
		public bool IsDisposed { get; private set; }
		private readonly Config _config;
		private readonly List<RazorFileWatcher> _watchers;

		public FileHandler(Config config, string configPath, string rootDirectory)
		{
			_config = config;
			_config.RootDirectory = Path.GetDirectoryName(rootDirectory);
			_config.ConfigPath = configPath;

			_watchers = new List<RazorFileWatcher>();

			Init();
		}

		private async void Init()
		{
			try
			{
				foreach (var file in _config.Files)
				{
					await AddFileToWatchers(file);
				}
			}
			catch { }
		}

		private async Task AddFileToWatchers(MinifiedRazorFile file)
		{
			file.FullSourceFilePath = GetFullPathFromRootDir(file.SourceFilePath);

			if (!File.Exists(file.FullEditFilePath))
			{
				await CreateEditFileWithContent(file);
			}
			else
			{
				file.FullEditFilePath = GetFullPathFromRootDir(file.EditFilePath);
			}

			var watcher = new RazorFileWatcher(file);

			watcher.FileUpdated += FileUpdated;

			_watchers.Add(watcher);
		}

		private string GetFullPathFromRootDir(string relativePath)
			=> Path.Combine(_config.RootDirectory, relativePath);

		private async Task CreateEditFileWithContent(MinifiedRazorFile file)
		{
			var editPath = Path.ChangeExtension(file.SourceFilePath, ".edit.cshtml");

			file.EditFilePath = editPath;
			file.FullEditFilePath = GetFullPathFromRootDir(editPath);

			File.Copy(file.FullSourceFilePath, file.FullEditFilePath, true);

			await WriteMinifiedContent(file);
		}

		public IEnumerable<string> GetAllEditFiles()
		{
			return _config.Files.Select(x => x.FullEditFilePath);
		}

		public async Task AddToConfigFile(MinifiedRazorFile file)
		{
			await AddFileToWatchers(file);
			_config.Files.Add(file);

			using (var sw = new StreamWriter(_config.ConfigPath, false))
			{
				await sw.WriteAsync(JsonConvert.SerializeObject(_config, Formatting.Indented));

				sw.Close();
			}
		}

		private Task FileUpdated(RazorFileWatcher watcher)
		{
			return WriteMinifiedContent(watcher.File);
		}

		private async Task WriteMinifiedContent(MinifiedRazorFile file)
		{
			try
			{
				if (!File.Exists(file.FullSourceFilePath))
				{
					File.Create(file.FullSourceFilePath).Close();
				}

				using (var writer = new StreamWriter(file.FullSourceFilePath, false))
				using (var reader = new StreamReader(file.FullEditFilePath))
				{
					var content = await reader.ReadToEndAsync();

					content = Minifier.Minify(content);

					await writer.WriteAsync(content);

					writer.Close();
					reader.Close();
				}
			}
			catch { }
		}

		public void Dispose()
		{
			if (!IsDisposed)
			{
				foreach (var watcher in _watchers)
				{
					watcher.FileUpdated -= FileUpdated;

					watcher.Dispose();
				}
				IsDisposed = true;
			}
		}
	}
}
