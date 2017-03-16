using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

using Android.Runtime;

using Mono.Cecil;

using MemberInfo = Mono.Cecil.IMemberDefinition;
using MethodInfo = Mono.Cecil.MethodDefinition;
using PropertyInfo = Mono.Cecil.PropertyDefinition;

namespace Xamarin.Android.Tools.JavaDocToMdoc
{

	abstract class JavaDocDocumentElement
	{
		protected JavaDocDocumentElement (MdocHelper mdoc, XElement docElem)
		{
			Mdoc = mdoc;
			Element = docElem;
		}

		public MdocHelper Mdoc { get; private set; }
		public XElement Element { get; private set; }

		public virtual void UpdateSince (XElement mdRemarks)
		{
			// no document element for "since API Level" by default.
		}

		public void UpdateTypeDocs (XElement mdoc, RegisterAttribute tregister)
		{
			var jd = Element;
			var mdRemarks = mdoc.XPathSelectElement ("Docs/remarks");
			mdRemarks.Value = "";
			var nodes = GetTypeSummaryNodes (jd);
			mdRemarks.Add (Mdoc.FromHtml (nodes));
			Application.FixupMdoc (mdRemarks);
			Application.AddAndroidDocUrlWithOptionalContent (mdRemarks, tregister, null);
			UpdateSince (mdRemarks);
			Application.SetSummaryFromRemarks (mdoc.Element ("Docs"));
		}

		public abstract void UpdateEnumField (XElement dest, string member, string java);

		public abstract IEnumerable<XNode> GetTypeSummaryNodes (XElement jd);

		public abstract DocumentSection CreateSection (XElement sectionAnchor, string anchor, RegisterAttribute mregister);

		public virtual XElement GetSectionAnchor (string localAnchor)
		{
			return Element.Descendants ("a").FirstOrDefault (a => {
				var n = a.Attribute ("name");
				if (n == null || n.Value != localAnchor)
					return false;
				return true;
			});
		}

		public DocumentSection GetSection (MemberInfo member, RegisterAttribute tregister)
		{
			XElement jd = Element;
			string anchor = null;

			var mattrs = (RegisterAttribute [])member.GetCustomAttributes<RegisterAttribute> (false);
			if (mattrs.Length == 0) {
				return null;
			}
			var mregister = mattrs.Single ();
			if (member is MethodDefinition &&
			    ((MethodInfo)member).IsPrivate)
				return null;
			if (member is TypeDefinition)
				return null;
			anchor = Application.GetAnchor (tregister, mregister);
			if (Application.ProcessingContext.MessageLevel > 1)
				Logger.Log (LoggingVerbosity.Debug, 0, "\tUpdating member anchor: {0}", anchor);
			string localAnchor = anchor;
			XElement sectionAnchor = GetSectionAnchor (localAnchor);
			if (sectionAnchor == null) {
				Logger.Log (LoggingVerbosity.Warning, Errors.JavaDocSectionNotFound, "Could not find JavaDoc for member '{1}' - managed member is '{0}'", member, anchor);
				return null;
			}

			return CreateSection (sectionAnchor, anchor, mregister);
		}

		public void UpdateMemberDocs (XElement mdoc, IMemberDefinition member, RegisterAttribute tregister)
		{
			XElement jd = Element;

			RegisterAttribute mregister;
			string anchor;
			var section = GetSection (member, tregister);
			if (section == null)
				return;

			PropertyInfo property;
			IEnumerable<XElement> destinations = GetMdocMembers (mdoc, member, section.RegisterAttribute, out property);
			MemberInfo secondPropertyMember = null;
			DocumentSection secondSection = null;
			RegisterAttribute secondmregister;
			string prefix = null, secondPrefix = null;
			if (property != null) {
				MethodInfo mi = member as MethodInfo;
				if (mi == property.SetMethod)
					return;
				secondPropertyMember = property.SetMethod;
				if (secondPropertyMember != null) {
					secondSection = GetSection (secondPropertyMember, tregister);
					if (secondSection != null) {
						prefix = "Get";
						secondPrefix = "Set";
					}
				}
			}

			foreach (XElement dest in destinations) {
				section.UpdateMemberSection (member, tregister, dest, prefix, true);
				if (secondSection != null) {
					secondSection.UpdateMemberSection (secondPropertyMember, tregister, dest, secondPrefix, false);
					foreach (var e in dest.Descendants ("format"))
						foreach (var attr in e.Attributes ("tmp").ToList ())
							attr.Remove ();
				}
			}
		}

