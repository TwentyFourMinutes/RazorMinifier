using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace RazorMinifier.Core.Models
{
	[Serializable]
	public class Config
	{
		[JsonIgnore]
		internal string RootDirectory { get; set; }
		public List<MinifiedRazorFile> Files { get; set; }
	}
}
