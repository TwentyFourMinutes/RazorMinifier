using Newtonsoft.Json;
using RazorMinifier.Core;
using RazorMinifier.Core.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RazorMinifier.Console
{
	public static class Startup
	{
		static async Task Main(string[] args)
		{
			var content = await File.ReadAllTextAsync("Rminify.json");

			var config = JsonConvert.DeserializeObject<Config>(content);

			var fh = new FileHandler(config, Environment.CurrentDirectory.Replace(@"\bin\Debug", string.Empty));

			System.Console.WriteLine("ready");

			await Task.Delay(-1);
		}
	}
}
