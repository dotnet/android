//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft Corp. (http://microsoft.com)
//
//  All rights reserved.
//
using System;
using System.Xml.Linq;

namespace Xamarin.Installer.AndroidSDK.GoogleV2
{
	public interface IParserErrorHandler
	{
		void OnRecoverableError (ParserErrorLevel recommendedLevel, Uri url, XElement element, string message);
		void OnFatalError (Uri url, XElement element, string message);
	}
}
