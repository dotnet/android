using System;

using Android.Content;

#if ANDROID_31
namespace Android.Icu.Text
{
	public partial class DateIntervalFormat
	{
		// ICharSequence inherits IEnumerable<char> in an Addition, so generator doesn't know about it.
		public sealed partial class FormattedDateInterval
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
