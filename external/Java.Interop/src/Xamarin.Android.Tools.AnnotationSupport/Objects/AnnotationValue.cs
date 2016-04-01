using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public class AnnotationValue : AnnotationObject
	{
		public AnnotationValue (XElement e)
		{
			var an = e.Attribute ("name");
			Name = an == null ? null : an.Value;
			var av = e.Attribute ("val");
			Val = av == null ? null : av.Value;
			if (!string.IsNullOrWhiteSpace (Val) && Val [0] == '{') {
				ValueAsArray = Val.Substring (1, Val.Length - 2).Split (',').Select (s => s.Trim ()).ToArray ();
				if (ValueAsArray.Count > 0) {
					var prefix = ValueAsArray [0];
					int idx = prefix.LastIndexOf ('.');
					if (idx > 0) {
						prefix = prefix.Substring (0, idx);
						if (ValueAsArray.All (s => s.StartsWith (prefix, StringComparison.Ordinal))) {
							ArrayItemCommonPrefix = prefix;
							ValueAsArray = ValueAsArray.Select (s => s.Substring (prefix.Length + 1)).ToArray ();
						}
					}
				}
			}
		}
		
		public string Name { get; set; }
		public string Val { get; set; }
		public IList<string> ValueAsArray { get; private set; }
		public string ArrayItemCommonPrefix { get; set; }

		public override string ToString ()
		{
			if (ArrayItemCommonPrefix == null)
				return Name + " = " + Val;
			return string.Format ("{0} = {1}[{2}]", Name, ArrayItemCommonPrefix, string.Join (",", ValueAsArray));
		}
	}
}
