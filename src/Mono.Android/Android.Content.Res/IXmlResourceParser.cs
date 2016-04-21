using System;
using System.Collections.Generic;
using Android.Runtime;
using Org.XmlPull.V1;

namespace Android.Content.Res
{
	public partial interface IXmlResourceParser : Android.Util.IAttributeSet, IXmlPullParser
	{
		#region These members are dare defined here to avoid conflicts between IAttributeSet and IXmlPullParser.

		int AttributeCount { get; }
		
		string PositionDescription { get; }
		
		string GetAttributeName (int pos);
		string GetAttributeValue (int pos);
		string GetAttributeValue (string ns, string name);
		#endregion
	}
}

