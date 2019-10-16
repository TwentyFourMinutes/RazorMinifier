using Newtonsoft.Json;
using System;

namespace RazorMinifier.Core.Models
{
	[Serializable]
	public class MinifiedRazorFile
	{
		public string EditFilePath { get; set; }

		[JsonIgnore]
		internal string FullEditFilePath { get; set; }

		public string SourceFilePath { get; set; }

		[JsonIgnore]
		internal string FullSourceFilePath { get; set; }
	}
}
