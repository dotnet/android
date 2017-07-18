using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

using Android.Runtime;

using MemberInfo = Mono.Cecil.IMemberDefinition;
using MethodInfo = Mono.Cecil.MethodDefinition;

namespace Xamarin.Android.Tools.JavaDocToMdoc
{

	abstract class DocumentSection
	{
		protected DocumentSection ()
		{
		}

		public MdocHelper Mdoc { get; set; }
		public string Anchor { get; set; }
		public RegisterAttribute RegisterAttribute { get; set; }

		public virtual void UpdateSince (XElement mdRemarks)
		{
			// no document element for "since API Level" by default.
		}

		public abstract void UpdateMemberParameters (XElement mdoc);

		public abstract void UpdateMemberExceptions (XElement mdoc, bool clean);

		public abstract void UpdateMemberReturns (XElement mdoc, bool clean);

		public abstract void UpdateMemberSeeAlso (XElement mdoc, bool clean);

		public void UpdateMemberSection (MemberInfo member, RegisterAttribute tregister, XElement dest, string prefix, bool firstPass)
		{
			//XElement jdSummaryDiv = section.SummaryDiv;
			string anchor = Anchor;

			var mdRemarks = dest.XPathSelectElement ("Docs/remarks");
			if (firstPass)
				mdRemarks.Value = "";
			if (prefix != null)
				Application.AddAndroidDocUrlWithOptionalContent (mdRemarks, tregister, anchor, prefix + " method documentation", Mdoc.FromHtml (GetSummaryNodes ()));
			else {
				mdRemarks.Add (Mdoc.FromHtml (GetSummaryNodes ()));
				Application.AddAndroidDocUrlWithOptionalContent (mdRemarks, tregister, anchor);
			}
			Application.FixupMdoc (mdRemarks);
			if (firstPass) {
				UpdateSince (mdRemarks);
				Application.SetSummaryFromRemarks (dest.Element ("Docs"), prefix, firstPass);
			}
			// if method.IsSpecialName is true, then it's probably a property, in
			// which case the parameter names will NOT match.
			var method = member as MethodInfo;
			if (method != null && !method.IsSpecialName)
				UpdateMemberParameters (dest);
			UpdateMemberExceptions (dest, firstPass);
			UpdateMemberReturns (dest, firstPass);
			UpdateMemberSeeAlso (dest, firstPass);
		}

		public abstract IEnumerable<XNode> GetSummaryNodes ();

		// method parameter update common utility
		protected void UpdateParameter (string name, IEnumerable<object> contents, XElement mdoc)
		{
			if (Application.ProcessingContext.MessageLevel > 1)
				Logger.Log (LoggingVerbosity.Debug, 0, "\t\tUpdating method parameter: {0}", name);
			XElement mdp = mdoc.Element ("Docs").Elements ("param").FirstOrDefault (p => p.Attribute ("name").Value == name);
			if (mdp == null) {
				Logger.Log (LoggingVerbosity.Warning, Errors.ManagedParameterNotFound, "Could not find parameter '{0}' for type '{1}', member '{2}'.",
						   name,
						   mdoc.Parent.Parent.Attribute ("FullName").Value,
						   mdoc.Element ("MemberSignature").Attribute ("Value").Value);
				return;
			}
			mdp.Value = "";
			mdp.Add (contents);
		}
	}


	class DroidDoc2DocumentSection : DroidDocDocumentSection
	{
		public DroidDoc2DocumentSection (XElement section, XElement summaryDiv)
			: base (section, summaryDiv)
		{
			if (summaryDiv == null)
				throw new ArgumentNullException (nameof (summaryDiv));
			Section = section;
			SummaryDiv = summaryDiv;
		}

		public override IEnumerable<XNode> GetSummaryNodes ()
		{
			return SummaryDiv.Elements (XName.Get ("p"));
		}

		public override void UpdateMemberParameters (XElement mdoc)
		{
			XElement jd = Section;

			XElement jdParameters = GetJavaDocSectionTable (jd, "Parameters");
			if (jdParameters == null)
				return;

			foreach (XElement jdp in jdParameters.Elements ("tr").Where (e => e.Element ("td") != null)) {
				string name = jdp.Elements ().First ().Value.Trim ();
				if (name == "event")
					name = "e";
				var nodes = Mdoc.FromHtml (jdp.Elements ().Last ().Nodes ());

				UpdateParameter (name, nodes, mdoc);
			}
		}

