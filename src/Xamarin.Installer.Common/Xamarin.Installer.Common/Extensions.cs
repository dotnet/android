using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Xamarin.Web.Installer
{
	public static partial class Extensions
	{
		static readonly DateTime epoch = new DateTime (1970, 1, 1, 0, 0, 0, 0).ToUniversalTime ();

		public static string JsonQuoted (this string text)
		{
			return "\"" + text + "\"";
		}

		public static long ToUnixTimestamp (this DateTime dt)
		{
			return (long)(dt - epoch).TotalSeconds;
		}

		public static string ToBase64 (this string data)
		{
			if (String.IsNullOrEmpty (data))
				return String.Empty;

			return Convert.ToBase64String (Encoding.UTF8.GetBytes (data));
		}

		public static string FromBase64 (this string data)
		{
			if (String.IsNullOrEmpty (data))
				return String.Empty;

			return Encoding.UTF8.GetString (Convert.FromBase64String (data));
		}

		public static IEnumerable<TSource> DistinctBy<TSource, TKey>
			(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
		{
			HashSet<TKey> knownKeys = new HashSet<TKey> ();
			foreach (TSource element in source) {
				if (knownKeys.Add (keySelector (element))) {
					yield return element;
				}
			}
		}
	}
}
