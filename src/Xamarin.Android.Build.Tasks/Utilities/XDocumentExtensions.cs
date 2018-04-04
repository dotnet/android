using System;
using System.IO;
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

		public static string ToFullString (this XElement element)
		{
			return element.ToString (SaveOptions.DisableFormatting);
		}

		public static void SaveIfChanged (this XDocument document, string fileName)
		{
			var tempFile = System.IO.Path.GetTempFileName ();
			try {
				document.Save (tempFile);
				MonoAndroidHelper.CopyIfChanged (tempFile, fileName);
			} finally {
				File.Delete (tempFile);
			}
		}
	}
}

