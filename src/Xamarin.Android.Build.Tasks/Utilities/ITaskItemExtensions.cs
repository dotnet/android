using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Build.Framework;

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
	}
}
