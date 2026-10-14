using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Installer.AndroidSDK
{
	static partial class Extensions
	{
		public static bool GetProperty<T> (this IDictionary<string, string> props, string name, out T result)
		{
			result = default (T);
			if (props == null)
				return false;
			if (!props.TryGetValue (name, out string value))
				return false;
			if (value != null)
				value = value.Trim ();
			if (String.IsNullOrEmpty (value))
				return false;

			if (typeof (T) == typeof (string)) {
				result = (T)((object)value);
				return true;
			}

			try {
				result = (T)Convert.ChangeType (value, typeof (T));
			} catch {
				// ignore
				return false;
			}
			return true;
		}
	}
}
