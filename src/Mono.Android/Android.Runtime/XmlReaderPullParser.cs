using System;
using System.IO;
using System.Text;
using Android.Content.Res;
using System.Xml;
using Org.XmlPull.V1;

namespace Android.Runtime
{
	public class XmlReaderResourceParser : XmlReaderPullParser, IXmlResourceParser
	{
		public static IntPtr ToLocalJniHandle (XmlReader? value)
		{
			if (value == null)
				return IntPtr.Zero;

			var xpr = value as XmlResourceParserReader;
			if (xpr != null)
				return JNIEnv.NewLocalRef (xpr.Handle);
			return JNIEnv.ToLocalJniHandle (new Android.Runtime.XmlReaderResourceParser (value));
		}

		XmlReader r;
		
		public XmlReaderResourceParser (XmlReader r)
			: base (r)
		{
			this.r = r;
		}
	
		#region IXmlResourceParser implementation
		public void Close ()
		{
			r.Close ();
		}
		#endregion
	
		#region IAttributeSet implementation
		public bool GetAttributeBooleanValue (int index, bool defaultValue)
		{
			return index < AttributeCount ? XmlConvert.ToBoolean (GetAttributeValue (index)) : defaultValue;
		}
	
		public bool GetAttributeBooleanValue (string? namespaceURI, string? attribute, bool defaultValue)
		{
			var v = GetAttributeValue (namespaceURI, attribute);
			return v != null ? XmlConvert.ToBoolean (v) : defaultValue;
		}
	
		public float GetAttributeFloatValue (int index, float defaultValue)
		{
			return index < AttributeCount ? XmlConvert.ToSingle (GetAttributeValue (index)) : defaultValue;
		}
	
		public float GetAttributeFloatValue (string? namespaceURI, string? attribute, float defaultValue)
		{
			var v = GetAttributeValue (namespaceURI, attribute);
			return v != null ? XmlConvert.ToSingle (v) : defaultValue;
		}
	
		public int GetAttributeIntValue (int index, int defaultValue)
		{
			return index < AttributeCount ? XmlConvert.ToInt32 (GetAttributeValue (index)) : defaultValue;
		}
	
		public int GetAttributeIntValue (string? namespaceURI, string? attribute, int defaultValue)
		{
			var v = GetAttributeValue (namespaceURI, attribute);
			return v != null ? XmlConvert.ToInt32 (v) : defaultValue;
		}
	
		public int GetAttributeListValue (int index, string[]? options, int defaultValue)
		{
			throw new NotSupportedException ();
		}
	
		public int GetAttributeListValue (string? namespaceURI, string? attribute, string[]? options, int defaultValue)
		{
			throw new NotSupportedException ();
		}
	
		public int GetAttributeNameResource (int index)
		{
			throw new NotSupportedException ();
		}
	
		public int GetAttributeResourceValue (int index, int defaultValue)
		{
			throw new NotSupportedException ();
		}
	
		public int GetAttributeResourceValue (string? namespaceURI, string? attribute, int defaultValue)
		{
			throw new NotSupportedException ();
		}
	
		public int GetAttributeUnsignedIntValue (int index, int defaultValue)
		{
			return index < AttributeCount ? (int) XmlConvert.ToUInt32 (GetAttributeValue (index)) : defaultValue;
		}
	
		public int GetAttributeUnsignedIntValue (string? namespaceURI, string? attribute, int defaultValue)
		{
			var v = GetAttributeValue (namespaceURI, attribute);
			return v != null ? (int) XmlConvert.ToUInt32 (v) : defaultValue;
		}
	
		public int GetIdAttributeResourceValue (int defaultValue)
		{
			return GetAttributeResourceValue (null, "id", defaultValue);
		}
	
		public string? ClassAttribute {
			get { return GetAttributeValue (null, "class"); }
		}
	
		public string? IdAttribute {
			get { return GetAttributeValue (null, "id"); }
		}
	
		public int StyleAttribute {
			get { return GetAttributeResourceValue (null, "style", 0); }
		}
		#endregion
	}

	public class XmlReaderPullParser : Java.Lang.Object, IXmlPullParser
	{
		public static IntPtr ToLocalJniHandle (XmlReader? value)
		{
			if (value == null)
				return IntPtr.Zero;

			var xppr = value as XmlPullParserReader;
			if (xppr != null)
				return JNIEnv.NewLocalRef (xppr.Handle);
			return JNIEnv.ToLocalJniHandle (new Android.Runtime.XmlReaderPullParser (value));
		}
		
		XmlReader r;
		bool started;
		
		public XmlReaderPullParser (XmlReader r)
		{
			this.r = r;
		}
	
