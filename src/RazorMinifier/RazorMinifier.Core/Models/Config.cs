using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace RazorMinifier.Core.Models
{
	[Serializable]
	public class Config
	{
		[JsonIgnore]
		public string RootDirectory { get; internal set; }

		[JsonIgnore]
		public string ConfigPath { get; internal set; }

		public HashSet<MinifiedRazorFile> Files { get; set; }
	}
}
