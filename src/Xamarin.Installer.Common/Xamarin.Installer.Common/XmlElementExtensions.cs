using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Xamarin.Installer.Common
{
	public static class XmlElementExtensions
	{
		public static string SelectSingleElementValue (this XmlElement element, string xpath)
		{
			if (element == null || String.IsNullOrEmpty (xpath))
				return null;

			var found = element.SelectSingleNode (xpath) as XmlElement;
			return found == null ? null : found.InnerText;
		}

		public static string GetChildValue (this XmlElement element, string childName)
		{
			if (element == null || String.IsNullOrEmpty (childName))
				return null;

			XmlElement child = element[childName];
			if (child == null)
				return null;

			string ret = child.InnerText;
			if (String.IsNullOrEmpty (ret))
				return null;

			return ret;
		}

		public static bool GetAttributeValue<T> (this XmlElement element, string attributeName, out T result)
		{
			result = default (T);
			if (element == null)
				return false;
			return ConvertValue (element.GetAttribute (attributeName), out result);
		}

		public static bool GetChildValue<T> (this XmlElement element, string childName, out T result)
		{
			result = default (T);
			if (element == null)
				return false;
			return ConvertValue (element.GetChildValue (childName), out result);
		}

		static bool ConvertValue<T> (string value, out T result)
		{
			result = default (T);
			if (value != null)
				value = value.Trim ();
			if (String.IsNullOrEmpty (value))
				return false;

			if (typeof (T) == typeof (string)) {
				result = (T) ((object) value);
				return true;
			}

			try {
				result = (T) Convert.ChangeType (value, typeof (T));
			} catch {
				// ignore
				return false;
			}

			return true;
		}

		public static XmlElement GetChildElement (this XmlElement parent, XmlNamespaceManager nsmgr, string xpath, bool required = true)
		{
			var ret = parent.SelectSingleNode (xpath, nsmgr) as XmlElement;
			if (ret == null && required)
				throw new InvalidOperationException ("Required child element not found");
			return ret;
		}
	}
}