		public override void UpdateMemberReturns (XElement mdoc, bool clean)
		{
			XElement jd = Section;

			XElement jdReturns = GetJavaDocSectionTable (jd, "Returns");
			if (jdReturns == null)
				return;

			XElement mdReturns = mdoc.Element ("Docs").Element ("returns") ?? mdoc.Element ("Docs").Element ("value");
			if (mdReturns == null)
				return;

			XElement jdr = jdReturns.Elements ("tr").Last ();
			string name = jdr.Elements ().First ().Value.Trim ();
			var nodes = Mdoc.FromHtml (jdr.Elements ().Last ().Nodes ());
			if (clean)
				mdReturns.Value = "";

			mdReturns.Add (Mdoc.FromHtml (jdr.Elements ().Last ()));
			Application.FixupMdoc (mdReturns);
		}

		XElement GetJavaDocSectionTable (XElement jd, string section)
		{
			return jd.Descendants ("table").FirstOrDefault (t => t.XPathSelectElement ("tr/th[string(th) = '" + section + "']") != null);
		}
	}

	class DroidDocDocumentSection : DocumentSection
	{
		public DroidDocDocumentSection (XElement section, XElement summaryDiv)

		{
			if (summaryDiv == null)
				throw new ArgumentNullException (nameof (summaryDiv));
			Section = section;
			SummaryDiv = summaryDiv;
		}

		public XElement Section { get; protected set; }
		public XElement SummaryDiv { get; protected set; }

		public override IEnumerable<XNode> GetSummaryNodes ()
		{
			return SummaryDiv.Nodes ();
		}

		public override void UpdateSince (XElement mdRemarks)
		{
			Section.Descendants ("div").FirstOrDefault (d => Application.HasHtmlClass (d, "api-level"));
		}

		public override void UpdateMemberExceptions (XElement mdoc, bool clean)
		{
			XElement jd = Section;

			XElement jdThrows = GetJavaDocSectionTable (jd, "Throws");
			if (jdThrows == null)
				return;

			XElement mdDocs = mdoc.Element ("Docs");
			if (clean)
				foreach (XElement e in mdDocs.Elements ("exception").ToList ())
					e.Remove ();

			foreach (XElement jdp in jdThrows.Elements ("tr").Elements ("th")) {
				XElement type = jdp.Element ("a");
				if (type != null) {
					mdDocs.Add (new XElement ("exception",
								new XAttribute ("cref", Mdoc.CrefFromHref (type.Attribute ("href").Value)),
								Mdoc.FromHtml (ExceptionDocs (type))));
				} else if ((type = jdp.Element ("td")) != null) {
					mdDocs.Add (new XElement ("exception",
								new XAttribute ("cref", Mdoc.CrefFromJavaType (type.Value.Trim ())),
								Mdoc.FromHtml (ExceptionDocs (type))));
				}
			}

		}

		IEnumerable<XNode> ExceptionDocs (XElement type)
		{
			return type.NodesAfterSelf ()
				.Select (n => (n is XElement && ((XElement)n).Name == "td")
					? ((XElement)n).Nodes ()
					: new [] { n })
				.SelectMany (n => n);
		}

		public override void UpdateMemberParameters (XElement mdoc)
		{
			XElement jd = Section;

			XElement jdParameters = GetJavaDocSectionTable (jd, "Parameters");
			if (jdParameters == null)
				return;

			foreach (XElement jdp in jdParameters.Elements ("tr").Elements ("th")) {
				string name = jdp.FirstNode.ToString ().Trim ();
				if (name == "event")
					name = "e";
				var nodes = Mdoc.FromHtml (jdp.Element ("td").Nodes ());

				UpdateParameter (name, nodes, mdoc);
			}
		}

		public override void UpdateMemberReturns (XElement mdoc, bool clean)
		{
			bool firstPass = clean;
			if (firstPass)
				return;

			XElement jd = Section;

			XElement jdReturns = GetJavaDocSection (jd, "Returns");
			if (jdReturns == null)
				return;

			XElement mdReturns = mdoc.Element ("Docs").Element ("returns") ?? mdoc.Element ("Docs").Element ("value");
			if (mdReturns == null)
				return;

			if (clean)
				mdReturns.Value = "";
			mdReturns.Add (Mdoc.FromHtml (jdReturns.ElementsAfterSelf ("ul").First ()));
			Application.FixupMdoc (mdReturns);
		}

