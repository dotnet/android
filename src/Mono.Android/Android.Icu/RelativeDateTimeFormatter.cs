using System;

using Android.Content;

#if ANDROID_31
namespace Android.Icu.Text
{
	public partial class RelativeDateTimeFormatter
	{
		// ICharSequence inherits IEnumerable<char> in an Addition, so generator doesn't know about it.
		public partial class FormattedRelativeDateTime
		{
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
			{
				return GetEnumerator ();
			}

			public System.Collections.Generic.IEnumerator<char> GetEnumerator ()
			{
				for (var i = 0; i < Length (); i++)
					yield return CharAt (i);
			}
		}
	}
}
#endif
