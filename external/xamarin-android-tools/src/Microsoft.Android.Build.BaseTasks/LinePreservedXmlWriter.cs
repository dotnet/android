// https://github.com/xamarin/xamarin-android/blob/2aea0af1da5c46924dd00587701b6c91391d62f8/src/Xamarin.Android.Build.Tasks/Utilities/LinePreservedXmlWriter.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;

namespace Microsoft.Android.Build.Tasks
{
	public class LinePreservedXmlWriter : XmlTextWriter
	{
		LinePreservedTextWriter tw;

		static LinePreservedTextWriter GetLinePreservedTextWriter (TextWriter w)
		{
			var tw = w as LinePreservedTextWriter;
			if (tw == null)
				tw = new LinePreservedTextWriter (w);
			return tw;
		}
		
		public LinePreservedXmlWriter (TextWriter w)
			: this (GetLinePreservedTextWriter (w))
		{
		}
		
		internal LinePreservedXmlWriter (LinePreservedTextWriter w)
			: base (w)
		{
			this.tw = w;
		}
		
		XPathNavigator nav;
		
		public override void WriteNode (XPathNavigator navigator, bool defattr)
		{
			XPathNavigator bak = nav;
			this.nav = navigator;
			IXmlLineInfo li = navigator as IXmlLineInfo;
			if (li != null)
				tw.ProceedTo (li.LineNumber, li.LinePosition);
			base.WriteNode (navigator, defattr);
			this.nav = bak;
		}
		
		public override void WriteStartAttribute (string prefix, string localName, string namespaceUri)
		{
			if (nav != null)
				Proceed (nav as IXmlLineInfo);
			base.WriteStartAttribute (prefix, localName, namespaceUri);
		}
		
		public override void WriteStartElement (string prefix, string localName, string namespaceUri)
		{
			if (nav != null)
				Proceed (nav as IXmlLineInfo);
			base.WriteStartElement (prefix, localName, namespaceUri);
		}
		
		void Proceed (IXmlLineInfo li)
		{
			if (li == null || !li.HasLineInfo ())
				return;
			tw.ProceedTo (li.LineNumber, li.LinePosition);
		}
	}
	
	class LinePreservedTextWriter : TextWriter
	{
		TextWriter w;
		int line = 1;

		public LinePreservedTextWriter (TextWriter w)
		{
			this.w = w;
		}
		
		public override System.Text.Encoding Encoding {
			get { return Encoding.Unicode; }
		}
		
		public void ProceedTo (int line, int column)
		{
			if (line <= 0)
				return;
			bool wrote = this.line < line;
			while (this.line < line)
				WriteLine ();
			if (wrote)
				Write (new string (' ', column));
		}
		
		public override void Close ()
		{
			w.Close ();
		}

		public override void Flush ()
		{
			w.Flush ();
		}
		
		public override void Write (char value)
		{
			w.Write (value);
			if (value == '\n')
				line++;
		}
		
		public override void Write (char[] buffer, int index, int count)
		{
			w.Write (buffer, index, count);
			int next = index;
			while (next < index + count) {
				int idx = Array.IndexOf<char> (buffer, '\n', next, count + (index - next));
				if (idx < 0)
					break;
				line++;
				next = idx + 1;
			}
		}
		
		public override void Write (string value)
		{
			w.Write (value);
			int next = 0;
			while (next < value.Length) {
				int idx = value.IndexOf ('\n', next);
				if (idx < 0)
					break;
				line++;
				next = idx + 1;
			}
		}
		
		public override void WriteLine ()
		{
			w.WriteLine ();
			line++;
		}
	}
}

