using System;
using System.Globalization;
using Android.Runtime;

namespace Android.Util {

	public partial class Log {

		public static int Debug (string tag, string format, params object[] args)
		{
			return Debug (tag, string.Format (CultureInfo.InvariantCulture, format, args));
		}

		public static int Debug (string tag, Java.Lang.Throwable tr, string msg)
		{
			return Debug (tag, msg, tr);
		}

		public static int Debug (string tag, Java.Lang.Throwable tr, string format, params object[] args)
		{
			return Debug (tag, string.Format (CultureInfo.InvariantCulture, format, args), tr);
		}

		public static int Error (string tag, string format, params object[] args)
		{
			return Error (tag, string.Format (CultureInfo.InvariantCulture, format, args));
		}

		public static int Error (string tag, Java.Lang.Throwable tr, string msg)
		{
			return Error (tag, msg, tr);
		}

		public static int Error (string tag, Java.Lang.Throwable tr, string format, params object[] args)
		{
			return Error (tag, string.Format (CultureInfo.InvariantCulture, format, args), tr);
		}

		public static int Info (string tag, string format, params object[] args)
		{
			return Info (tag, string.Format (CultureInfo.InvariantCulture, format, args));
		}

		public static int Info (string tag, Java.Lang.Throwable tr, string msg)
		{
			return Info (tag, msg, tr);
		}

		public static int Info (string tag, Java.Lang.Throwable tr, string format, params object[] args)
		{
			return Info (tag, string.Format (CultureInfo.InvariantCulture, format, args), tr);
		}

		public static int WriteLine (LogPriority priority, string tag, string format, params object[] args)
		{
			return WriteLine (priority, tag, string.Format (CultureInfo.InvariantCulture, format, args));
		}

		public static int Verbose (string tag, string format, params object[] args)
		{
			return Verbose (tag, string.Format (CultureInfo.InvariantCulture, format, args));
		}

		public static int Verbose (string tag, Java.Lang.Throwable tr, string msg)
		{
			return Verbose (tag, msg, tr);
		}

		public static int Verbose (string tag, Java.Lang.Throwable tr, string format, params object[] args)
		{
			return Verbose (tag, string.Format (CultureInfo.InvariantCulture, format, args), tr);
		}

		public static int Warn (string tag, string format, params object[] args)
		{
			return Warn (tag, string.Format (CultureInfo.InvariantCulture, format, args));
		}

		public static int Warn (string tag, Java.Lang.Throwable tr, string msg)
		{
			return Warn (tag, msg, tr);
		}

		public static int Warn (string tag, Java.Lang.Throwable tr, string format, params object[] args)
		{
			return Warn (tag, string.Format (CultureInfo.InvariantCulture, format, args), tr);
		}

		public static int Wtf (string tag, string format, params object[] args)
		{
			return Wtf (tag, string.Format (CultureInfo.InvariantCulture, format, args));
		}

		public static int Wtf (string tag, Java.Lang.Throwable tr, string msg)
		{
			return Wtf (tag, msg, tr);
		}

		public static int Wtf (string tag, Java.Lang.Throwable tr, string format, params object[] args)
		{
			return Wtf (tag, string.Format (CultureInfo.InvariantCulture, format, args), tr);
		}
	}
}
