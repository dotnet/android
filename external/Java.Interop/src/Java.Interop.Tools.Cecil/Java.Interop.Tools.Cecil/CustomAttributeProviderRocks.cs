using System;
using System.Collections.Generic;
using System.Linq;

using Mono.Cecil;

namespace Java.Interop.Tools.Cecil {

	public static class CustomAttributeProviderRocks
	{
		public static bool AnyCustomAttributes (this ICustomAttributeProvider item, Type attribute) =>
			item.AnyCustomAttributes (attribute.FullName);

		public static IEnumerable<CustomAttribute> GetCustomAttributes (this ICustomAttributeProvider item, Type attribute)
		{
			return item.GetCustomAttributes (attribute.FullName);
		}

		public static bool AnyCustomAttributes (this ICustomAttributeProvider item, string attribute_fullname)
		{
			foreach (CustomAttribute custom_attribute in item.CustomAttributes) {
				if (custom_attribute.Constructor.DeclaringType.FullName == attribute_fullname)
					return true;
			}
			return false;
		}

		public static IEnumerable<CustomAttribute> GetCustomAttributes (this ICustomAttributeProvider item, string attribute_fullname)
		{
			foreach (CustomAttribute custom_attribute in item.CustomAttributes) {
				if (custom_attribute.Constructor.DeclaringType.FullName != attribute_fullname)
					continue;

				yield return custom_attribute;
			}
		}

		public static IEnumerable<CustomAttribute> GetCustomAttributes (this IEnumerable<ICustomAttributeProvider> items, Type attribute)
		{
			return items.GetCustomAttributes (attribute.FullName);
		}

		public static IEnumerable<CustomAttribute> GetCustomAttributes (this IEnumerable<ICustomAttributeProvider> items, string attribute_fullname)
		{
			return items.SelectMany (e => e.GetCustomAttributes (attribute_fullname));
		}
	}
}