		#region IXmlPullParser implementation
		public void DefineEntityReplacementText (string? entityName, string? replacementText)
		{
			throw new NotSupportedException ();
		}
	
		public string GetAttributeName (int index)
		{
			r.MoveToAttribute (index);
			return r.LocalName;
		}
	
		public string GetAttributeNamespace (int index)
		{
			r.MoveToAttribute (index);
			return r.NamespaceURI;
		}
	
		public string GetAttributePrefix (int index)
		{
			r.MoveToAttribute (index);
			return r.Prefix;
		}
	
		public string GetAttributeType (int index)
		{
			throw new NotSupportedException ();
		}
	
		public string GetAttributeValue (int index)
		{
			return r.GetAttribute (index);
		}
	
		public string? GetAttributeValue (string? namespaceURI, string? name)
		{
			if (name is null)
				return null;

			return r.GetAttribute (name, namespaceURI);
		}
	
		public bool GetFeature (string? name)
		{
			switch (name) {
			case XmlPullParser.FeatureProcessNamespaces:
			case XmlPullParser.FeatureReportNamespaceAttributes:
				return true;
			}
			return false;
		}
	
		public string? GetNamespace (string prefix)
		{
			return r.LookupNamespace (prefix);
		}
	
		public int GetNamespaceCount (int depth)
		{
			throw new NotSupportedException ();
		}
	
		public string GetNamespacePrefix (int pos)
		{
			throw new NotSupportedException ();
		}
	
		public string GetNamespaceUri (int pos)
		{
			throw new NotSupportedException ();
		}
	
		public Java.Lang.Object GetProperty (string? name)
		{
			throw new NotSupportedException ();
		}
	
		public char[] GetTextCharacters (int[] holderForStartAndLength)
		{
			throw new NotSupportedException ();
		}
	
		public bool IsAttributeDefault (int index)
		{
			r.MoveToAttribute (index);
			return r.IsDefault;
		}
		
		bool on_empty_end_element;
		
		public XmlPullParserNode Next ()
		{
			do {
				var n = NextToken ();
				switch (n) {
				case XmlPullParserNode.StartTag:
				case XmlPullParserNode.EndTag:
				case XmlPullParserNode.Text:
				case XmlPullParserNode.EndDocument:
					return n;
				}
			} while (true);
		}
	
		public XmlPullParserNode NextTag ()
		{
			var eventType = Next ();
			if (eventType == XmlPullParserNode.Text && IsWhitespace) {
				eventType = Next ();
			}
			switch (eventType) {
			case XmlPullParserNode.StartTag:
			case XmlPullParserNode.EndTag:
				return eventType;
			}
			throw new XmlPullParserException ("expected start or end tag", r, null);
		}
	
		public string NextText ()
		{
			if (EventType != XmlPullParserNode.StartTag)
				throw new XmlPullParserException ("parser must be on START_TAG to read next text", r, null);
			var eventType = Next ();
			if (eventType == XmlPullParserNode.Text) {
				string result = Text;
				eventType = Next ();
				if (eventType != XmlPullParserNode.EndTag)
					throw new XmlPullParserException ("event TEXT it must be immediately followed by END_TAG", r, null);
				return result;
			}
			else if (eventType == XmlPullParserNode.EndTag)
				return "";
			throw new XmlPullParserException ("parser must be on START_TAG or TEXT to read text", r, null);
		}
	
		public XmlPullParserNode NextToken ()
		{
			if (EventType == XmlPullParserNode.EndDocument)
				throw new XmlPullParserException ("Attempt to get next token after EndDocument event is invalid.");

			if (r.IsEmptyElement) {
				if (!on_empty_end_element) {
					on_empty_end_element = true;
					return XmlPullParserNode.EndTag;
				}
				on_empty_end_element = false;
			}
			
			if (!started) {
				started = true;
				return XmlPullParserNode.StartDocument; // StartDocument
			}
			
			while (r.Read ()) {
				switch (r.NodeType) {
				case XmlNodeType.CDATA:
					return XmlPullParserNode.Cdsect;
				case XmlNodeType.Text:
				case XmlNodeType.SignificantWhitespace:
					return XmlPullParserNode.Text;
				case XmlNodeType.Whitespace:
					return XmlPullParserNode.IgnorableWhitespace;
				case XmlNodeType.Comment:
					return XmlPullParserNode.Comment;
				case XmlNodeType.Element:
					return XmlPullParserNode.StartTag;
				case XmlNodeType.EndElement:
					return XmlPullParserNode.EndTag;
				case XmlNodeType.EntityReference:
					return XmlPullParserNode.EntityRef;
				case XmlNodeType.ProcessingInstruction:
					return XmlPullParserNode.ProcessingInstruction;
				case XmlNodeType.XmlDeclaration:
				case XmlNodeType.DocumentType:
				case XmlNodeType.EndEntity:
					continue;
				default:
					throw new NotSupportedException ();
				}
			}
			return XmlPullParserNode.EndDocument;
		}
	
