#if CLASS_PARSE_XML
using System;
using System.Collections.Generic;
using System.Linq;

namespace Android.Test
{
	public partial class MoreAsserts
	{
		[Obsolete ("Use AssertEquals(ICollection<Java.Lang.Object>, ICollection<Java.Lang.Object>) instead.")]
		public static void AssertEquals (ICollection<object> expected, ICollection<object> actual)
		{
			AssertEquals (expected.Cast<Java.Lang.Object> ().ToArray (), actual.Cast<Java.Lang.Object> ().ToArray ());
		}
		[Obsolete ("Use AssertEquals(string message, ICollection<Java.Lang.Object>, ICollection<Java.Lang.Object>) instead.")]
		public static void AssertEquals (string message, ICollection<object> expected, ICollection<object> actual)
		{
			AssertEquals (expected.Cast<Java.Lang.Object> ().ToArray (), actual.Cast<Java.Lang.Object> ().ToArray ());
		}
	}
}
#endif
