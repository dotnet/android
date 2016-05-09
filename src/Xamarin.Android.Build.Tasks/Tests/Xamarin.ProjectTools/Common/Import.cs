using System;

namespace Xamarin.ProjectTools
{
	public class Import
	{
		public Import (string project)
			: this (() => project)
		{
		}

		public Import (Func<string> project) {
			Project = project; 
		}

		public Func<string> Project { get; set; }
		public Func<string> TextContent { get; set; }
	}
}