		static IEnumerable<XElement> GetMdocMembers (XElement mdoc, MemberInfo member, RegisterAttribute register, out PropertyInfo property)
		{
			MethodInfo method = member as MethodInfo;
			if (method != null && method.IsSpecialName && !method.IsConstructor) {
				// member is a get or set method for a property, and the property
				// won't have a [Register] attribute in the docs.
				property = method.DeclaringType.Properties// (DefaultBindingFlags)
					.Single (p => (p.GetMethod == method) || (p.SetMethod == method));
				string name = property.Name;
				return mdoc.XPathSelectElements ("Members/Member")
					.Where (m => m.Attribute ("MemberName").Value == name && m.Element ("MemberType").Value == "Property");
			}
			property = null;
			string attribute = string.IsNullOrEmpty (register.Signature) && string.IsNullOrEmpty (register.Connector)
				? string.Format ("Android.Runtime.Register(\"{0}\")", register.Name)
					: string.Format ("Android.Runtime.Register(\"{0}\", \"{1}\", \"{2}\")",
							 register.Name, register.Signature, register.Connector);

			return
				from m in mdoc.XPathSelectElements ("Members/Member")
				where m.Elements ("Attributes")
					      .Elements ("Attribute")
					      .Elements ("AttributeName")
					      // now n.Value may have ", ApiSince=xx" suffix, which requires partial matching...
					      .Any (n => string.CompareOrdinal (n.Value, 0, attribute, 0, attribute.Length - 1) == 0)
				select m;
		}
	}

	class DroidDocDocumentElement : JavaDocDocumentElement
	{
		public DroidDocDocumentElement (MdocHelper mdoc, XElement documentElement)
			: base (mdoc, documentElement)
		{
		}

		public override DocumentSection CreateSection (XElement sectionAnchor, string anchor, RegisterAttribute mregister)
		{
			var section = sectionAnchor.ElementsAfterSelf ("div").First (d => Application.HasHtmlClass (d, "jd-details"));
			var jdSummaryDiv = section.Descendants ("div").First (d => Application.HasHtmlClass (d, "jd-tagdescr"));
			return new DroidDocDocumentSection (section, jdSummaryDiv) { Mdoc = Mdoc, Anchor = anchor, RegisterAttribute = mregister };
		}

		public override XElement GetSectionAnchor (string localAnchor)
		{
			return Element.Descendants ("a").FirstOrDefault (a => {
				var n = a.Attribute ("name");
				if (n == null || n.Value != localAnchor)
					return false;
				var div = a.ElementsAfterSelf ("div").FirstOrDefault ();
				return div != null && Application.HasHtmlClass (div, "jd-details");
			});
		}

		public override void UpdateSince (XElement mdRemarks)
		{
			Element.Descendants ("div").FirstOrDefault (d => Application.HasHtmlClass (d, "api-level"));
		}

		public override IEnumerable<XNode> GetTypeSummaryNodes (XElement jd)
		{
			var jdSummaryDiv = jd.Descendants ("div")
				.Where (d => Application.HasHtmlClass (d, "jd-descr") &&
						d.Descendants ("h2").Any (h2 => h2.Value == "Class Overview"))
					.FirstOrDefault ();
			if (jdSummaryDiv == null)
				return null;
			return jdSummaryDiv.Descendants ("h2").First ().NodesAfterSelf ();
		}

		public override void UpdateEnumField (XElement dest, string member, string java)
		{
			XElement jd = Element;

			var div = jd.Descendants ("div").Where (d => Application.HasHtmlClass (d, "jd-details") &&
								d.Descendants ("h4").Any (h4 => h4.LastNode.ToString ().Trim () == java)).FirstOrDefault ();
			if (div == null)
				return;
			var srcDoc = div.Descendants ("div").Where (d => Application.HasHtmlClass (d, "jd-tagdescr")).Elements ();
			if (srcDoc == null)
				return;
			var dSummary = dest.Descendants ("Member").Where (d => d.Attribute ("MemberName").Value == member).Descendants ("summary").FirstOrDefault ();
			if (dSummary == null)
				return;
			dSummary.Value = "";
			dSummary.Add (Mdoc.FromHtml (srcDoc));
			Application.FixupMdoc (dSummary);
		}
	}

