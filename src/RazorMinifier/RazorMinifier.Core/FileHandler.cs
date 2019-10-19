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
				RemoveFileFromWatchers(file);
			}

			foreach (var file in toAdd)
			{
				await AddFileToWatchers(file);
			}

			_configHandler.SaveConfigFile();
		}

		private async Task<bool> AddFileToWatchers(MinifiedRazorFile file)
		{
			file.FullOutputPath = GetFullPathFromRootDir(file.OutputPath);

			if (!File.Exists(file.FullOutputPath))
			{
				return false;
			}

			if (string.IsNullOrWhiteSpace(file.InputPath))
			{
				await CreateEditFileWithContent(file);
			}
			else
			{
				file.FullInputPath = GetFullPathFromRootDir(file.InputPath);

				if (!File.Exists(file.FullInputPath))
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

		private bool RemoveFileFromWatchers(MinifiedRazorFile file)
		{
			var remove = _watchers.FirstOrDefault(x => x.File.OutputPath == file.OutputPath);

			if (remove is null)
				return false;

			remove.Dispose();

			_watchers.Remove(remove);

			_config.Files.Remove(remove.File);

			return true;
		}

		private string GetFullPathFromRootDir(string relativePath)
			=> new Uri(new Uri(_config.RootDirectory + "\\"), relativePath).LocalPath;


		private async Task CreateEditFileWithContent(MinifiedRazorFile file)
		{
			var editPath = Path.ChangeExtension(file.OutputPath, ".edit.cshtml");

			file.InputPath = editPath;
			file.FullInputPath = GetFullPathFromRootDir(editPath);

			File.Copy(file.FullOutputPath, file.FullInputPath, true);

			await WriteMinifiedContent(file);
		}

		public IEnumerable<string> GetAllEditFiles()
		{
			return _config.Files.Select(x => x.FullInputPath);
		}

		public async Task<bool> AddToConfigFile(MinifiedRazorFile file)
		{
			if (_config.Files.Any(x => x.OutputPath == file.OutputPath))
				return false;

			var result = await AddFileToWatchers(file);

			if (!result)
				return true;

			if (!File.Exists(_config.ConfigPath))
			{
				File.Create(_config.ConfigPath).Close();
			}

			_configHandler.SaveConfigFile();

			return true;
		}

		public void RemoveFromConfigFile(MinifiedRazorFile file)
		{
			var result = RemoveFileFromWatchers(file);

			if (!result)
				return;

			_configHandler.SaveConfigFile();
		}

		private Task FileUpdated(RazorFileWatcher watcher)
		{
			return WriteMinifiedContent(watcher.File);
		}

		private async Task WriteMinifiedContent(MinifiedRazorFile file)
		{
			try
			{
				if (!File.Exists(file.FullOutputPath))
				{
					File.Create(file.FullOutputPath).Close();
				}

				using (var writer = new StreamWriter(file.FullOutputPath, false))
				using (var reader = new StreamReader(file.FullInputPath))
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
