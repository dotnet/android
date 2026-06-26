using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace Xamarin.Installer.Common
{
	public static class Logger
	{
		public static ILogAdapter LogAdapter { get; set; }

		public static void Action(string format, params object[] parms)
		{
			if (LogAdapter == null)
				return;
			LogAdapter.Action(format, parms);
		}

		public static void Debug (string format, params object[] parms)
		{
			if (LogAdapter == null)
				return;
			LogAdapter.Debug (format, parms);
		}

		public static void Debug (string format, Exception ex, params object[] parms)
		{
			if (LogAdapter == null)
				return;
			LogAdapter.Debug (format, ex, parms);
		}

		public static void Error (string format, params object[] parms)
		{
			if (LogAdapter == null)
				return;
			LogAdapter.Error (format, parms);
		}

		public static void Exception (string format, Exception ex, params object[] parms)
		{
			if (LogAdapter == null)
				return;
			LogAdapter.Exception (format, ex, parms);
		}

		public static void Info (string format, params object[] parms)
		{
			if (LogAdapter == null)
				return;
			LogAdapter.Info (format, parms);
		}
		
		public static void Warning (string format, params object[] parms)
		{
			if (LogAdapter == null)
				return;
			LogAdapter.Warning (format, parms);
		}

		public static void SetOperationStatus (OperationStatus status)
		{
			if (LogAdapter == null)
				return;
			LogAdapter.SetOperationStatus (status);
		}

		public static void LogManifest (string manifestName, Stream contentStream)
		{
			contentStream.Seek (0, SeekOrigin.Begin);
			var content = new StreamReader (contentStream).ReadToEnd ();
			if (LogAdapter is ILogAdapterExtended extendedLogAdapter) {
				extendedLogAdapter.SaveManifest (manifestName, content);
			} else {
				Debug ($"{manifestName} :\n{content}");
			}
		}
	}
}
