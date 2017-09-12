using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

using Mono.Options;

using Java.Interop.Tools.TypeNameMappings;

using Android.Runtime;

using Mono.Cecil;

using Assembly = Mono.Cecil.AssemblyDefinition;
using Type = Mono.Cecil.TypeDefinition;//IKVM.Reflection.Type;

namespace Xamarin.Android.Tools.JavaDocToMdoc
{
	public partial class Application
	{
		internal static Dictionary<string, string> PackageRenames;

		internal const string OnlineDocumentationPrefix = "http://developer.android.com/reference/";

		internal static ReadOnlyCollection<Assembly> Assemblies;
		internal static Dictionary<string, string> EnumMappings;
		internal static Dictionary<string, string> EnumMappingsReversed;

		internal static ProcessingContext ProcessingContext;

		internal static HtmlLoader HtmlLoader;

		internal static SampleRepository Samples;

		public static void Run (ProcessingContext options)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));
			ProcessingContext = options;
			HtmlLoader = new HtmlLoader (ProcessingContext);

			// samples are valid only for DroidDoc. Do not try to read them for other doclets.
			try {
				Samples = options.SamplesPath != null ? File.Exists (options.SamplesPath) ? SampleRepository.LoadFrom (options.SamplesPath) : new SampleRepository (options.SamplesPath) : null;
			} catch {
			}

			Assemblies = LoadAssemblies (options.Assemblies);
			EnumMappings = CreateEnumMappings (out EnumMappingsReversed);
			PackageRenames = CreatePackageRenames ();
			ProcessAssemblies (options.TypesToProcess, options.TypesToSkip);
			if (Samples != null)
				Samples.Close (true);
		}

		static Dictionary<string, string> CreatePackageRenames ()
		{
			var renames = Assemblies.SelectMany (assembly => assembly.CustomAttributes).Where (a => a.AttributeType.FullName == "Android.Runtime.NamespaceMappingAttribute")
						.ToDictionary (a => (string)a.Properties.FirstOrDefault (p => p.Name == "Java").Argument.Value,
							       a => (string)a.Properties.FirstOrDefault (p => p.Name == "Managed").Argument.Value);
			return renames;
		}

		static Dictionary<string, string> CreateEnumMappings (out Dictionary<string, string> reversed)
		{
			var mappings = Assemblies.Where (a => a.CustomAttributes.Any (c => c.AttributeType.FullName == "Android.Runtime.NamespaceMappingAttribute")) // any valid binding dll should contain at least one [NamespaceMapping].
				  .SelectMany (a => a.Modules).SelectMany (m => m.Types).Where (t => t.IsEnum)
						 .SelectMany (t => t.Fields.Where (f => f.HasConstant))
						 .Select (f => new {
							 Java = f.CustomAttributes.FirstOrDefault (ca => ca.AttributeType.FullName == "Android.Runtime.IntDefinitionAttribute")
								?.Properties.FirstOrDefault (p => p.Name == "JniField").Argument.Value as string,
							 Managed = "F:" + f.FullName
						 })
						 .Where (p => p.Java != null);
			var ret = new Dictionary<string, string> ();
			foreach (var p in mappings)
				ret [p.Java] = p.Managed;
			reversed = ret.Where (p => p.Value != null).ToDictionary (p => p.Value, p => p.Key);

			return ret;
		}

		static ReadOnlyCollection<Assembly> LoadAssemblies (IEnumerable<string> assemblies)
		{
			return new ReadOnlyCollection<Assembly> (assemblies.Select (a => AssemblyDefinition.ReadAssembly (a/*, null, false*/)).ToList ());
		}

		static void ProcessAssemblies (IEnumerable<string> types, IEnumerable<string> skip)
		{
			bool checkTypes = types.Count () > 0;
			var packages = new HashSet<string> ();

			foreach (Assembly a in Assemblies) {
				foreach (Type t in a.Modules.SelectMany (m => m.Types).SelectMany (t => t.FlattenTypeHierarchy ())) {
					if (checkTypes && !types.Contains (t.FlattenFullName ()))
						continue;
					if (skip.Contains (t.FlattenFullName ()))
						continue;
					RegisterAttribute tregister = ImportDocsForType (t);
					if (tregister == null)
						continue;
					if (string.IsNullOrEmpty (t.Namespace))
						continue;
					string package = GetPackageName (tregister.Name);
					if (package == null || packages.Contains (package))
						continue;

					packages.Add (package);
					ImportDocsForPackage (package, t.Namespace);
				}
			}
		}

		static string GetPackageName (string jniName)
		{
			if (string.IsNullOrEmpty (jniName))
				return null;
			int s = jniName.LastIndexOf ('/');
			if (s == -1)
				return null;
			return jniName.Substring (0, s);
		}

		static void ClearEnumDoc (XElement dest)
		{
			var summary = dest.Descendants ("summary").FirstOrDefault ();
			if (summary != null)
				summary.Value = "";
			var remarks = dest.Descendants ("remarks").FirstOrDefault ();
			if (remarks != null)
				remarks.Value = "";
		}

		static void ImportDocsForEnum (Type type)
		{
			string srcType = null;
			JavaDocDocumentElement src = null;
			string destFile = GetMdocFileName (type);
			XElement dest = GetMdocFile (type, destFile);
			if (dest == null)
				return;

			if (ProcessingContext.MessageLevel > 1)
				Logger.Log (LoggingVerbosity.Debug, 0, "Importing docs for enum: {0}", type);

			foreach (var info in type.Fields.Where (f => f.IsPublic && f.HasConstant)) {
				string jniField;
				if (Application.EnumMappingsReversed.TryGetValue ("F:" + type.FlattenFullName () + "." + info.Name, out jniField)) {
					int index = jniField.LastIndexOf ('.');
					if (index < 0)
						continue;
					string jniType = jniField.Substring (0, index);
					string jniName = jniField.Substring (index + 1);
					if (srcType != jniType) {
						src = HtmlLoader.GetJavaDocDocumentElement (jniType);
						if (src == null)
							return;
						if (srcType == null)
							ClearEnumDoc (dest);
						srcType = jniType;
					}
					src.UpdateEnumField (dest, info.Name, jniName);
				}
			}
			WriteMdocFile (destFile, dest);
		}

		static RegisterAttribute ImportDocsForType (Type type)
		{
			if (type.IsNotPublic)
				return null;

			var tattrs = type.CustomAttributes.Where (t => t.AttributeType.FullName == "Android.Runtime.RegisterAttribute").ToArray ();
			if (tattrs.Length == 0) {
				if (type.IsEnum && Application.EnumMappingsReversed != null)
					ImportDocsForEnum (type);
				return null;
			}
			if (TypeUtilities.ShouldExcludeType (type, (string)tattrs.First ().ConstructorArguments [0].Value))
				return null;

			Logger.Log (LoggingVerbosity.Debug, 0, "Importing docs for type: {0}", type);

			RegisterAttribute tregister = tattrs.Single ().CreateInstance<RegisterAttribute> ();

			var source = HtmlLoader.GetJavaDocDocumentElement (tregister.Name);
			string destFile = GetMdocFileName (type);
			XElement dest = GetMdocFile (type, destFile);
			if (source == null || dest == null)
				return null;

			ProcessingContext.CurrentFilePath = HtmlLoader.GetJavaDocPath (tregister.Name);
			ProcessingContext.CurrentType = type;

			source.UpdateTypeDocs (dest, tregister);
			UpdateTypeIntPtrConstructorDocs (dest);
			UpdateTypeThresholdClassProperty (dest);
			UpdateTypeThresholdTypeProperty (dest);

			foreach (var member in type.Fields.Cast<IMemberDefinition> ().Concat (type.Properties).Concat (type.Methods)) {
				source.UpdateMemberDocs (dest, member, tregister);
			}

			WriteMdocFile (destFile, dest);
			return tregister;
		}
	}

	partial class Application
	{
		static XElement GetMdocFile (Type type, string file)
		{
			if (!File.Exists (file)) {
                		Logger.Log (LoggingVerbosity.Warning, Errors.MissingFileForRegeneration, "Documentation updates for {0} is on hold due to missing file: {1}", type.FlattenFullName (), file);
				return null;
			}
			return GetMdocFile (file);
		}

		static XElement GetMdocFile (string file)
		{
			return XElement.Load (file);
		}

		static string GetMdocFileName (Type type)
		{
			var nsType = type.DeclaringType ?? type;
			var path = new StringBuilder ()
				.Append (ProcessingContext.DestDocumentationRoot)
				.Append (Path.DirectorySeparatorChar)
				.Append (nsType.Namespace)
				.Append (Path.DirectorySeparatorChar);

			var typeParts = new List<string> ();
			while (type != null) {
				typeParts.Add (type.FlattenName ());
				type = type.DeclaringType;
			}
			typeParts.Reverse ();

			path.Append (string.Join ("+", typeParts.ToArray ()));
			path.Append (".xml");

			return path.ToString ();
		}

		static void UpdateTypeIntPtrConstructorDocs (XElement mdoc)
		{
			XElement ctorDocs = mdoc.Elements ("Members")
				.Elements ("Member")
				.Where (m => (string) m.Attribute ("MemberName") == ".ctor" &&
						m.Elements ("Parameters").Elements ("Parameter").Count () == 2 &&
						(string) m.Element ("Parameters").Elements ("Parameter").ElementAt (0).Attribute ("Type") == "System.IntPtr" &&
						(string) m.Element ("Parameters").Elements ("Parameter").ElementAt (1).Attribute ("Type") == "Android.Runtime.JniHandleOwnership")
				.Select (m => m.Element ("Docs"))
				.FirstOrDefault ();
			if (ctorDocs == null)
				return;
			var name = (string) ctorDocs.Elements ("param").ElementAt (0).Attribute ("name");
			var transfer = (string) ctorDocs.Elements ("param").ElementAt (1).Attribute ("name");
			ctorDocs.ReplaceAll (
					new XElement ("param",
						new XAttribute ("name", name),
						"A ", 
						new XElement ("see", new XAttribute ("cref", "T:System.IntPtr")),
						"containing a Java Native Interface (JNI) object reference."),
					new XElement ("param",
						new XAttribute ("name", transfer),
						"A ",
						new XElement ("see", new XAttribute ("cref", "T:Android.Runtime.JniHandleOwnership")),
						"indicating how to handle ",
						new XElement ("paramref", new XAttribute ("name", name))),
					new XElement ("summary",
						"A constructor used when creating managed representations of JNI objects; called by the runtime."),
					new XElement ("remarks",
						new XElement ("para",
							new XAttribute ("tool", "javadoc-to-mdoc"),
							"This constructor is invoked by the runtime infrastructure (",
							new XElement ("see", 
								new XAttribute ("cref", "M:Java.Lang.Object.GetObject``1(System.IntPtr,Android.Runtime.JniHandleOwnership)")),
							") to create a new managed representation for a Java Native Interface object."),
						new XElement ("para",
							new XAttribute ("tool", "javadoc-to-mdoc"),
							"The constructor will initializes the ",
							new XElement ("see",
								new XAttribute ("cref", "P:Android.Runtime.IJavaObject.Handle")),
							" property of the new instance using ",
							new XElement ("paramref",
								new XAttribute ("name", name)),
							" and ",
							new XElement ("paramref", new XAttribute ("name", transfer)),
							".")));
		}

		static void UpdateTypeThresholdClassProperty (XElement mdoc)
		{
			XElement prop = mdoc.Elements ("Members")
				.Elements ("Member")
				.Where (m => (string) m.Attribute ("MemberName") == "ThresholdClass" &&
						m.Element ("MemberType").Value == "Property")
				.Select (m => m.Element ("Docs"))
				.FirstOrDefault ();
			if (prop == null)
				return;
			prop.ReplaceAll (
					new XElement ("summary",
						"This API supports the Mono for Android infrastructure and is not intended to be used directly from your code."),
					new XElement ("value",
						"A ",
						new XElement ("see", new XAttribute ("cref", "T:System.IntPtr")),
						" which contains the ",
						new XElement ("c", "java.lang.Class"), 
						" JNI value corresponding to this type."),
					new XElement ("remarks",
						new XElement ("para",
							new XAttribute ("tool", "javadoc-to-mdoc"),
							"This property is used to control which ",
							new XElement ("c", "jclass"),
							" is provided to methods like ",
							new XElement ("see", new XAttribute ("cref", "M:Android.Runtime.JNIEnv.CallNonVirtualVoidMethod")),
							".")));
		}

		static void UpdateTypeThresholdTypeProperty (XElement mdoc)
		{
			XElement prop = mdoc.Elements ("Members")
				.Elements ("Member")
				.Where (m => (string) m.Attribute ("MemberName") == "ThresholdType" &&
						m.Element ("MemberType").Value == "Property")
				.Select (m => m.Element ("Docs"))
				.FirstOrDefault ();
			if (prop == null)
				return;
			prop.ReplaceAll (
					new XElement ("summary",
						"This API supports the Mono for Android infrastructure and is not intended to be used directly from your code."),
					new XElement ("value",
						"A ",
						new XElement ("see", new XAttribute ("cref", "T:System.Type")),
						" which provides the declaring type."),
					new XElement ("remarks",
						new XElement ("para",
							new XAttribute ("tool", "javadoc-to-mdoc"),
							"This property is used to control virtual vs. non virtual method dispatch " + 
							"against the underlying JNI object. When this property is equal to the " + 
							"declaring type, then virtual method invocation against the JNI object is " +
							"performed; otherwise, we assume that the method was overridden by a derived " +
							"type, and perform non-virtual methdo invocation.")));
		}

		internal static bool HasHtmlClass (XElement e, string @class)
		{
			XAttribute c = e.Attribute ("class");
			if (c == null)
				return false;
			return Regex.IsMatch (c.Value, @"\b" + @class + @"\b");
		}

		internal static void SetSummaryFromRemarks (XElement docs, string prefix = null, bool clean = true)
		{
			var mdRemarks = GetFirstDocumentationElement (docs);
			var mdSummary = docs.Element ("summary");
			if (clean)
				mdSummary.Value = "";
			if (mdRemarks == null)
				return;
			foreach (XNode n in mdRemarks.Nodes ()) {
				if (prefix != null) {
					XElement xe = n as XElement;
					if (xe != null && xe.Name == "format" && xe.Attribute ("tmp") != null)
						continue;
				}
				XText t = n as XText;
				if (t == null) {
					mdSummary.Add (n);
					continue;
				}
				string value = t.Value;
				int e = value.IndexOf ('.');
				if (e == -1) {
					mdSummary.Add (t);
					continue;
				}
				// Often times the summary is overly truncated because an abbreviation
				// is used.  Try to detect the end of the sentence by looking for caps.
				for (int i = e; i < value.Length; ++i) {
					if (char.IsWhiteSpace (value [i]))
						continue;
					if (char.IsUpper (value [i]))
						break;
					if (char.IsLower (value [i])) {
						e = value.IndexOf ('.', i);
						if (e == -1)
							break;
						i = e;
					}
				}
				if (e == -1) {
					mdSummary.Add (t);
					continue;
				}
				mdSummary.Add (value.Substring (0, e+1));
				break;
			}
		}

		static XElement GetFirstDocumentationElement (XElement docs)
		{
			var mdRemarks = docs.Element ("remarks");

			return mdRemarks.Descendants ("para").FirstOrDefault ();
		}

		internal static void AddAndroidDocUrlWithOptionalContent (XElement mdoc, RegisterAttribute tregister, string anchor, string prefix = null, object content = null)
		{
			string url = OnlineDocumentationPrefix + 
				tregister.Name.Replace ('$', '.') +
				".html";
			if (anchor != null)
				url += "#" + anchor;
			XElement format = new XElement ("format", new XAttribute ("type", "text/html"));
			if (prefix != null) {
				format.Add (new XAttribute ("tmp", ""));
				format.Add (new XElement ("b", prefix));
				format.Add (" ");
			}
			format.Add (new XElement ("a", new XAttribute ("href", url), new XAttribute ("target", "_blank"),
				                          "[Android Documentation]"));
			if (prefix != null)
				format.Add (new XElement ("br"));
			XElement para = new XElement ("para",
					new XAttribute ("tool", "javadoc-to-mdoc" + (prefix == null ? "" : ": " + prefix)),
					format);
			if (content != null) {
				para.Add (content);
				XElement last = para.Elements ().Last ();
				if (last.Name == "para") {
					last.Remove ();
					para.Add (last.Nodes ());
				}
			}
			mdoc.Add (para);
		}

		internal static string GetAnchor (RegisterAttribute tregister, RegisterAttribute mregister)
		{
			string name = mregister.Name;
			if (name == ".ctor") {
				name = tregister.Name.Replace ('$', '.');
				int n = name.LastIndexOf ('/');
				if (n >= 0)
					name = name.Substring (n+1);
			}

			if (mregister.Signature == null)
				return name;

			var anchor = new StringBuilder ()
				.Append (name)
				.Append ("(");
			bool first = true;
			foreach (JniTypeName t in JavaNativeTypeManager.FromSignature (mregister.Signature)) {
				if (!first)
					anchor.Append (", ");
				first = false;
				anchor.Append (t.Type);
			}
			anchor.Append (")");

			return anchor.ToString ();
		}

		static void WriteMdocFile (string file, XElement contents)
		{
			var settings = new XmlWriterSettings () {
				Encoding            = new UTF8Encoding (false),
				Indent              = true,
				OmitXmlDeclaration  = true,
			};
			using (var output = new StreamWriter (file, false, settings.Encoding)) {
				using (var writer = XmlWriter.Create (output, settings))
					contents.Save (writer);
				output.WriteLine ();
			}
		}

		static void ImportDocsForPackage (string package, string ns)
		{
			string packagePath = new StringBuilder (ProcessingContext.SourceDocumentationRoot.Length + 1 + package.Length + 19)
				.Append (ProcessingContext.SourceDocumentationRoot)
				.Append (Path.DirectorySeparatorChar)
				.Append (package)
				.Append (Path.DirectorySeparatorChar)
				.Append ("package-summary.html")
				.ToString ();
			if (!File.Exists (packagePath)) {
				Logger.Log (LoggingVerbosity.Warning, Errors.MissingPackagePathToImportDocumentation, "Could not import documentation for package '{0}', as file '{1}' does not exist.", package, packagePath);
				return;
			}
			string nsDocsPath = new StringBuilder (ProcessingContext.DestDocumentationRoot.Length + 1 + ns.Length + 7)
				.Append (ProcessingContext.DestDocumentationRoot)
				.Append (Path.DirectorySeparatorChar)
				.Append ("ns-")
				.Append (ns)
				.Append (".xml")
				.ToString ();

			var source  = HtmlLoader.GetJavaDocFile (packagePath);
			XElement dest    = GetMdocFile (nsDocsPath);

			// new DroidDoc -> JavaDoc
			var jdSummaryDiv = source.Descendants ("div")
			                         .Where (d => d.Attribute ("id")?.Value == "jd-content" || HasHtmlClass (d, "jd-descr"))
				.FirstOrDefault ();
			if (jdSummaryDiv == null)
				return;

			var mdRemarks = dest.XPathSelectElement ("Docs/remarks");
			mdRemarks.Value = "";
			// it's somewhat hacky... it's not only for DroidDoc, but hopefully works.
			mdRemarks.Add (new DroidDocDocumentElement (new DroidDocMdocHelper (), source).Mdoc.FromHtml (jdSummaryDiv.Nodes ()));
			FixupMdoc (mdRemarks);
			SetSummaryFromRemarks (dest.Element ("Docs"));

			WriteMdocFile (nsDocsPath, dest);
		}

		internal static void FixupMdoc (XElement root)
		{
			FlattenParas (root);
			FlattenTerms (root);
		}

		static void FlattenParas (XElement root)
		{
			foreach (var e in root.Elements ()) {
				XNode before = e.NextNode;
				XNode after  = e;
				foreach (var p in e.Descendants ("para").ToList ()) {
					Append (p, before, ref after);
				}
			}
			XElement add = null;
			foreach (var n in root.Nodes ().ToList ()) {
				var e = n as XElement;
				if (e != null && e.Name.LocalName == "para") {
					add = e.HasElements ? null : e;
					continue;
				}
				if (add != null) {
					n.Remove ();
					add.Add (n);
				}
			}
		}

		static void Append (XElement node, XNode before, ref XNode after)
		{
			node.Remove ();
			if (before != null) {
				before.AddBeforeSelf (node);
				return;
			}
			after.AddAfterSelf (node);
			after = node;
		}

		static void FlattenTerms (XElement root)
		{
			foreach (var item in root.Descendants ("list").Elements ("item")) {
				XNode before = item.NextNode;
				XNode after  = item;
				foreach (var nestedItem in item.Descendants ("item").ToList ()) {
					Append (nestedItem, before, ref after);
				}
			}
		}
	}


	/* This class gives processing methods (namely samples processing)
	 * some context on what they are currently working on
	 */
	public class ProcessingContext {
		public TextWriter LoggingOutput { get; set; } = Console.Error;
		public string CurrentFilePath { get; set; }
		public Type CurrentType { get; set; }

		public string SourceDocumentationRoot { get; set; }
		public string DestDocumentationRoot { get; set; }
		public int MessageLevel { get; set; }
		public bool ImportSamples { get; set; }
		public string SamplesPath { get; set; }
		public IEnumerable<string> Assemblies { get; set; }
		public IEnumerable<string> TypesToProcess { get; set; }
		public IEnumerable<string> TypesToSkip { get; set; }
	}

	static class Errors
	{
		public const int CouldNotFindSourceJavadoc = 1;
		public const int MissingFileForRegeneration = 2;
		public const int MissingPackagePathToImportDocumentation = 3;
		public const int JavaDocSectionNotFound = 4;
		public const int ManagedParameterNotFound = 5;
	}
}
