//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Marek Habersack
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class TypeDetailsGenericParser : TypeDetailsParser
	{
		public TypeDetailsGenericParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{ }

		protected override AndroidComponentInfo CreateComponentInfo ()
		{
			return new AndroidComponentInfoGeneric (Type);
		}
	}
}
