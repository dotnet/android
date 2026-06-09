//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft Corp. (http://microsoft.com)
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class ArchiveMetadataExtractor
	{
		public ulong Size { get; }
		public string Checksum { get; }
		public string ChecksumType { get; }
		public Uri Url { get; }

		public ArchiveMetadataExtractor (XElement element, ParserContext parserContext)
		{
			if (element == null)
				throw new ArgumentNullException (nameof (element));

			if (parserContext == null)
				throw new ArgumentNullException (nameof (parserContext));

			ulong s;
			string v = element.GetChildElementValue ("size");
			if (!String.IsNullOrEmpty (v) && UInt64.TryParse (v, out s))
				Size = s;
			else
				Size = 0;

			XElement checksumElement = element.Elements ("checksum")?.FirstOrDefault ();
			if (checksumElement != null) {
				Checksum = checksumElement.Value;
				ChecksumType = checksumElement.Attribute ("type")?.Value;
			}

			Uri url;
			v = element.GetChildElementValue ("url");
			if (String.IsNullOrEmpty (v) || !Uri.TryCreate (v, UriKind.RelativeOrAbsolute, out url))
				return;

			if (url.IsAbsoluteUri)
				Url = url;
			else
				Url = new Uri (parserContext.CurrentManifestBaseURL, url);
		}
	}
}