		public override void UpdateMemberSeeAlso (XElement mdoc, bool clean)
		{
			XElement jd = Section;

			XElement mdDocs = mdoc.Element ("Docs");
			if (clean)
				foreach (XElement e in mdDocs.Elements ("altmember").ToList ())
					e.Remove ();

			XElement jdSeeAlso = GetJavaDocSection (jd, "See Also");
			if (jdSeeAlso == null)
				return;

			foreach (XElement jdp in jdSeeAlso.ElementsAfterSelf ("ul").Elements ("li").Elements ("code")) {
				XElement link = jdp.Element ("a");
				if (link == null)
					continue;

				string cref = Mdoc.CrefFromHref (link.Attribute ("href").Value);
				if (cref.Length > 0 && cref [0] == '!')
					continue;

				mdDocs.Add (new XElement ("altmember", new XAttribute ("cref", cref)));
			}
		}

		XElement GetJavaDocSectionTable (XElement jd, string section)
		{
			XElement e = GetJavaDocSection (jd, section);
			if (e == null)
				return null;
			return e.ElementsAfterSelf ("table").FirstOrDefault ();
		}

		static XElement GetJavaDocSection (XElement jd, string section)
		{
			return jd.Descendants ("h5").FirstOrDefault (d => Application.HasHtmlClass (d, "jd-tagtitle") && d.Value == section);
		}
	}

	// JavaDoc6 output format is... ugly. There is no structured information on the output, and document nodes cannot be
	// really parsable in certain format. So we need a lot of ugly decompiling hacks.
	//
	// The worst part is member contents. Parameters, Returns and Related Items are all in plain definition lists
	// (DL/DT/DD elements) and they cannot be distinguished by any structure. Parameters may be missing for non-parameter
	// methods. Returns is missing if the return type is void. Related Items may be missing by nature. No positional
	// assumption works. Those items are only labeled with <b> element e.g. <B>Parameters:</B>. And the labels can be
	// *translated* e.g. it is NOT <B>Parameters</B> if it was Japanese environment when javadoc ran to generate docs.
	//
	// The only possibility I found was that we try *all those translated labels* in JDK (can be done only for OpenJDK).
	// http://hg.openjdk.java.net/jdk7/tl/langtools/file/a72412b148d7/src/share/classes/com/sun/tools/doclets/internal/toolkit/resources/
	//  as indirectly indicated at http://openjdk.java.net/groups/i18n/
	//
	// Fortunately there are only ja_JP and zh_CN which have translations (the source is for JDK7, but they would be the
	// same).

	abstract class JavaDocDocumentSection : DocumentSection
	{
		static readonly string [] param_labels = { "Parameters:", "\u53C2\u6570:", "\u30D1\u30E9\u30E1\u30FC\u30BF:" };
		static readonly string [] returns_labels = { "Returns:", "\u8FD4\u56DE:", "\u623B\u308A\u5024:" };
		static readonly string [] exception_labels = { "Throws:", "\u629B\u51FA:", "\u4F8B\u5916:" };
		static readonly string [] seealso_labels = { "See Also:", "\u53E6\u8BF7\u53C2\u9605:", "\u95A2\u9023\u9805\u76EE:" };
		protected XElement section_node, params_node, returns_node, exception_node, seealso_node;

		public abstract string SectionNameWrapperTag { get; }

		public JavaDocDocumentSection (XElement sectionNode)
		{
			if (sectionNode == null)
				return; // Any derived classes might pass null element here. Since we cannot reject it here, we ignore the entire section.

			section_node = sectionNode;
			foreach (XElement pp in section_node.XPathSelectElements (".//dl/dt")) {
				var b = pp.XPathSelectElement (SectionNameWrapperTag);
				if (b == null)
					continue;
				if (param_labels.Contains (b.Value))
					params_node = pp;
				else if (returns_labels.Contains (b.Value))
					returns_node = pp;
				else if (exception_labels.Contains (b.Value))
					exception_node = pp;
				else if (seealso_labels.Contains (b.Value))
					seealso_node = pp;
			}
		}

		public override void UpdateMemberParameters (XElement mdoc)
		{
			if (params_node == null)
				return;

			foreach (XElement pe in params_node.ElementsAfterSelf ().TakeWhile (e => e.Name.LocalName == "dd")) {
				var code = pe.Element (XName.Get ("code"));
				string name = code.Value;
				if (name == "event")
					name = "e";
				var nodes = Mdoc.FromHtml (code.NodesAfterSelf ());

				UpdateParameter (name, nodes, mdoc);
			}
		}

		public override void UpdateMemberReturns (XElement mdoc, bool clean)
		{
			XElement mdReturns = mdoc.Element ("Docs").Element ("returns") ?? mdoc.Element ("Docs").Element ("value");
			if (mdReturns == null)
				return;

			if (clean)
				mdReturns.Value = "";

			if (returns_node == null)
				return;

			mdReturns.Add (Mdoc.FromHtml (returns_node.ElementsAfterSelf ("dd").First ().Nodes ()));
			Application.FixupMdoc (mdReturns);
		}