	class DroidDoc2DocumentElement : DroidDocDocumentElement
	{
		public DroidDoc2DocumentElement (MdocHelper mdoc, XElement documentElement)
			: base (mdoc, documentElement)
		{
		}


		static readonly XName name_a = XName.Get ("a");
		static readonly XName name_href = XName.Get ("href");
		static readonly XName name_tr = XName.Get ("tr");

		List<Tuple<string, XElement>> hrefs;

		bool IsNamedAnchor (XElement a)
		{
			return a.Parent?.Parent?.Name?.LocalName == "td" && a.Attribute (name_href) != null;
		}

		public override DocumentSection CreateSection (XElement sectionAnchor, string anchor, RegisterAttribute mregister)
		{
			if (hrefs == null)
				hrefs = this.Element.Descendants (name_a).Where (IsNamedAnchor)
				            .Select (a => Tuple.Create (TypeUtilities.StripGenericArgument (a.Attribute (name_href).Value), a))
						.ToList ();
			var section = sectionAnchor.ElementsAfterSelf ("div").First (d => d.Elements ().Any (p => Application.HasHtmlClass (p, "api-signature")));
			var summary = hrefs.FirstOrDefault (p => p.Item1 == '#' + anchor)?.Item2?.Parent?.Parent ??
					   // sometimes DroidDoc hrefs have generic arguments within their links, which is annoying...
					   this.Element.Descendants (name_a).FirstOrDefault (a => IsNamedAnchor (a) && (a.Attribute (name_href)?.Value.EndsWith ('#' + anchor, StringComparison.Ordinal) ?? false))?.Parent?.Parent;
			if (summary == null)
				return null;
			return new DroidDoc2DocumentSection (section, summary) { Mdoc = Mdoc, Anchor = anchor, RegisterAttribute = mregister };
		}

		public override XElement GetSectionAnchor (string localAnchor)
		{
			return Element.Descendants ("a").FirstOrDefault (a => {
				var n = a.Attribute ("name");
				if (n == null || TypeUtilities.StripGenericArgument (n.Value) != localAnchor)
					return false;
				var div = a.ElementsAfterSelf ("div").FirstOrDefault (
					d => d.Elements ().Any (p => Application.HasHtmlClass (p, "api-signature")));
				return div != null;
			});
		}

		public override IEnumerable<XNode> GetTypeSummaryNodes (XElement jd)
		{
			var table = jd.Descendants ("table")
			              .Where (d => Application.HasHtmlClass (d, "jd-inheritance-table"))
				      .FirstOrDefault ();
			var p = table.ElementsAfterSelf ("p")
			             .Take (1);
			return p;
		}

		public override void UpdateEnumField (XElement dest, string member, string java)
		{
			XElement jd = Element;

			var div = jd.Descendants ("div").Where (d => Application.HasHtmlClass (d, "api") &&
								d.Elements ("h3").Any (h3 => h3.LastNode.ToString ().Trim () == java)).FirstOrDefault ();
			if (div == null)
				return;
			var srcDoc = div.Descendants ("p");
			if (srcDoc == null)
				return;
			var dSummary = dest.Descendants ("Member").Where (d => d.Attribute ("MemberName").Value == member).Descendants ("summary").FirstOrDefault ();
			if (dSummary == null)
				return;
			dSummary.Value = "";
			dSummary.Add (Mdoc.FromHtml (srcDoc));
			Application.FixupMdoc (dSummary);
		}
	}

	class JavaDoc6DocumentElement : JavaDocDocumentElement
	{
		public JavaDoc6DocumentElement (MdocHelper mdoc, XElement documentElement)
			: base (mdoc, documentElement)
		{
		}

		public override DocumentSection CreateSection (XElement sectionAnchor, string anchor, RegisterAttribute mregister)
		{
			return new JavaDoc6DocumentSection (sectionAnchor) { Mdoc = Mdoc, Anchor = anchor, RegisterAttribute = mregister };
		}

