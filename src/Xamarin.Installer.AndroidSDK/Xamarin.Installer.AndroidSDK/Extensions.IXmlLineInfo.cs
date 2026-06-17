//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Marek Habersack
//
//  All rights reserved.
//
using System;
using System.Xml;

namespace Xamarin.Installer.AndroidSDK
{
	static partial class Extensions
	{
		public static string AsString (this IXmlLineInfo info, string openBracket = "[", string closeBracket = "]")
		{
			if (info == null || (info.LineNumber < 0 && info.LinePosition < 0))
				return String.Empty;
			string open = openBracket ?? String.Empty;
			string close = closeBracket ?? String.Empty;

			return $"{open}{info.LineNumber}:{info.LinePosition}{close}";
		}
	}
}