		public override void UpdateMemberExceptions (XElement mdoc, bool clean)
		{
			if (exception_node == null)
				return;

			XElement mdDocs = mdoc.Element ("Docs");
			if (clean)
				foreach (XElement e in mdDocs.Elements ("exception").ToList ())
					e.Remove ();

			foreach (XElement jdp in exception_node.ElementsAfterSelf ().TakeWhile (e => e.Name.LocalName == "dd")) {
				XElement type = jdp.Descendants ("a").FirstOrDefault ();
				if (type != null) {
					mdDocs.Add (new XElement ("exception",
						  new XAttribute ("cref", Mdoc.CrefFromHref (type.Attribute ("href").Value)),
						  Mdoc.FromHtml (ExceptionDocs (type))));
				}
			}
		}

		IEnumerable<XNode> ExceptionDocs (XElement type)
		{
			return type.Parent.NodesAfterSelf ();
		}

		public override void UpdateMemberSeeAlso (XElement mdoc, bool clean)
		{
			XElement mdDocs = mdoc.Element ("Docs");
			if (clean)
				foreach (XElement e in mdDocs.Elements ("altmember").ToList ())
					e.Remove ();

			if (seealso_node == null)
				return;

			foreach (XElement pe in seealso_node.ElementsAfterSelf ().TakeWhile (e => e.Name.LocalName == "dd")) {
				XElement link = pe.Element ("a");
				if (link == null)
					continue;

				// FIXME: CrefFromHref() is valid only for DroidDoc. It needs to be rewritten.
				string cref = Mdoc.CrefFromHref (link.Attribute ("href").Value);
				if (cref.Length > 0 && cref [0] == '!')
					continue;

				mdDocs.Add (new XElement ("altmember", new XAttribute ("cref", cref)));
			}
		}
	}

	class JavaDoc6DocumentSection : JavaDocDocumentSection
	{
		public JavaDoc6DocumentSection (XElement anchorNode)
		: base (anchorNode.ElementsAfterSelf ("dl").FirstOrDefault ())
		{
		}

		public override string SectionNameWrapperTag {
			get { return "b"; }
		}

		public override IEnumerable<XNode> GetSummaryNodes ()
		{
			if (section_node == null)
				return Enumerable.Empty<XNode> ();
			var sumDL = section_node.Elements (XName.Get ("dd")).FirstOrDefault ();
			return sumDL == null ? Enumerable.Empty<XNode> () :
			sumDL.Nodes ().TakeWhile (n => !(n is XElement && ((XElement)n).Name.LocalName != "dl"))
				.Where (n => n.NodeType != XmlNodeType.Text || ((XText)n).Value.Length > 0)
				.Select (n => n.NodeType == XmlNodeType.Text ? new XElement ("p", n) : n);
		}
	}

	class JavaDoc7DocumentSection : JavaDocDocumentSection
	{
		public JavaDoc7DocumentSection (XElement anchorNode)
			: base (anchorNode.ElementsAfterSelf ("ul").Elements ("li").FirstOrDefault ())
		{
		}

		public override string SectionNameWrapperTag {
			get { return "span"; }
		}

		public override IEnumerable<XNode> GetSummaryNodes ()
		{
			var div = section_node.Elements ("div").FirstOrDefault ();
			return div == null ? Enumerable.Empty<XNode> () : div.Nodes ()
				.Where (n => n.NodeType != XmlNodeType.Text || ((XText)n).Value.Length > 0)
				.Select (n => n.NodeType == XmlNodeType.Text ? new XElement ("p", n) : n);
		}
	}

	class JavaDoc8DocumentSection : JavaDocDocumentSection
	{
		public JavaDoc8DocumentSection (XElement anchorNode)
			: base (anchorNode.ElementsAfterSelf ("ul").Elements ("li").FirstOrDefault ())
		{
		}

		public override string SectionNameWrapperTag {
			get { return "span"; }
		}

		public override IEnumerable<XNode> GetSummaryNodes ()
		{
			var div = section_node.Elements ("div").FirstOrDefault ();
			return div == null ? Enumerable.Empty<XNode> () : div.Nodes ()
				.Where (n => n.NodeType != XmlNodeType.Text || ((XText)n).Value.Length > 0)
				.Select (n => n.NodeType == XmlNodeType.Text ? new XElement ("p", n) : n);
		}
	}
}
