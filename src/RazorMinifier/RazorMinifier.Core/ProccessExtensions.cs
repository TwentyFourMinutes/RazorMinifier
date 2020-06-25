using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace RazorMinifier.Core
{
	public static class ProcessExtensions
	{
		public static async Task StartAndWaitForExitAsync(this Process process, bool dispose = true, CancellationToken cancellationToken = default)
		{
			var tcs = new TaskCompletionSource<bool>();

			void Process_Exited(object sender, EventArgs e)
			{
				tcs.TrySetResult(true);
			}

			process.EnableRaisingEvents = true;
			process.Exited += Process_Exited;

			process.Start();

			try
			{
				if (process.HasExited)
				{
					return;
				}

				using (cancellationToken.Register(() => tcs.TrySetCanceled()))
				{
					await tcs.Task;
				}
			}
			finally
			{
				process.Exited -= Process_Exited;

				if (dispose)
				{
					process.Dispose();
				}
			}
		}
	}
}
