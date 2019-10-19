using Newtonsoft.Json;
using System;

namespace RazorMinifier.Core.Models
{
	[Serializable]
	public class MinifiedRazorFile
	{
		public string InputPath { get; set; }

		[JsonIgnore]
		internal string FullInputPath { get; set; }

		public string OutputPath { get; set; }

		[JsonIgnore]
		internal string FullOutputPath { get; set; }

		public static bool operator ==(MinifiedRazorFile obj1, MinifiedRazorFile obj2)
		{
			if (ReferenceEquals(obj1, obj2))
			{
				return true;
			}
			if (obj1 is null)
			{
				return false;
			}
			if (obj2 is null)
			{
				return false;
			}

			return (obj1.InputPath == obj2.InputPath && obj1.OutputPath == obj2.OutputPath);
		}
		public static bool operator !=(MinifiedRazorFile obj1, MinifiedRazorFile obj2)
		{
			return !(obj1 == obj2);
		}
	}
}
