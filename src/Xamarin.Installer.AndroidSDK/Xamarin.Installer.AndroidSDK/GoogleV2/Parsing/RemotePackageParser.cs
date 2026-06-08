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

using IOPath = System.IO.Path;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class RemotePackageParser : ElementParser
	{
		Repository repository;

		protected string Path { get; private set; } = String.Empty;
		protected string FileSystemPath { get; private set; }
		protected bool Obsolete { get; set; }
		protected AndroidComponentInfo Info { get; private set; }
		protected AndroidRevision Revision { get; private set; }
		protected string DisplayName { get; private set; } = String.Empty;
		protected IList<Dependency> Dependencies { get; private set; }
		protected string ChannelID { get; private set; }
		protected IList<Archive> Archives { get; private set; }
		protected string LicenseID { get; private set; }

		public RemotePackage Package { get; private set; }

		internal bool IncludeAllArchives { get; set; } // For Xamarin manifest generator this is 'true'

		public RemotePackageParser (Repository repository, ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
			this.repository = repository ?? throw new ArgumentNullException (nameof (repository));
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"type-details", ParseChildElement_TypeDetails},
				{"revision", ParseChildElement_Revision},
				{"display-name", ParseChildElement_DisplayName},
				{"uses-license", ParseChildElement_UsesLicense},
				{"channelRef", ParseChildElement_ChannelRef},
				{"dependencies", ParseChildElement_Dependencies},
				{"archives", ParseChildElement_Archives},
			};
		}

		protected override void Parsed ()
		{
			base.Parsed ();

			Package = new RemotePackage (repository, Context.CurrentManifestURL, Element.GetLineInfo (), ErrorHandler, Archives) {
				ChannelID = ChannelID,
				Dependencies = Dependencies,
				DisplayName = DisplayName,
				FileSystemPath = FileSystemPath,
				LicenseID = LicenseID,
				Obsolete = Obsolete,
				Path = Path,
				Revision = Revision,
				Info = Info,
				IncludeAllArchives = IncludeAllArchives
			};
			Package.Verify ();
		}

		void ParseChildElement_Archives (XElement element)
		{
			var ap = new ArchivesParser (Context, element, Namespaces);
			ap.Parse ();
			Archives = ap.Archives;
		}

		void ParseChildElement_Dependencies (XElement element)
		{
			var dp = new DependenciesParser (Context, element, Namespaces);
			dp.Parse ();
			Dependencies = dp.Dependencies;
		}

		void ParseChildElement_ChannelRef (XElement element)
		{
			ChannelID = GetRefAttributeValue (element);
		}

		void ParseChildElement_UsesLicense (XElement element)
		{
			LicenseID = GetRefAttributeValue (element);
		}

		string GetRefAttributeValue (XElement element)
		{
			return element.Attribute ("ref")?.Value ?? String.Empty;
		}

		void ParseChildElement_DisplayName (XElement element)
		{
			DisplayName = element?.Value ?? String.Empty;
		}

		void ParseChildElement_Revision (XElement element)
		{
			var rp = new RevisionParser (Context, element, Namespaces);
			rp.Parse ();
			if (rp.Revision == null) {
				ErrorHandler.Warning (Context.CurrentManifestURL, Element, "Package is missing a valid 'revision' child element");
				Revision = null;
			} else
				Revision = rp.Revision;
		}

		void ParseChildElement_TypeDetails (XElement element)
		{
			if (Info != null)
				ErrorHandler.Info (Context.CurrentManifestURL, element, "'type-details' element present more than once within this package. Will override old values.");

			Info = null;
			TypeDetailsParser td = TypeDetailsParserFactory.CreateInstance (Context, element, Namespaces);
			if (td == null) {
				ErrorHandler.Error (Context.CurrentManifestURL, Element, "Package is missing the 'type-details' child element");
				return;
			}
			td.Parse ();

			Info = td.Info;
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return new Dictionary<string, Action<XAttribute>> (StringComparer.Ordinal) {
				{"path", ParseAttribute_Path},
				{"obsolete", ParseAttribute_Obsolete},
			};
		}

		void ParseAttribute_Obsolete (XAttribute attr)
		{
			string value = attr.Value;
			if (String.IsNullOrEmpty (value))
				return;
			Obsolete = String.Compare ("true", value, StringComparison.OrdinalIgnoreCase) == 0;
		}

		void ParseAttribute_Path (XAttribute attr)
		{
			Path = attr.Value ?? String.Empty;
			FileSystemPath = Path.Replace (';', IOPath.DirectorySeparatorChar);
		}
	}
}
