using RazorMinifier.Core.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RazorMinifier.Core
{
	internal class RazorFileWatcher : IDisposable
	{
		public readonly MinifiedRazorFile File;
		public readonly FileSystemWatcher _fileSystemWatcher;
		public event Func<RazorFileWatcher, Task> FileUpdated;
		public bool IsDisposed { get; private set; }

		public RazorFileWatcher(MinifiedRazorFile file) : base()
		{
			if (file is null)
				throw new NullReferenceException(file.GetType().Name);

			File = file;

			_fileSystemWatcher = new FileSystemWatcher();

			Init();
		}

		private void Init()
		{
			_fileSystemWatcher.Filter = Path.GetFileName(File.EditFilePath);
			_fileSystemWatcher.Path = Path.GetDirectoryName(File.FullEditFilePath);
			_fileSystemWatcher.EnableRaisingEvents = true;
			_fileSystemWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.CreationTime;

			_fileSystemWatcher.Changed += RazorFileWatcher_Changed;
		}

		private void RazorFileWatcher_Changed(object sender, FileSystemEventArgs e)
		{
			_ = Task.Run(async () =>
			{
				await FileUpdated?.Invoke(this);
			});
		}

		public void Dispose()
		{
			if (!IsDisposed)
			{
				_fileSystemWatcher.Dispose();
				IsDisposed = true;
			}
		}
	}
}
