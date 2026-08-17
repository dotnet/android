// https://github.com/xamarin/xamarin-android/blob/72bb66856814e0b64e02b21be66a6dc03e1ffcb6/src/Xamarin.Android.Build.Tasks/Utilities/XDocumentExtensions.cs

using System.Linq;
using System.Xml.XPath;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Android.Build.Tasks
{
	public static class XDocumentExtensions
	{
		const string PathsElementName = "Paths";

		public static ITaskItem[] GetPathsAsTaskItems (this XDocument doc, params string[] paths)
		{
			var e = doc.Elements (PathsElementName);
			foreach (var p in paths)
				e = e.Elements (p);
			return e.Select (ToTaskItem).ToArray ();
		}

		static ITaskItem ToTaskItem (XElement element)
		{
			var taskItem = new TaskItem (element.Value);
			foreach (var attribute in element.Attributes ()) {
				taskItem.SetMetadata (attribute.Name.LocalName, attribute.Value);
			}
			return taskItem;
		}

		public static string[] GetPaths (this XDocument doc, params string[] paths)
		{
			var e = doc.Elements (PathsElementName);
			foreach (var p in paths)
				e = e.Elements (p);
			return e.Select (p => p.Value).ToArray ();
		}

		public static string ToFullString (this XElement element)
		{
			return element.ToString (SaveOptions.DisableFormatting);
		}

		public static bool SaveIfChanged (this XDocument document, string fileName)
		{
			using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ())
			using (var xw = new LinePreservedXmlWriter (sw)) {
				xw.WriteNode (document.CreateNavigator (), false);
				xw.Flush ();
				return Files.CopyIfStreamChanged (sw.BaseStream, fileName);
			}
		}
	}
}

