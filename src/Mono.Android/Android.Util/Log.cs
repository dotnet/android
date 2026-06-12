using System;
using System.Globalization;
using Android.Runtime;

namespace Android.Util {

	public partial class Log {

		/// <summary>
		/// IFormatProvider passed to any underlying string.Format() calls. Defaults to System.Globalization.CultureInfo.CurrentCulture.
		/// </summary>
#if ANDROID_34
		public
#endif  // ANDROID_34
		static IFormatProvider FormatProvider { get; set; } = CultureInfo.CurrentCulture;

		/// <summary>
		/// Sends a <see cref="LogPriority.Debug"/> log message using a composite format string.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#d(java.lang.String,%20java.lang.String)">Android documentation for <c>android.util.Log.d</c></seealso>
		public static int Debug (string tag, string format, params object[] args)
		{
			return Debug (tag, string.Format (FormatProvider, format, args));
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Debug"/> log message with an associated <see cref="Java.Lang.Throwable"/>.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="tr">An exception to log.</param>
		/// <param name="msg">The message to log.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#d(java.lang.String,%20java.lang.String,%20java.lang.Throwable)">Android documentation for <c>android.util.Log.d</c></seealso>
		public static int Debug (string tag, Java.Lang.Throwable tr, string msg)
		{
			return Debug (tag, msg, tr);
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Debug"/> log message using a composite format string, with an associated <see cref="Java.Lang.Throwable"/>.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="tr">An exception to log.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#d(java.lang.String,%20java.lang.String,%20java.lang.Throwable)">Android documentation for <c>android.util.Log.d</c></seealso>
		public static int Debug (string tag, Java.Lang.Throwable tr, string format, params object[] args)
		{
			return Debug (tag, string.Format (FormatProvider, format, args), tr);
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Error"/> log message using a composite format string.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#e(java.lang.String,%20java.lang.String)">Android documentation for <c>android.util.Log.e</c></seealso>
		public static int Error (string tag, string format, params object[] args)
		{
			return Error (tag, string.Format (FormatProvider, format, args));
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Error"/> log message with an associated <see cref="Java.Lang.Throwable"/>.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="tr">An exception to log.</param>
		/// <param name="msg">The message to log.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#e(java.lang.String,%20java.lang.String,%20java.lang.Throwable)">Android documentation for <c>android.util.Log.e</c></seealso>
		public static int Error (string tag, Java.Lang.Throwable tr, string msg)
		{
			return Error (tag, msg, tr);
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Error"/> log message using a composite format string, with an associated <see cref="Java.Lang.Throwable"/>.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="tr">An exception to log.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#e(java.lang.String,%20java.lang.String,%20java.lang.Throwable)">Android documentation for <c>android.util.Log.e</c></seealso>
		public static int Error (string tag, Java.Lang.Throwable tr, string format, params object[] args)
		{
			return Error (tag, string.Format (FormatProvider, format, args), tr);
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Info"/> log message using a composite format string.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#i(java.lang.String,%20java.lang.String)">Android documentation for <c>android.util.Log.i</c></seealso>
		public static int Info (string tag, string format, params object[] args)
		{
			return Info (tag, string.Format (FormatProvider, format, args));
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Info"/> log message with an associated <see cref="Java.Lang.Throwable"/>.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="tr">An exception to log.</param>
		/// <param name="msg">The message to log.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#i(java.lang.String,%20java.lang.String,%20java.lang.Throwable)">Android documentation for <c>android.util.Log.i</c></seealso>
		public static int Info (string tag, Java.Lang.Throwable tr, string msg)
		{
			return Info (tag, msg, tr);
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Info"/> log message using a composite format string, with an associated <see cref="Java.Lang.Throwable"/>.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="tr">An exception to log.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#i(java.lang.String,%20java.lang.String,%20java.lang.Throwable)">Android documentation for <c>android.util.Log.i</c></seealso>
		public static int Info (string tag, Java.Lang.Throwable tr, string format, params object[] args)
		{
			return Info (tag, string.Format (FormatProvider, format, args), tr);
		}

		/// <summary>
		/// Writes a log message at the specified priority using a composite format string.
		/// </summary>
		/// <param name="priority">The priority of the log message.</param>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#println(int,%20java.lang.String,%20java.lang.String)">Android documentation for <c>android.util.Log.println</c></seealso>
		public static int WriteLine (LogPriority priority, string tag, string format, params object[] args)
		{
			return WriteLine (priority, tag, string.Format (FormatProvider, format, args));
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Verbose"/> log message using a composite format string.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#v(java.lang.String,%20java.lang.String)">Android documentation for <c>android.util.Log.v</c></seealso>
		public static int Verbose (string tag, string format, params object[] args)
		{
			return Verbose (tag, string.Format (FormatProvider, format, args));
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Verbose"/> log message with an associated <see cref="Java.Lang.Throwable"/>.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="tr">An exception to log.</param>
		/// <param name="msg">The message to log.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#v(java.lang.String,%20java.lang.String,%20java.lang.Throwable)">Android documentation for <c>android.util.Log.v</c></seealso>
		public static int Verbose (string tag, Java.Lang.Throwable tr, string msg)
		{
			return Verbose (tag, msg, tr);
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Verbose"/> log message using a composite format string, with an associated <see cref="Java.Lang.Throwable"/>.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="tr">An exception to log.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#v(java.lang.String,%20java.lang.String,%20java.lang.Throwable)">Android documentation for <c>android.util.Log.v</c></seealso>
		public static int Verbose (string tag, Java.Lang.Throwable tr, string format, params object[] args)
		{
			return Verbose (tag, string.Format (FormatProvider, format, args), tr);
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Warn"/> log message using a composite format string.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#w(java.lang.String,%20java.lang.String)">Android documentation for <c>android.util.Log.w</c></seealso>
		public static int Warn (string tag, string format, params object[] args)
		{
			return Warn (tag, string.Format (FormatProvider, format, args));
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Warn"/> log message with an associated <see cref="Java.Lang.Throwable"/>.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="tr">An exception to log.</param>
		/// <param name="msg">The message to log.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#w(java.lang.String,%20java.lang.String,%20java.lang.Throwable)">Android documentation for <c>android.util.Log.w</c></seealso>
		public static int Warn (string tag, Java.Lang.Throwable tr, string msg)
		{
			return Warn (tag, msg, tr);
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Warn"/> log message using a composite format string, with an associated <see cref="Java.Lang.Throwable"/>.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="tr">An exception to log.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#w(java.lang.String,%20java.lang.String,%20java.lang.Throwable)">Android documentation for <c>android.util.Log.w</c></seealso>
		public static int Warn (string tag, Java.Lang.Throwable tr, string format, params object[] args)
		{
			return Warn (tag, string.Format (FormatProvider, format, args), tr);
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Wtf"/> log message using a composite format string.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#wtf(java.lang.String,%20java.lang.String)">Android documentation for <c>android.util.Log.wtf</c></seealso>
		public static int Wtf (string tag, string format, params object[] args)
		{
			return Wtf (tag, string.Format (FormatProvider, format, args));
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Wtf"/> log message with an associated <see cref="Java.Lang.Throwable"/>.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="tr">An exception to log.</param>
		/// <param name="msg">The message to log.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#wtf(java.lang.String,%20java.lang.String,%20java.lang.Throwable)">Android documentation for <c>android.util.Log.wtf</c></seealso>
		public static int Wtf (string tag, Java.Lang.Throwable tr, string msg)
		{
			return Wtf (tag, msg, tr);
		}

		/// <summary>
		/// Sends a <see cref="LogPriority.Wtf"/> log message using a composite format string, with an associated <see cref="Java.Lang.Throwable"/>.
		/// </summary>
		/// <param name="tag">Used to identify the source of a log message.</param>
		/// <param name="tr">An exception to log.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="args">An object array that contains zero or more objects to format.</param>
		/// <returns>The number of bytes written.</returns>
		/// <seealso href="https://developer.android.com/reference/android/util/Log#wtf(java.lang.String,%20java.lang.String,%20java.lang.Throwable)">Android documentation for <c>android.util.Log.wtf</c></seealso>
		public static int Wtf (string tag, Java.Lang.Throwable tr, string format, params object[] args)
		{
			return Wtf (tag, string.Format (FormatProvider, format, args), tr);
		}
	}
}
