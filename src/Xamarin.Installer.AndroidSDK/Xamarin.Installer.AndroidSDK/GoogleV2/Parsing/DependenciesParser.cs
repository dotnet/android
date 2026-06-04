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

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class DependenciesParser : ElementParser
	{
		List<Dependency> dependencies;

		public IList<Dependency> Dependencies { get; private set; }

		public DependenciesParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return null;
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"dependency", ParseChildElement_Dependency}
			};
		}

		void ParseChildElement_Dependency (XElement element)
		{
			var dep = new DependencyParser (Context, element, Namespaces);
			dep.Parse ();

			if (dep.Dependency == null)
				return;
			
			if (dependencies == null)
				dependencies = new List<Dependency> ();
			dependencies.Add (dep.Dependency);
		}

		protected override void Parsed ()
		{
			base.Parsed ();
			Dependencies = dependencies?.AsReadOnly ();
		}
	}
}
