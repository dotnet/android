using System;
using Android.Runtime;

namespace Android.Text.Format {

	partial class DateUtils {

		[Obsolete ("This method was improperly bound. Please use FormatSameDayTime(Int64, Int64, Int32, Int32).")]
		public static string FormatSameDayTime (long then, long now, AbbreviationLength dateStyle, AbbreviationLength timeStyle)
		{
			return FormatSameDayTime (then, now, (int) dateStyle, (int) timeStyle);
		}

		[Obsolete ("This method was improperly bound. Please use FormatSameDayTimeFormatted(Int64, Int64, Int32, Int32).")]
		public static Java.Lang.ICharSequence FormatSameDayTimeFormatted (long then, long now, AbbreviationLength dateStyle, AbbreviationLength timeStyle)
		{
			return FormatSameDayTimeFormatted (then, now, (int) dateStyle, (int) timeStyle);
		}
	}
}
