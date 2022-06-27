using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Irony.Parsing;

using Java.Interop.Tools.JavaSource;

namespace MonoDroid.Generation
{
	enum ApiLinkStyle {
		None,
		DeveloperAndroidComReference_2020Nov,
	}

	public sealed class JavadocInfo {

		public  string          Javadoc             { get; set; }

		public  XElement[]      ExtraRemarks        { get; set; }

		public  XElement[]      Copyright           { get; set; }

		public  XmldocStyle     XmldocStyle         { get; set; }
		public  string          DocRootReplacement  { get; set; }

		string  MemberDescription;

		public static JavadocInfo CreateInfo (XElement element, XmldocStyle style, bool appendCopyrightExtra = true)
		{
			if (element == null) {
				return null;
			}

			string javadoc                  = element.Element ("javadoc")?.Value;

			var desc                        = GetMemberDescription (element);
			string declaringJniType         = desc.DeclaringJniType;
			string declaringMemberName      = desc.DeclaringMemberName;
			var declaringMemberParamString  = desc.DeclaringMemberParameterString;

			var extras                      = GetExtra (element, style, declaringJniType, declaringMemberName, declaringMemberParamString, appendCopyrightExtra);
			XElement[] extra                = extras.Extras;
			XElement[] copyright            = extras.Copyright;
			string docRoot                  = extras.DocRoot;

			if (string.IsNullOrEmpty (javadoc) && extra == null)
				return null;

			var info = new JavadocInfo () {
				ExtraRemarks        = extra,
				Copyright           = copyright,
				DocRootReplacement  = docRoot,
				Javadoc             = javadoc,
				MemberDescription   = declaringMemberName == null
					? declaringJniType
					: $"{declaringJniType}.{declaringMemberName}{declaringMemberParamString}",
				XmldocStyle         = style,
			};
			return info;
		}

		static (string DeclaringJniType, string DeclaringMemberName, string DeclaringMemberParameterString) GetMemberDescription (XElement element)
		{
			bool isType     = element.Name.LocalName == "class" ||
				element.Name.LocalName == "interface";

			string declaringJniType             = isType
				? (string) element.Attribute ("jni-signature")
				: (string) element.Parent.Attribute ("jni-signature");
			if (declaringJniType.StartsWith ("L", StringComparison.Ordinal) &&
					declaringJniType.EndsWith (";", StringComparison.Ordinal)) {
				declaringJniType = declaringJniType.Substring (1, declaringJniType.Length-2);
			}

			string declaringMemberName          = isType
				? null
				: (string) element.Attribute ("name") ?? declaringJniType.Substring (declaringJniType.LastIndexOf ('/')+1);

			string declaringMemberJniSignature  = isType
				? null
				: (string) element.Attribute ("jni-signature");


			string declaringMemberParameterString = null;
			if (!isType && (declaringMemberJniSignature?.StartsWith ("(", StringComparison.Ordinal) ?? false)) {
				var parameterTypes = element.Elements ("parameter")?.Select (e => e.Attribute ("type")?.Value)?.ToList ();
				if (parameterTypes?.Any () ?? false) {
					declaringMemberParameterString = $"({string.Join (", ", parameterTypes)})";
				} else {
					declaringMemberParameterString = "()";
				}
			}

			return (declaringJniType, declaringMemberName, declaringMemberParameterString);
		}

		static (XElement[] Extras, XElement[] Copyright, string DocRoot) GetExtra (XElement element, XmldocStyle style, string declaringJniType, string declaringMemberName, string declaringMemberParameterString, bool appendCopyrightExtra)
		{
			if (!style.HasFlag (XmldocStyle.IntelliSenseAndExtraRemarks))
				return (null, null, null);

			XElement javadocMetadata    = null;
			while (element != null) {
				javadocMetadata = element.Element ("javadoc-metadata");
				if (javadocMetadata != null) {
					break;
				}
				element         = element.Parent;
			}

			List<XElement>  extra   = null;
			IEnumerable<XElement>  copyright = null;
			string docRoot = null;
			if (javadocMetadata != null) {
				var link            = javadocMetadata.Element ("link");
				var urlPrefix       = (string) link.Attribute ("prefix");
				var linkStyle       = (string) link.Attribute ("style");
				docRoot             = (string) link.Attribute ("docroot");
				var kind            = ParseApiLinkStyle (linkStyle);

				XElement docLink	= null;
				if (!string.IsNullOrEmpty (urlPrefix)) {
					docLink         = CreateDocLinkUrl (kind, urlPrefix, declaringJniType, declaringMemberName, declaringMemberParameterString);
				}
				extra           = new List<XElement> ();
				extra.Add (docLink);
				copyright = javadocMetadata.Element ("copyright").Elements ();
				if (appendCopyrightExtra) {
					extra.AddRange (copyright);
				}
			}
			return (extra?.ToArray (), copyright?.ToArray (), docRoot);
		}

		static ApiLinkStyle ParseApiLinkStyle (string style)
		{
			switch (style) {
				case "developer.android.com/reference@2020-Nov":
					return ApiLinkStyle.DeveloperAndroidComReference_2020Nov;
				default:
					return ApiLinkStyle.None;
			}
		}


