using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.XPath;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
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

		public static IEnumerable<XElement> ToXElements (ICollection<ITaskItem> items, string itemName, string[] knownMetadata)
		{
			foreach (var item in items) {
				var e = new XElement (itemName, item.ItemSpec);
				foreach (var name in knownMetadata) {
					var value = item.GetMetadata (name);
					if (!string.IsNullOrEmpty (value))
						e.SetAttributeValue (name, value);
				}
				yield return e;
			}
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
			using (var stream = new MemoryStream ())
			using (var sw = new StreamWriter (stream))
			using (var xw = new Monodroid.LinePreservedXmlWriter (sw)) {
				xw.WriteNode (document.CreateNavigator (), false);
				xw.Flush ();
				return MonoAndroidHelper.CopyIfStreamChanged (stream, fileName);
			}
		}
	}
}

