using System;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public static class XDocumentExtensions
	{
		public static ITaskItem[] GetPathsAsTaskItems (this XDocument doc, params string[] paths)
		{
			return doc.GetPaths (paths)
				.Select(x => new TaskItem(x))
				.ToArray ();
		}

		public static string[] GetPaths (this XDocument doc, params string[] paths)
		{
			var e = doc.Elements ("Paths");
			foreach (var p in paths)
				e = e.Elements (p);
			return e.Select (p => p.Value).ToArray ();
		}
	}
}

