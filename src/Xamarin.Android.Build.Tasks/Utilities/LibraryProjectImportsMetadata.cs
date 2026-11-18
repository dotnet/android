#nullable enable

using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	static class LibraryProjectImportsMetadata
	{
		internal const string OriginalFile = "OriginalFile";
		internal const string AndroidSkipResourceProcessing = "AndroidSkipResourceProcessing";
		internal const string NuGetPackageVersion = "NuGetPackageVersion";
		internal const string NuGetPackageId = "NuGetPackageId";

		internal static readonly string [] KnownMetadata = new [] {
			OriginalFile,
			AndroidSkipResourceProcessing,
			NuGetPackageId,
			NuGetPackageVersion,
		};

		internal static IEnumerable<XElement> ToXElements (ICollection<ITaskItem> items, string itemName)
		{
			foreach (var item in items) {
				var element = new XElement (itemName, item.ItemSpec);
				foreach (var name in KnownMetadata) {
					var value = item.GetMetadata (name);
					if (!value.IsNullOrEmpty ())
						element.SetAttributeValue (name, value);
				}
				yield return element;
			}
		}
	}
}
