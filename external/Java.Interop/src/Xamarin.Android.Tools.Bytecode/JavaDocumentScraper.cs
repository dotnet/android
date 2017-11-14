/* 
 *  Copyright (c) 2011 Xamarin Inc.
 * 
 *  Permission is hereby granted, free of charge, to any person 
 *  obtaining a copy of this software and associated documentation 
 *  files (the "Software"), to deal in the Software without restriction, 
 *  including without limitation the rights to use, copy, modify, merge, 
 *  publish, distribute, sublicense, and/or sell copies of the Software, 
 *  and to permit persons to whom the Software is furnished to do so, 
 *  subject to the following conditions:
 * 
 *  The above copyright notice and this permission notice shall be 
 *  included in all copies or substantial portions of the Software.
 * 
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
 *  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES 
 *  OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
 *  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS 
 *  BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN 
 *  ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN 
 *  CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
 *  SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Tools.Bytecode
{
	class DroidDocScraper : AndroidDocScraper
	{
		const String pattern_head_droiddoc = "<span class=\"sympad\"><a href=\".*";

		public DroidDocScraper (string dir)
			: base (dir, pattern_head_droiddoc, null, " ", false)
		{
			ShouldEscapeBrackets = true;
		}
	}
	
	class DroidDoc2Scraper : AndroidDocScraper
	{
		const String pattern_head_droiddoc = "<tr class=\"api .+\".*>.*<code>.*<a href=\".*";
		const String reset_pattern_head = "<p>";

		public DroidDoc2Scraper (string dir)
			: base (dir, pattern_head_droiddoc, reset_pattern_head, " ", true, "\\(", ", ", "\\)", "\\s*</code>")
		{
			ShouldEscapeBrackets = true;
		}

		string prev_path;
		string [] prev_contents;

		protected override IEnumerable<string> GetContentLines (string path)
		{
			if (prev_path == path)
				return prev_contents;
			else {
				prev_path = path;
				string all = File.ReadAllText (path).Replace ('\r', ' ').Replace ('\n', ' ');
				int start = all.IndexOf ("<!-- ======== START OF CLASS DATA ======== -->", StringComparison.Ordinal);
				all = start < 0 ? all : all.Substring (start);
				int end = all.IndexOf ("<!-- ========= END OF CLASS DATA ========= -->", StringComparison.Ordinal);
				all = end < 0 ? all : all.Substring (0, end);
				// <tr>...</tr> is the basic structure so </tr> is used as the end of member, but we also use <p> here.
				// Sometimes another </code> can appear after "</code>" (for the end of context member) and that messes regex match.
				// So, with any <p>, we interrupt consecutive matches.
				prev_contents = all.Split (new string [] { "<p>", "</tr>" }, StringSplitOptions.RemoveEmptyEntries);
				return prev_contents;
			}
		}

		protected override bool ShouldResetMatchBuffer (string text)
		{
			return true;
		}
	}

	class JavaDocScraper : AndroidDocScraper
	{
		const String pattern_head_javadoc = "<TD><CODE><B><A HREF=\"[./]*"; // I'm not sure how path could be specified... (./ , ../ , or even /)
		const String reset_pattern_head_javadoc = "<TD><CODE>";
		const String parameter_pair_splitter_javadoc = "&nbsp;";
	
		public JavaDocScraper (string dir)
			: base (dir, pattern_head_javadoc, reset_pattern_head_javadoc, parameter_pair_splitter_javadoc, false)
		{
		}
	}
	
	class Java7DocScraper : AndroidDocScraper
	{
		const String pattern_head_javadoc = "<td class=\"col.+\"><code><strong><a href=\"[./]*"; // I'm not sure how path could be specified... (./ , ../ , or even /)
		const String reset_pattern_head_javadoc = "<td><code>";
		const String parameter_pair_splitter_javadoc = "&nbsp;";
	
		public Java7DocScraper (string dir)
			: base (dir, pattern_head_javadoc, reset_pattern_head_javadoc, parameter_pair_splitter_javadoc, true)
		{
		}
	}

	class Java8DocScraper : AndroidDocScraper
	{
		const String pattern_head_javadoc = "<td class=\"col.+\"><code><span class=\"memberNameLink\"><a href=\"[./]*"; // I'm not sure how path could be specified... (./ , ../ , or even /)
		const String reset_pattern_head_javadoc = "<td><code>";
		const String parameter_pair_splitter_javadoc = "&nbsp;";

		public Java8DocScraper (string dir)
			: base (dir, pattern_head_javadoc, reset_pattern_head_javadoc, parameter_pair_splitter_javadoc, true, "\\-", "\\-", "\\-", null)
		{
			ShouldAlterArraySpec = true;
			ShouldEliminateGenericArguments = true;
		}

		protected override string StripTagsFromParameters (string value)
		{
			// Java8 javadoc contains possibly linked types with <a> tags, so remove all of them.
			while (value.IndexOf ('<') >= 0 && value.IndexOf ('>') > value.IndexOf ('<'))
				value = value.Substring (0, value.IndexOf ('<')) + value.Substring (value.IndexOf ('>') + 1);
			return value;
		}
	}

	public abstract class AndroidDocScraper : IJavaMethodParameterNameProvider
	{
		readonly String pattern_head;
		readonly String reset_pattern_head;
		readonly string [] parameter_pair_splitter;
		readonly bool continuous_param_lines;
		readonly String open_method;
		readonly String param_sep;
		readonly String close_method;	
		readonly String post_close_method_parens;
		string root;

		protected AndroidDocScraper (string dir, String patternHead, String resetPatternHead, String parameterPairSplitter, bool continuousParamLines)
			: this (dir, patternHead, resetPatternHead, parameterPairSplitter, continuousParamLines, "\\(", ", ", "\\)", null)
		{
		}

		protected AndroidDocScraper (string dir, String patternHead, String resetPatternHead, String parameterPairSplitter, bool continuousParamLines, string openMethod, string paramSep, string closeMethod, string postCloseMethodParens)
		{
			if (dir == null)
				throw new ArgumentNullException ("dir");
	
			pattern_head = patternHead;
			reset_pattern_head = resetPatternHead;
			parameter_pair_splitter = new string [] { (parameterPairSplitter != null ? parameterPairSplitter : "\\s+") };
			continuous_param_lines = continuousParamLines;
			open_method = openMethod;
			param_sep = paramSep;
			close_method = closeMethod;
			post_close_method_parens = postCloseMethodParens ?? string.Empty;
			if (!Directory.Exists (dir))
				throw new Exception ("Directory '" + dir + "' does not exist");
	
			root = dir;
	
			if (!File.Exists (Path.Combine (dir, "package-list")) && !File.Exists (Path.Combine (dir, "packages.html")))
				throw new ArgumentException ("Directory '" + dir + "' does not appear to be an android doc reference directory.");
			
			//foreach (var f in Directory.GetFiles (dir, "*.html", SearchOption.AllDirectories))
			//	LoadDocument (f.Substring (dir.Length + 1), f);
		}

		protected bool ShouldEscapeBrackets { get; set; }
		protected bool ShouldAlterArraySpec { get; set; }
		protected bool ShouldEliminateGenericArguments { get; set; }

		protected virtual IEnumerable<string> GetContentLines (string path)
		{
			return File.ReadAllText (path).Split ('\n');
		}

		protected virtual bool ShouldResetMatchBuffer (string text)
		{
			// sometimes we get incomplete tag, so cache it until it gets complete or matched.
			// I *know* this is a hack.
			return reset_pattern_head == null || text.EndsWith (">", StringComparison.Ordinal) || !continuous_param_lines && !text.StartsWith (reset_pattern_head, StringComparison.Ordinal);
		}

		protected virtual string StripTagsFromParameters (string value)
		{
			return value;
		}
	
		public virtual String[] GetParameterNames (string package, string type, string method, string[] ptypes, bool isVarArgs)
		{
			string path = package.Replace ('.', '/') + '/' + type.Replace ('$', '.') + ".html";
			string file = Path.Combine (root, path);
			if (!File.Exists (file)) {
				Log.Warning (1,"Warning: no document found : " + file);
				return null;
			}
	
			var buffer = new StringBuilder ();
			buffer.Append (pattern_head);
			buffer.Append (path);
			buffer.Append ("#");
			buffer.Append (method);
			buffer.Append (open_method);
			for (int i = 0; i < ptypes.Length; i++) {
				if (i != 0)
					buffer.Append (param_sep);
				var ptype = ptypes [i];
				if (ShouldEliminateGenericArguments)
					while (ptype.IndexOf ('<') > 0 && ptype.IndexOf ('>') > ptype.IndexOf ('<'))
						ptype = ptype.Substring (0, ptype.IndexOf ('<')) + ptype.Substring (ptype.IndexOf ('>') + 1);
				buffer.Append (ptype.Replace ('$', '.'));
			}
			if (ShouldEscapeBrackets)
				buffer.Replace ("[", "\\[").Replace ("]", "\\]");
			if (ShouldAlterArraySpec)
				buffer.Replace ("[]", ":A");
			buffer.Append (close_method);
			buffer.Append ("\".*\\(([^\\(\\)]*)\\)");
			buffer.Append (post_close_method_parens);
			buffer.Replace ("?", "\\?");
			Regex pattern = new Regex (buffer.ToString (), RegexOptions.Multiline);
	
			try {
				String text = "";
				String prev = null;
				foreach (var _text in GetContentLines (file)) {
					text = _text.TrimEnd ('\r');
					if (prev != null)
						prev = text = prev + text;
					var matcher = pattern.Match (text);
					if (matcher.Success) {
						var plist = matcher.Groups [1];
						String[] parms = StripTagsFromParameters (plist.Value).Split (new string [] { ", " }, StringSplitOptions.RemoveEmptyEntries);
						if (parms.Length != ptypes.Length) {
							Log.Warning (1, "failed matching {0} (expected {1} params, got {2} params)", buffer, ptypes.Length, parms.Length);
							return null;
						}
						String[] result = new String [ptypes.Length];
						for (int i = 0; i < ptypes.Length; i++) {
							String[] toks = parms [i].Split (parameter_pair_splitter, StringSplitOptions.RemoveEmptyEntries);
							result [i] = toks [toks.Length - 1];
						}
						return result;
					}
					if (ShouldResetMatchBuffer (text))
						prev = null;
					else
						prev = text;
				}
			} catch (Exception e) {
				Log.Error ("ERROR in {0}.{1}: {2}", type, method, e);
				return null;
			}
	
			Log.Warning (1, "Warning : no match for {0}.{1} (rex: {2})", type, method, buffer);
			return null;
		}
		
		static Dictionary<String,List<String>> deprecatedFields;
		static Dictionary<String,List<String>> deprecatedMethods;
		
		public static void LoadXml (String filename)
		{
			try {
				var doc = XDocument.Load (filename);
				deprecatedFields = new Dictionary<String,List<String>> ();
				deprecatedMethods = new Dictionary<String,List<String>> ();
				var files = doc.Root.Descendants ("file");
				foreach (var file in files) {
					var f = new List<String> ();
					deprecatedFields [file.Attribute ("name").Value] = f;
					var fields = file.Descendants ("field");
					foreach (var fld in fields)
						f.Add (fld.Value);
	
					var m = new List<String> ();
					deprecatedMethods [file.Attribute ("name").Value] = m;
					var methods = file.Descendants ("method");
					foreach (var meh in methods)
						m.Add (meh.Value);
				}
			
			} catch (Exception ex) {
				Log.Error ("Annotations parser error: " + ex);
			}
		}
	}

	public interface IJavaMethodParameterNameProvider
	{
		String[] GetParameterNames (string package, string type, string method, string[] ptypes, bool isVarArgs);
	}

	public static class JavaMethodParameterNameProvider {

		public static JavaDocletType GetDocletType (string path)
		{
			var kind = JavaDocletType.DroidDoc;
			char[] buf = new char[500];

			string packagesHtml = Path.Combine (path, "packages.html");
			if (File.Exists (packagesHtml) && File.ReadAllText (packagesHtml).Contains ("<body class=\"gc-documentation develop reference api "))
				kind = JavaDocletType.DroidDoc2;

			string indexHtml = Path.Combine (path, "index.html");
			if (File.Exists (indexHtml)) {
				using (var reader = File.OpenText (indexHtml))
					reader.ReadBlock (buf, 0, buf.Length);
				string rawHTML = new string (buf);
				if (rawHTML.Contains ("Generated by javadoc (build 1.6"))
					kind = JavaDocletType.Java6;
				else if (rawHTML.Contains ("Generated by javadoc (version 1.7"))
					kind = JavaDocletType.Java7;
				else if (rawHTML.Contains ("Generated by javadoc (1.8"))
					kind = JavaDocletType.Java8;
			}

			// Check to see if it's an api.xml formatted doc
			if (File.Exists (path)) {
				string rawXML = null;
				using (var reader = File.OpenText (path)) {
					int len = reader.ReadBlock (buf, 0, buf.Length);
					rawXML = new string (buf, 0, len).Trim ();
				}
				if (rawXML.Contains ("<api>") && rawXML.Contains ("<package"))
					kind = JavaDocletType._ApiXml;
				else if (rawXML.StartsWith ("package", StringComparison.Ordinal) ||
						rawXML.StartsWith (";", StringComparison.Ordinal)) {
					kind = JavaDocletType.JavaApiParameterNamesXml;
				}
			}

			return kind;
		}
	}

	public class ApiXmlDocScraper : IJavaMethodParameterNameProvider
	{
		public ApiXmlDocScraper (string apiXmlFile)
		{
			xdoc = XDocument.Load (apiXmlFile);
		}

		XDocument xdoc;

		public string[] GetParameterNames (string package, string type, string method, string[] ptypes, bool isVarArgs)
		{
			var methodOrCtor = method == "constructor" ?
				"constructor[" : $"method[@name='{method}'";

			var pcount = ptypes.Length;

			var xpath = new StringBuilder ();

			xpath.Append ($"/api/package[@name='{package}']/*[self::class or self::interface]/");

			if (method == "constructor")
				xpath.Append ("constructor[");
			else
				xpath.Append ($"method[@name='{method}'");

			xpath.Append ($" and count(parameter)={pcount}");

			if (pcount > 0) {
				xpath.Append (" and ");
				xpath.Append (string.Join (" and ", ptypes.Select ((pt, pindex) => $"parameter[{pindex + 1}][@type='{pt}']")));
			}

			xpath.Append ("]");

			var methodElem = xdoc.XPathSelectElement (xpath.ToString ());

			if (methodElem != null)
				return methodElem.Elements ("parameter").Select (pe => pe.Attribute ("name")?.Value).ToArray ();

			return new string[0];
		}
	}
}
