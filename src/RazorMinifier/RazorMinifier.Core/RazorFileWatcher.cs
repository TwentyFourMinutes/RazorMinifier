using RazorMinifier.Core.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RazorMinifier.Core
{
	public class RazorFileWatcher : FileSystemWatcher, IDisposable
	{
		public readonly MinifiedRazorFile File;
		public event Func<RazorFileWatcher, Task> FileUpdated;
		public bool IsDisposed { get; private set; }

		public RazorFileWatcher(MinifiedRazorFile file) : base()
		{
			File = file;
			if (file is null)
				throw new NullReferenceException(file.GetType().Name);

			Init();
		}

		private void Init()
		{
			this.Filter = System.IO.Path.GetFileName(File.EditFilePath);
			this.Path = System.IO.Path.GetDirectoryName(File.FullEditFilePath);
			this.EnableRaisingEvents = true;
			this.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.CreationTime;

			this.Changed += RazorFileWatcher_Changed;
		}

		private void RazorFileWatcher_Changed(object sender, FileSystemEventArgs e)
		{
			_ = Task.Run(async () =>
			{
				await FileUpdated?.Invoke(this);
			});
		}

		public new void Dispose()
		{
			if (!IsDisposed)
			{
				base.Dispose();
				IsDisposed = true;
			}
		}
	}
}