		public override IEnumerable<XNode> GetTypeSummaryNodes (XElement jd)
		{
			var classDataComment = jd.DescendantNodes ()
				.Where (n => n.NodeType == XmlNodeType.Comment && ((XComment)n).Value.IndexOf ("======== START OF CLASS DATA ========") > 0)
				.FirstOrDefault ();
			if (classDataComment == null)
				yield break;
			var dlStart = classDataComment.ElementsAfterSelf ()
				.Where (e => e.Name.LocalName == "dl" && e.XPathSelectElement ("dt/pre") != null || e.Element ("pre") != null)
				.FirstOrDefault ();
			if (dlStart == null)
				yield break;
			foreach (var n in dlStart.NodesAfterSelf ()) {
				//n.WriteTo (xw);
				// The next comment node is the end of this CLASS SUMMARY. It could be different comments
				if (n.NodeType == XmlNodeType.Comment) {
					var c = (XComment)n;
					if (c.Value.IndexOf (" SUMMARY ========") > 0)
						yield break;
				} else if (n is XText) {
					// FIXME: we don't use HAP for JavaDoc6 anymore, so we don't need such a mess.

					// HtmlAgilityPack somehow returns sequential split text nodes.
					// We don't want to wrap everything in separate <p> element, so merge them here.
					// So we concat every following XText in one single node.
					if (n.PreviousNode != null && n.PreviousNode.NodeType == XmlNodeType.Text)
						continue;
					var v = ((XText)n).Value + string.Concat (n.NodesAfterSelf ().TakeWhile (x => x.NodeType == XmlNodeType.Text).Cast<XText> ().Select (x => x.Value));
					yield return new XElement ("p", v);
				} else {
					// Sometimes this NESTED CLASS SUMMARY node could come *inside* a <p> tag. I think it is wrong, but
					// 1) I don't trust SgmlReader, and 2) I don't trust javadoc/doclet.
					var e = n as XElement;
					if (e != null && e.Nodes ().Any (x => x.NodeType == XmlNodeType.Comment && ((XComment)x).Value.IndexOf (" SUMMARY ========") > 0))
						yield break;
					// skip empty <P> tag.
					if (e == null || e.Name.LocalName != "p" || e.Nodes ().Any ())
						yield return n;
				}
			}
		}

		public override void UpdateEnumField (XElement dest, string member, string java)
		{
			throw new NotImplementedException ();
		}
	}

	class JavaDoc7DocumentElement : JavaDocDocumentElement
	{
		public JavaDoc7DocumentElement (MdocHelper mdoc, XElement documentElement)
			: base (mdoc, documentElement)
		{
		}

		public override DocumentSection CreateSection (XElement sectionAnchor, string anchor, RegisterAttribute mregister)
		{
			return new JavaDoc7DocumentSection (sectionAnchor) { Mdoc = Mdoc, Anchor = anchor, RegisterAttribute = mregister };
		}

		public override IEnumerable<XNode> GetTypeSummaryNodes (XElement jd)
		{
			var desc = jd.Descendants ("div").Where (e => Application.HasHtmlClass (e, "contentContainer"))
				.Elements ("div").Where (e => Application.HasHtmlClass (e, "description"))
				.First ();
			if (desc == null)
				return new XNode [0];
			var div = desc.XPathSelectElement ("ul/li/div");
			if (div == null)
				return new XNode [0];
			return div.Nodes ();
		}

		public override void UpdateEnumField (XElement dest, string member, string java)
		{
			throw new NotImplementedException ();
		}
	}

	class JavaDoc8DocumentElement : JavaDoc7DocumentElement
	{
		public JavaDoc8DocumentElement (MdocHelper mdoc, XElement documentElement)
			: base (mdoc, documentElement)
		{
		}

		public override DocumentSection CreateSection (XElement sectionAnchor, string anchor, RegisterAttribute mregister)
		{
			return new JavaDoc8DocumentSection (sectionAnchor) { Mdoc = Mdoc, Anchor = anchor, RegisterAttribute = mregister };
		}

		public override XElement GetSectionAnchor (string localAnchor)
		{
			localAnchor = localAnchor.Replace ('(', '-').Replace (')', '-').Replace (',', '-').Replace (" ", string.Empty).Replace ("[]", ":A");
			while (localAnchor.IndexOf ('<') >= 0 && localAnchor.IndexOf ('>') > localAnchor.IndexOf ('<'))
				localAnchor = localAnchor.Substring (0, localAnchor.IndexOf ('<')) + localAnchor.Substring (localAnchor.IndexOf ('>') + 1);
			return Element.Descendants ("a").FirstOrDefault (a => {
				var n = a.Attribute ("name");
				if (n == null || n.Value.Replace ("...", ":A") != localAnchor)
					return false;
				return true;
			});
		}
	}
}
