using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Android.Build.Tasks;
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

		[return: NotNullIfNotNull (nameof (defaultValue))]
		public static T? GetMetadataOrDefault<T> (this ITaskItem item, string name, T? defaultValue)
		{
			var value = item.GetMetadata (name);

			if (string.IsNullOrWhiteSpace (value))
				return defaultValue;

			return (T?)Convert.ChangeType (value, typeof (T));
		}

		public static string? GetRequiredMetadata (this ITaskItem item, string itemName, string name, TaskLoggingHelper log)
		{
			var value = item.GetMetadata (name);

			if (string.IsNullOrWhiteSpace (value)) {
				log.LogCodedError ("XA4234", Properties.Resources.XA4234, itemName, item.ToString (), name);
				return null;
			}

			return value;
		}

		public static bool TryGetRequiredMetadata (this ITaskItem item, string itemName, string name, TaskLoggingHelper log, out string value)
		{
			value = item.GetMetadata (name);

			if (string.IsNullOrWhiteSpace (value)) {
				log.LogCodedError ("XA4234", Properties.Resources.XA4234, itemName, item.ToString (), name);
				return false;
			}

			return true;
		}

		public static bool HasMetadata (this ITaskItem item, string name)
			=> item.MetadataNames.OfType<string> ().Contains (name, StringComparer.OrdinalIgnoreCase);
	}
}
