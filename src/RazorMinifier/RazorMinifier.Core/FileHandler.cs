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
		private readonly List<RazorFileWatcher> _watchers;
		private readonly ConfigHandler _configHandler;

		private Config _config
		{
			get => _configHandler.Config;
		}

		public FileHandler(ConfigHandler configHandler)
		{
			_configHandler = configHandler;

			_watchers = new List<RazorFileWatcher>();

			_configHandler.ConfigUpdated += ConfigUpdated;

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

		private async Task ConfigUpdated(List<MinifiedRazorFile> toRemove, List<MinifiedRazorFile> toAdd)
		{
			foreach (var file in toRemove)
			{
				var remove = _watchers.FirstOrDefault(x => x.File.EditFilePath == file.EditFilePath && x.File.SourceFilePath == file.SourceFilePath);

				_watchers.Remove(remove);
			}

			foreach (var file in toAdd)
			{
				await AddFileToWatchers(file);
			}
		}

		private async Task<bool> AddFileToWatchers(MinifiedRazorFile file)
		{
			file.FullSourceFilePath = GetFullPathFromRootDir(file.SourceFilePath);

			if (!File.Exists(file.FullSourceFilePath))
			{
				return false;
			}

			if (string.IsNullOrWhiteSpace(file.EditFilePath))
			{
				await CreateEditFileWithContent(file);
			}
			else
			{
				file.FullEditFilePath = GetFullPathFromRootDir(file.EditFilePath);

				if (!File.Exists(file.FullEditFilePath))
				{
					await CreateEditFileWithContent(file);
				}
			}

			var watcher = new RazorFileWatcher(file);

			watcher.FileUpdated += FileUpdated;

			_watchers.Add(watcher);
			_config.Files.Add(file);

			return true;
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
			if (_config.Files.Any(x => x.EditFilePath == file.EditFilePath && x.SourceFilePath == file.SourceFilePath))
				return;

			var result = await AddFileToWatchers(file);

			if (!result)
				return;

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

				_configHandler.Dispose();

				IsDisposed = true;
			}
		}
	}
}
