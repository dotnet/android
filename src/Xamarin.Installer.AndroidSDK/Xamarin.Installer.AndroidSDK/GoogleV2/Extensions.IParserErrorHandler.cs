//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Marek Habersack
//
//  All rights reserved.
//
using System;
using System.Xml.Linq;

namespace Xamarin.Installer.AndroidSDK.GoogleV2
{
	public static partial class Extensions
	{
		public static void Fatal (this IParserErrorHandler handler, string message)
		{
			handler.Fatal (null, null, message);
		}

		public static void Fatal (this IParserErrorHandler handler, Uri url, string message)
		{
			handler.Fatal (url, null, message);
		}

		public static void Fatal (this IParserErrorHandler handler, XElement element, string message, bool addLocationInfo = true)
		{
			handler.Fatal (null, element, message);
		}

		public static void Fatal (this IParserErrorHandler handler, Uri documentUrl, XElement element, string message, bool addLocationInfo = true)
		{
			if (handler == null)
				return;

			handler.OnFatalError (documentUrl, element, $"{message}{RenderLocation (documentUrl, element, addLocationInfo)}");
		}

		public static void Error (this IParserErrorHandler handler, string message)
		{
			handler.Error (null, null, message);
		}

		public static void Error (this IParserErrorHandler handler, Uri url, string message)
		{
			handler.Error (url, null, message);
		}

		public static void Error (this IParserErrorHandler handler, XElement element, string message, bool addLocationInfo = true)
		{
			handler.Error (null, element, message, addLocationInfo);
		}

		public static void Error (this IParserErrorHandler handler, Uri documentUrl, XElement element, string message, bool addLocationInfo = true)
		{
			ReportRecoverableError (handler, ParserErrorLevel.Error, documentUrl, element, message, addLocationInfo);
		}

		public static void Warning (this IParserErrorHandler handler, string message)
		{
			handler.Warning (null, null, message);
		}

		public static void Warning (this IParserErrorHandler handler, Uri url, string message)
		{
			handler.Warning (url, null, message);
		}

		public static void Warning (this IParserErrorHandler handler, XElement element, string message, bool addLocationInfo = true)
		{
			handler.Warning (null, element, message, addLocationInfo);
		}

		public static void Warning (this IParserErrorHandler handler, Uri documentUrl, XElement element, string message, bool addLocationInfo = true)
		{
			ReportRecoverableError (handler, ParserErrorLevel.Warning, documentUrl, element, message, addLocationInfo);
		}

		public static void Info (this IParserErrorHandler handler, string message)
		{
			handler.Info (null, null, message);
		}

		public static void Info (this IParserErrorHandler handler, Uri url, string message)
		{
			handler.Info (url, null, message);
		}

		public static void Info (this IParserErrorHandler handler, XElement element, string message, bool addLocationInfo = true)
		{
			handler.Info (null, element, message, addLocationInfo);
		}

		public static void Info (this IParserErrorHandler handler, Uri documentUrl, XElement element, string message, bool addLocationInfo = true)
		{
			ReportRecoverableError (handler, ParserErrorLevel.Info, documentUrl, element, message, addLocationInfo);
		}

		public static void Debug (this IParserErrorHandler handler, string message)
		{
			handler.Debug (null, null, message);
		}

		public static void Debug (this IParserErrorHandler handler, Uri url, string message)
		{
			handler.Debug (url, null, message);
		}

		public static void Debug (this IParserErrorHandler handler, XElement element, string message, bool addLocationInfo = true)
		{
			handler.Debug (null, element, message, addLocationInfo);
		}

		public static void Debug (this IParserErrorHandler handler, Uri documentUrl, XElement element, string message, bool addLocationInfo = true)
		{
			ReportRecoverableError (handler, ParserErrorLevel.Debug, documentUrl, element, message, addLocationInfo);
		}

		static void ReportRecoverableError (IParserErrorHandler handler, ParserErrorLevel level, Uri documentUrl, XElement element, string message, bool addLocationInfo = true)
		{
			if (handler == null)
				return;

			handler.OnRecoverableError (level, documentUrl, element, $"{message}{RenderLocation (documentUrl, element, addLocationInfo)}");
		}

		static string RenderLocation (Uri documentUrl, XElement element, bool addLocationInfo)
		{
			if (!addLocationInfo)
				return String.Empty;

			return $" {element.GetLocation (documentUrl)}";
		}
	}
}
