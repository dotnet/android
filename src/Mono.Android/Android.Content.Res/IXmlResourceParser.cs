using System;
using System.Collections.Generic;
using Android.Runtime;
using Org.XmlPull.V1;

namespace Android.Content.Res
{
	public partial interface IXmlResourceParser : Android.Util.IAttributeSet, IXmlPullParser
	{

		new int AttributeCount { get; }
		
		new string PositionDescription { get; }
		
		new string GetAttributeName (int pos);
		new string GetAttributeValue (int pos);
		new string? GetAttributeValue (string? ns, string? name);
	}
}

