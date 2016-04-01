using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public class AnnotationData : AnnotationObject
	{
		public AnnotationData (XElement e)
		{
			var a = e.Attribute ("name");
			Name = a == null ? null : a.Value;
			string predef = "android.support.annotation.";
			if (Name.StartsWith (predef, StringComparison.Ordinal))
				Name = Name.Substring (predef.Length);
			Values = e.Elements ("val").Select (c => new AnnotationValue (c)).ToArray ();
		}
		
		public string Name { get; set; }
		public IList<AnnotationValue> Values { get; private set; }
	}
}
