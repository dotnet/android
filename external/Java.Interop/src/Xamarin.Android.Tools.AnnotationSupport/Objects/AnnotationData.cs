using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public class AnnotationData : AnnotationObject
	{
		static readonly string[] Prefixes = new[] {
			"android.support.annotation.",
			"androidx.annotation.",
		};

		public AnnotationData (XElement e)
		{
			var a = e.Attribute ("name");
			Name = a == null ? null : a.Value;
			foreach (var predef in Prefixes) {
				if (!Name.StartsWith (predef, StringComparison.Ordinal))
					continue;
				Name = Name.Substring (predef.Length);
				break;
			}
			Values = e.Elements ("val").Select (c => new AnnotationValue (c)).ToArray ();
		}
		
		public string Name { get; set; }
		public IList<AnnotationValue> Values { get; private set; }
	}
}
