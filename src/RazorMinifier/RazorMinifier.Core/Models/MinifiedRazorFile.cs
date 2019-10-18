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

			return (obj1.EditFilePath == obj2.EditFilePath && obj1.SourceFilePath == obj2.SourceFilePath);
		}
		public static bool operator !=(MinifiedRazorFile obj1, MinifiedRazorFile obj2)
		{
			return !(obj1 == obj2);
		}
	}
}
