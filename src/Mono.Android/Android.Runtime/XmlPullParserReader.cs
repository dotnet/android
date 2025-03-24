using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using Android.Content.Res;
using Android.Runtime;
using Org.XmlPull.V1;

namespace Android.Runtime
{
	public class XmlResourceParserReader : XmlPullParserReader
	{
		IXmlResourceParser source;
		
		public XmlResourceParserReader (IJavaObject source)
			: base (source)
		{
			this.source = (IXmlResourceParser) source;
		}
		
		public override void Close ()
		{
			source.Close ();
		}

		public static XmlResourceParserReader? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			return FromNative (handle, transfer);
		}
		
		static XmlResourceParserReader? FromNative (IntPtr handle, JniHandleOwnership transfer)
		{
			var inst = Java.Lang.Object.GetObject<Android.Content.Res.IXmlResourceParser> (handle, transfer);
			if (inst is null)
				return null;
			return new XmlResourceParserReader (inst);
		}
	}
	
	public class XmlPullParserReader : XmlReader, IXmlLineInfo
	{
		const string xmlns_uri = "http://www.w3.org/2000/xmlns/";
		
		IXmlPullParser source;
		bool supports_ns, supports_ns_report_as_attr;

		XmlNamespaceManager nsmgr = new XmlNamespaceManager (new NameTable ());
		
		// Read() changes them.
		bool started;
		int ns_index = 0;
		int ns_count = 0;
		
		// MoveTo*Attribute() and ReadAttributeValue() change them.
		int attr_pos = -1;
		bool attr_value;
		
		struct QName
		{
			public QName (XmlPullParserReader r, string name)
			{
				int pos = name.IndexOf (':');
				LocalName = pos < 0 ? name : name.Substring (pos + 1);
				Namespace = pos < 0 ? String.Empty : r.LookupNamespace (name.Substring (0, pos));
			}
			
			public string LocalName;
			public string? Namespace;
		}
		
		public XmlPullParserReader (IJavaObject source)
		{
			this.source = (IXmlPullParser) source;
			supports_ns = this.source.GetFeature (XmlPullParser.FeatureProcessNamespaces);
			supports_ns_report_as_attr = this.source.GetFeature (XmlPullParser.FeatureReportNamespaceAttributes);
		}
		
		public IntPtr Handle {
			get { return source.Handle; }
		}
		
		public override int AttributeCount {
			get {
				int n = source.AttributeCount + ns_count; // could be -1
				return n > 0 ? n : 0;
			}
		}
		
		public bool HasLineInfo ()
		{
			return attr_pos < 0;
		}
		
		public int LineNumber {
			// 0-based, -1 if unknown
			get { return HasLineInfo () ? source.LineNumber + 1 : 0; }
		}
		
		public int LinePosition {
			// 0-based, -1 if unknown
			get { return HasLineInfo () ? source.ColumnNumber + 1 : 0; }
		}
		
		// Cannot support this.
		public override string BaseURI {
			get { return String.Empty; }
		}

		public override void Close ()
		{
			// we cannot do anything with IXmlPullParser. IXmlResourceParser can invoke it.
			var rp = source as IXmlResourceParser;
			if (rp != null)
				rp.Close ();
		}

		public override int Depth {
			// since it counts Document as one level, we have to decrease the number.
			get { return source.Depth - 1 + (attr_pos >= 0 ? attr_value ? 2 : 1 : 0); }
		}

		public override bool EOF {
			get { return source.EventType == XmlPullParserNode.EndDocument; }
		}

		public override string? GetAttribute (int i)
		{
			if (i < source.AttributeCount)
				return source.GetAttributeValue (i);
			else if (i < AttributeCount)
				return source.GetNamespaceUri (i - source.AttributeCount + ns_index);
			else
				throw new ArgumentOutOfRangeException ();
		}

		public override string? GetAttribute (string localName, string? namespaceName)
		{
			return namespaceName == xmlns_uri ? source.GetNamespace (localName) : source.GetAttributeValue (namespaceName, localName);
		}

		public override string? GetAttribute (string name)
		{
			var qn = new QName (this, name);
			return GetAttribute (qn.LocalName, qn.Namespace);
		}

		public override bool HasAttributes {
			get { return source.AttributeCount > 0 || ns_count > 0; }
		}

		public override bool HasValue {
			get { return attr_pos >= 0 ? true : source.Text != null; }
		}

		public override bool IsDefault {
			get { return attr_pos >= 0 && attr_pos < source.AttributeCount ? source.IsAttributeDefault (attr_pos) : false; }
		}

		public override bool IsEmptyElement {
			get { return source.IsEmptyElementTag; }
		}

		public override string? LocalName {
			get {
				if (attr_pos < 0)
					return source.Name;
				else if (attr_pos < source.AttributeCount)
					return source.GetAttributeName (attr_pos);

				var ret = source.GetNamespacePrefix (attr_pos - source.AttributeCount + ns_index);
				return String.IsNullOrEmpty (ret) ? "xmlns" : ret;
			}
		}

		public override string? LookupNamespace (string prefix)
		{
			return nsmgr.LookupNamespace (prefix);
		}

		public override void MoveToAttribute (int i)
		{
			if (i < 0 || i >= AttributeCount)
				throw new IndexOutOfRangeException ();
			attr_pos = i;
			attr_value = false;
		}

		public override bool MoveToAttribute (string localName, string? namespaceName)
		{
			if (namespaceName == xmlns_uri) {
				for (int i = 0; i < ns_count; i++)
					if (source.GetNamespacePrefix (ns_index + i) == localName) {
						attr_pos = source.AttributeCount + i;
						attr_value = false;
						return true;
					}
			} else {
				int count = source.AttributeCount;
				for (int i = 0; i < count; i++)
					if (source.GetAttributeName (i) == localName && source.GetAttributeNamespace (i) == namespaceName) {
						attr_pos = i;
						attr_value = false;
						return true;
					}
			}
			return false;
		}

		public override bool MoveToAttribute (string name)
		{
			var qn  = new QName (this, name);
			return MoveToAttribute (qn.LocalName, qn.Namespace);
		}

		public override bool MoveToElement ()
		{
			if (attr_pos >= 0) {
				attr_pos = -1;
				attr_value = false;
				return true;
			}
			return false;
		}

		public override bool MoveToFirstAttribute ()
		{
			if (source.AttributeCount == 0 && ns_count == 0)
				return false;
			attr_pos = 0;
			attr_value = false;
			return true;
		}

		public override bool MoveToNextAttribute ()
		{
			if (attr_pos + 1 >= source.AttributeCount && attr_pos + 1 - source.AttributeCount >= ns_count)
				return false;
			attr_pos++;
			attr_value = false;
			return true;
		}

		public override string? Name {
			get { return String.IsNullOrEmpty (Prefix) ? LocalName : Prefix + ':' + LocalName; }
		}

		public override XmlNameTable? NameTable {
			get { return nsmgr.NameTable; }
		}

		public override string? NamespaceURI {
			get {
				if (attr_pos < 0)
					return source.Namespace;
				else if (attr_pos < source.AttributeCount)
					return source.GetAttributeNamespace (attr_pos);
				else
					return source.GetNamespaceUri (attr_pos - source.AttributeCount + ns_index);
			}
		}

		public override XmlNodeType NodeType {
			get {
				if (attr_value)
					return XmlNodeType.Text;
				else if (attr_pos >= 0)
					return XmlNodeType.Attribute;
				
				switch (source.EventType) {
				case XmlPullParserNode.Cdsect:
					return XmlNodeType.CDATA;
				case XmlPullParserNode.Comment:
					return XmlNodeType.Comment;
				case XmlPullParserNode.Docdecl:
					return XmlNodeType.XmlDeclaration;
				case XmlPullParserNode.EndTag:
					return XmlNodeType.EndElement;
				case XmlPullParserNode.EntityRef:
					return XmlNodeType.EntityReference;
				case XmlPullParserNode.IgnorableWhitespace:
					return XmlNodeType.Whitespace;
				case XmlPullParserNode.ProcessingInstruction:
					return XmlNodeType.ProcessingInstruction;
				case XmlPullParserNode.StartTag:
					return XmlNodeType.Element;
				case XmlPullParserNode.Text:
					return source.IsWhitespace ? XmlNodeType.SignificantWhitespace : XmlNodeType.Text;
				}
				return XmlNodeType.None;
			}
		}

		public override string? Prefix {
			// getPrefix(), getAttributePrefix(), getNamespacePrefix() are not supported!!!
			get {
				if (NamespaceURI is null)
					return null;

				return nsmgr.LookupPrefix (NamespaceURI);
			}
		}

		public override bool Read ()
		{
			started = true;

			if (source.EventType == XmlPullParserNode.EndDocument)
				return false;

			MoveToElement ();
			
			// XmlPullParser emits two events (one for start of start empty element
			// and another of end empty element) while XmlReader only returns Element once.
			bool wasEmptyElement = source.IsEmptyElementTag;
			if (source.IsEmptyElementTag)
				source.NextToken ();
			source.NextToken ();
			
			if (source.EventType == XmlPullParserNode.StartDocument) // skip START_DOCUMENT
				source.NextToken ();
			if (source.EventType == XmlPullParserNode.EndDocument) // end of file
				return false;
			
			if (supports_ns && !supports_ns_report_as_attr) {
				ns_index = source.Depth == 0 ? 0 : source.GetNamespaceCount (source.Depth - 1);
				ns_count = source.GetNamespaceCount (source.Depth) - ns_index;
			}
			
			if (wasEmptyElement || NodeType == XmlNodeType.EndElement)
				nsmgr.PopScope ();
			if (NodeType == XmlNodeType.Element) {
				if (!string.IsNullOrEmpty (NamespaceURI) && nsmgr.LookupPrefix (NamespaceURI) != String.Empty)
					nsmgr.AddNamespace (String.Empty, NamespaceURI);
				else if (NamespaceURI == String.Empty && nsmgr.DefaultNamespace != String.Empty)
					nsmgr.AddNamespace (String.Empty, String.Empty);
				for (int i = 0; i < source.AttributeCount; i++) {
					string? ns = source.GetAttributeNamespace (i);
					if (!string.IsNullOrEmpty (ns) && nsmgr.LookupPrefix (ns) == null)
						nsmgr.AddNamespace ("p" + i, ns);
				}
				nsmgr.PushScope ();
			}

			// FIXME: NameTable is not filled the names appeared in the XML,
			// which could result in problems. Should we cost name lookup here?
			return true;
		}

		public override bool ReadAttributeValue ()
		{
			if (attr_pos < 0 || attr_value)
				return false;
			attr_value = true;
			return true;
		}

		public override ReadState ReadState {
			get {
				if (!started)
					return ReadState.Initial;
				if (EOF)
					return ReadState.EndOfFile;
				return ReadState.Interactive; 
			}
		}

		public override void ResolveEntity ()
		{
			throw new NotSupportedException ();
		}

		public override string? Value {
			get {
				if (attr_pos < 0)
					return source.Text;
				else if (attr_pos < source.AttributeCount)
					return source.GetAttributeValue (attr_pos);
				else
					return source.GetNamespaceUri (attr_pos - source.AttributeCount + ns_index);
			}
		}

		public static XmlReader? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			return FromNative (handle, transfer);
		}
		
		static XmlReader? FromNative (IntPtr handle, JniHandleOwnership transfer)
		{
			var inst = Java.Lang.Object.GetObject<IXmlPullParser> (handle, transfer);
			if (inst is null)
				return null;
			return new XmlPullParserReader (inst);
		}
	}
}

