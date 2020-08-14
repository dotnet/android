using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.SourceWriter
{
	public static class StringExtensions
	{
		public static bool HasValue (this string str) => !string.IsNullOrWhiteSpace (str);
	}
}
