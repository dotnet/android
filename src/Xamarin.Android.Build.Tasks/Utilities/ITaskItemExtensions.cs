using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public static class ITaskItemExtensions
	{
		public static IEnumerable<XElement> ToXElements (this ICollection<ITaskItem> items, string itemName, string[] knownMetadata)
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

		public static string GetMetadataOrDefault (this ITaskItem item, string name, string defaultValue)
		{
			var value = item.GetMetadata (name);

			if (string.IsNullOrWhiteSpace (value))
				return defaultValue;

			return value;
		}

		public static string? GetRequiredMetadata (this ITaskItem item, string name, TaskLoggingHelper log)
		{
			var value = item.GetMetadata (name);

			if (string.IsNullOrWhiteSpace (value)) {
				log.LogError ("Item is missing required metadata '{0}'", name);
				return null;
			}

			return value;
		}

		public static bool HasMetadata (this ITaskItem item, string name)
			=> item.MetadataNames.OfType<string> ().Contains (name, StringComparer.OrdinalIgnoreCase);
	}
}
