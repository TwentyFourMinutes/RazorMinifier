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


		public FileHandler(Config config, string rootDirectory)
		{
			_config = config;
			_config.RootDirectory = Path.GetDirectoryName(rootDirectory);

			_watchers = new List<RazorFileWatcher>();

			Init();
		}

		private void Init()
		{
			foreach (var file in _config.Files)
			{
				file.FullEditFilePath = Path.Combine(_config.RootDirectory, file.EditFilePath);
				file.FullSourceFilePath = Path.Combine(_config.RootDirectory, file.SourceFilePath);

				if (!File.Exists(file.FullEditFilePath))
					continue;

				var watcher = new RazorFileWatcher(file);

				watcher.FileUpdated += FileUpdated;

				_watchers.Add(watcher);
			}
		}

		public IEnumerable<string> GetAllEditFiles()
		{
			return _config.Files.Select(x => x.FullEditFilePath);
		}

		private async Task FileUpdated(RazorFileWatcher watcher)
		{
			try
			{
				if (!File.Exists(watcher.File.FullSourceFilePath))
				{
					File.Create(watcher.File.FullSourceFilePath).Close();
				}

				using (var writer = new StreamWriter(watcher.File.FullSourceFilePath, false))
				using (var reader = new StreamReader(watcher.File.FullEditFilePath))
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