		public void Require (Org.XmlPull.V1.XmlPullParserNode type, string? namespaceURI, string? name)
		{
			if (type != EventType || namespaceURI != this.Namespace || name != this.Name)
				throw new XmlPullParserException( "expected " + type + " " + PositionDescription);
		}
	
		public void SetFeature (string? name, bool state)
		{
			switch (name) {
			case XmlPullParser.FeatureProcessNamespaces:
			case XmlPullParser.FeatureReportNamespaceAttributes:
				if (state)
					return;
				break;
			}
			throw new NotSupportedException ();
		}

		string? input_encoding;
		
		public void SetInput (Stream? inputStream, string? inputEncoding)
		{
			r = XmlReader.Create (new StreamReader (inputStream!, Encoding.GetEncoding (inputEncoding!)));
			r.Read ();
			input_encoding = inputEncoding;
		}
	
		public void SetInput (Java.IO.Reader? input)
		{
			throw new System.NotSupportedException ();
		}
	
		public void SetProperty (string? name, Java.Lang.Object? value)
		{
			throw new System.NotSupportedException ();
		}
	
		public int AttributeCount {
			get { return r.AttributeCount; }
		}
	
		public int ColumnNumber {
			get {
				var xi = r as IXmlLineInfo;
				// XPP column is zero-based
				return xi != null && xi.HasLineInfo () ? xi.LinePosition - 1 : -1;
			}
		}
	
		public int Depth {
			get {
				r.MoveToElement ();
				return r.Depth;
			}
		}
	
		public Org.XmlPull.V1.XmlPullParserNode EventType {
			get {
				if (on_empty_end_element)
					return XmlPullParserNode.EndTag;
				if (r.EOF) // I believe this is unnecessary, but it somehow returns StartDocument at the end of doc...
					return XmlPullParserNode.EndDocument;
				if (!started || r.ReadState == ReadState.Initial)
					return XmlPullParserNode.StartDocument;
				r.MoveToElement ();
				switch (r.NodeType) {
				case XmlNodeType.CDATA:
					return XmlPullParserNode.Cdsect;
				case XmlNodeType.Comment:
					return XmlPullParserNode.Comment;
				case XmlNodeType.Element:
					return XmlPullParserNode.StartTag; // empty element EndTag is covered above.
				case XmlNodeType.EndElement:
					return XmlPullParserNode.EndTag;
				case XmlNodeType.EntityReference:
					return XmlPullParserNode.EntityRef;
				case XmlNodeType.ProcessingInstruction:
					return XmlPullParserNode.ProcessingInstruction;
				case XmlNodeType.SignificantWhitespace:
				case XmlNodeType.Text:
					return XmlPullParserNode.Text;
				case XmlNodeType.Whitespace:
					return XmlPullParserNode.IgnorableWhitespace;
				default:
					if (r.ReadState == ReadState.EndOfFile)
						return XmlPullParserNode.EndDocument;
					throw new InvalidOperationException ();
				}
			}
		}
	
		public string? InputEncoding {
			get { return input_encoding; }
		}
	
		public bool IsEmptyElementTag {
			get {
				r.MoveToElement ();
				return r.IsEmptyElement;
			}
		}
	
		public bool IsWhitespace {
			get { return r.NodeType == XmlNodeType.Whitespace; }
		}
	
		public int LineNumber {
			get {
				var xi = r as IXmlLineInfo;
				// XPP line is zero-based
				return xi != null && xi.HasLineInfo () ? xi.LineNumber - 1 : -1;
			}
		}
	
		public string Name {
			get {
				r.MoveToElement ();
				return r.LocalName;
			}
		}
	
		public string Namespace {
			get {
				r.MoveToElement ();
				return r.NamespaceURI;
			}
		}
	
		public string PositionDescription {
			get {
				r.MoveToElement ();
				var xi = r as IXmlLineInfo;
				var loc = xi == null || !xi.HasLineInfo () ?
					"(location N/A)" :
					FormattableString.Invariant ($"({xi.LineNumber}, {xi.LinePosition})");
				var uri = string.IsNullOrEmpty (r.BaseURI) ? null : r.BaseURI;
				return FormattableString.Invariant ($"Node {r.NodeType} at {uri} {loc}");
			}
		}
	
		public string Prefix {
			get {
				r.MoveToElement ();
				return r.Prefix;
			}
		}
	
		public string Text {
			get {
				r.MoveToElement ();
				return r.Value;
			}
		}
		#endregion
	}
}