		public void AddJavadocs (ICollection<string> comments)
		{
			var nodes = ParseJavadoc ();
			AddComments (comments, nodes);
		}

		public IEnumerable<XNode> ParseJavadoc ()
		{
			if (string.IsNullOrWhiteSpace (Javadoc))
				return Enumerable.Empty<XNode> ();

			Javadoc         = Javadoc.Trim ();

			ParseTree           tree    = null;
			IEnumerable<XNode>  nodes   = null;

			try {
				var parser  = new SourceJavadocToXmldocParser (new XmldocSettings {
					Style = XmldocStyle,
					ExtraRemarks = ExtraRemarks,
					DocRootValue = DocRootReplacement,
				});
				nodes       = parser.TryParse (Javadoc, fileName: null, out tree);
			}
			catch (Exception e) {
				Console.Error.WriteLine ($"## Exception translating remarks: {e.ToString ()}");
			}

			if (tree != null && tree.HasErrors ()) {
				Console.Error.WriteLine ($"## Unable to translate remarks for {MemberDescription}:");
				Console.Error.WriteLine ("```");
				Console.Error.WriteLine (Javadoc);
				Console.Error.WriteLine ("```");
				PrintMessages (tree, Console.Error);
				Console.Error.WriteLine ();
			}

			return nodes;
		}

		public static void AddComments (ICollection<string> comments, IEnumerable<XNode> nodes)
		{
			if (nodes == null)
				return;

			foreach (var node in nodes) {
				AddNode (comments, node);
			}
		}

		static void AddNode (ICollection<string> comments, XNode node)
		{
			if (node == null)
				return;
			var contents = node.ToString ();

			var lines = new StringReader (contents);
			string line;
			while ((line = lines.ReadLine ()) != null) {
				comments.Add ($"/// {line}");
			}
		}

		static void PrintMessages (ParseTree tree, TextWriter writer)
		{
			var lines   = GetLines (tree.SourceText);
			foreach (var m in tree.ParserMessages) {
				writer.WriteLine ($"JavadocImport-{m.Level} {m.Location}: {m.Message}");
				writer.WriteLine (lines [m.Location.Line]);
				writer.Write (new string (' ', m.Location.Column));
				writer.WriteLine ("^");
			}
		}

		static List<string> GetLines (string text)
		{
			var lines = new List<string>();
			var reader = new StringReader (text);
			string line;
			while ((line = reader.ReadLine()) != null) {
				lines.Add (line);
			}
			return lines;
		}

		static Dictionary<ApiLinkStyle, Func<string, string, string, string, XElement>> UrlCreators = new Dictionary<ApiLinkStyle, Func<string, string, string, string, XElement>> {
			[ApiLinkStyle.DeveloperAndroidComReference_2020Nov] = CreateAndroidDocLinkUri,
		};

		static XElement CreateDocLinkUrl (ApiLinkStyle style, string prefix, string declaringJniType, string declaringMemberName, string declaringMemberParameterString)
		{
			if (style == ApiLinkStyle.None || prefix == null || declaringJniType == null)
				return null;
			if (UrlCreators.TryGetValue (style, out var creator)) {
				return creator (prefix, declaringJniType, declaringMemberName, declaringMemberParameterString);
			}
			return null;
		}

		static XElement CreateAndroidDocLinkUri (string prefix, string declaringJniType, string declaringMemberName, string declaringMemberParameterString)
		{
			// URL is:
			//  * {prefix}
			//  * declaring type in JNI format
			//  * when `declaringJniMemberName` != null, `#{declaringJniMemberName}`
			//  * for methods & constructors, a `(`, the arguments in *Java* syntax -- separated by `, ` -- and `)`
			//
			// Example: "https://developer.android.com/reference/android/app/Application#registerOnProvideAssistDataListener(android.app.Application.OnProvideAssistDataListener)"
			// Example: "https://developer.android.com/reference/android/animation/ObjectAnimator#ofFloat(T,%20android.util.Property%3CT,%20java.lang.Float%3E,%20float...)"

			declaringJniType = declaringJniType.Replace ("$", ".");
			var java    = new StringBuilder (declaringJniType)
				.Replace ("/", ".");

			var url     = new StringBuilder (prefix);
			if (!prefix.EndsWith ("/", StringComparison.Ordinal)) {
				url.Append ("/");
			}
			url.Append (declaringJniType);

			if (declaringMemberName != null) {
				java.Append (".").Append (declaringMemberName);
				url.Append ("#").Append (declaringMemberName);
				if (declaringMemberParameterString != null) {
					java.Append (declaringMemberParameterString);
					url.Append (declaringMemberParameterString);
				}
			}
			var format  = new XElement ("format",
					new XAttribute ("type", "text/html"),
					new XElement ("a",
						new XAttribute ("href", new Uri (url.ToString ()).AbsoluteUri),
						new XAttribute ("title", "Reference documentation"),
						"Java documentation for ",
						new XElement ("code", java.ToString ()),
						"."));
			return new XElement ("para", format);
		}

	}
}
