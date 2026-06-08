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
using System.Xml.Linq;

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class ArchiveParser : ElementParser
	{
		ulong size;
		string checksum;
		string checksumType;
		Uri url;
		string hostOS;
		string hostArch;
		uint hostBits;
		IList<ArchivePatch> patches;

		public Archive Archive { get; private set; }

		public ArchiveParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return null;
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"complete", ParseChildElement_Complete},
				{"host-os", (XElement element) => hostOS = element.Value},
				{"host-bits", (XElement element) => hostBits = element.Value.AsUInt ()},
				{"host-arch", (XElement element) => {
					hostArch = element.GetElementValue();
					if (hostBits != 0) {
						hostBits = HostArchToBits(hostArch);
					}
				}},
				{"patches", ParseChildElement_Patches}
			};
		}

		static uint HostArchToBits (string hostArch)
		{
			switch (hostArch) {
				case "aarch64": return 64;
				case "x64":     return 64;
				case "x86":     return 32;
			}
			throw new NotSupportedException ($"Unsupported architecture {hostArch}");
		}

		void ParseChildElement_Patches (XElement element)
		{
			var ap = new ArchivePatchesParser (Context, element, Namespaces);
			ap.Parse ();
			patches = ap.Patches;
		}

		void ParseChildElement_Complete (XElement element)
		{
			var ame = new ArchiveMetadataExtractor (element, Context);
			size = ame.Size;
			checksum = ame.Checksum;
			checksumType = ame.ChecksumType;
			url = ame.Url;
		}

		protected override void Parsed ()
		{
			base.Parsed ();
			Archive = new Archive (hostOS) {
				Size = size,
				Checksum = checksum,
				ChecksumType = checksumType,
				Url = url,
				HostBits = hostBits,
				HostArch = hostArch,
				Patches = patches
			};
		}
	}
}
