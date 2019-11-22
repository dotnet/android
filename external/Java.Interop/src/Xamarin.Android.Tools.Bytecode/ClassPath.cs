using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Text;

namespace Xamarin.Android.Tools.Bytecode {

	public enum JavaDocletType {
		DroidDoc,
		DroidDoc2,
		Java6,
		Java7,
		Java8,
		_ApiXml,
		JavaApiParameterNamesXml,
	}

	public class ClassPath {

		IList<ClassFile> classFiles = new List<ClassFile> ();

		public string ApiSource { get; set; }

		public IEnumerable<string> DocumentationPaths { get; set; }

		public string AndroidFrameworkPlatform { get; set; }

		public bool AutoRename { get; set; }

		public ClassPath (string path = null)
		{
			if (string.IsNullOrEmpty (path))
				return;

			Load (path);
		}

		public void Load (string jarFile)
		{
			if (!IsJarFile (jarFile))
				throw new ArgumentException ("'jarFile' is not a valid .jar file.", "jarFile");

			using (var jarStream = File.OpenRead (jarFile)) { 
				Load (jarStream);
			}
		}

		public void Load (Stream jarStream, bool leaveOpen = false)
		{
			if (jarStream == null)
				throw new ArgumentNullException (nameof (jarStream));

			using (var jar = CreateZipArchive (jarStream, leaveOpen)) {
				foreach (var entry in jar.Entries) {
					if (entry.Length == 0)
						continue;
					using (var s = entry.Open ()) {
						if (!ClassFile.IsClassFile (s))
							continue;
					}
					using (var s = entry.Open ()) {
						try {
							var c   = new ClassFile (s);
							Add (c);
						} catch (Exception e) {
							Log.Warning (0, "class-parse: warning: Could not load .jar entry '{0}': {1}",
									entry.Name, e);
						}
					}
				}
			}
		}

		static ZipArchive CreateZipArchive (Stream jarStream, bool leaveOpen)
		{
			var encoding    = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false);

			return new ZipArchive (jarStream, ZipArchiveMode.Read, leaveOpen: leaveOpen, entryNameEncoding: encoding);
		}

		public void Add (ClassFile classFile)
		{
			classFiles.Add (classFile);
		}

		public ReadOnlyDictionary<string, List<ClassFile>> GetPackages ()
		{
			return new ReadOnlyDictionary<string, List<ClassFile>> (classFiles
				.Select (x => x.PackageName)
				.Distinct ()
				.ToDictionary (x => x,
					x => classFiles.Where (p => p.PackageName == x).ToList ()));
		}

		public static bool IsJarFile (string jarFile)
		{
			if (jarFile == null)
				throw new ArgumentNullException ("jarFile");
			try {
				using (var f = File.OpenRead (jarFile))
				using (new ZipArchive (f)) {
					return true;
				}
			}
			catch (Exception) {
				return false;
			}
		}

		XAttribute GetApiSource ()
		{
			if (string.IsNullOrEmpty (ApiSource))
				return null;
			return new XAttribute ("api-source", ApiSource);
		}

		XAttribute GetPlatform ()
		{
			if (string.IsNullOrEmpty (AndroidFrameworkPlatform))
				return null;
			return new XAttribute ("platform", AndroidFrameworkPlatform);
		}

		bool IsGeneratedName (string parameterName)
		{
			return parameterName.StartsWith ("p") && parameterName.Length > 1 && Char.IsDigit (parameterName [1]);
		}

		IEnumerable<ClassFile> GetDescendants (ClassFile theClass, IList<ClassFile> classFiles)
		{
			for (var c = classFiles.FirstOrDefault(x => IsDescendedFrom (x, theClass.ThisClass)); 
					c != null;
					c = classFiles.FirstOrDefault(x => IsDescendedFrom (x, c.ThisClass)))
				yield return c;
		}

		IEnumerable<ClassFile> GetInterfaceImplemetations (ClassFile iface, IList<ClassFile> classFiles)
		{
			return classFiles.Where (x =>  ImplementsInterface(x, iface));
		}

		public bool IsDescendedFrom (ClassFile classToCheck, ConstantPoolClassItem baseClass)
		{
			if (classToCheck.SuperClass == null && baseClass == null)
				return true;
			if (classToCheck.SuperClass == null || baseClass == null)
				return false;
			return classToCheck.SuperClass.Name.Value == baseClass.Name.Value;
		}

		public bool ImplementsInterface (ClassFile classToCheck, ClassFile targetInteface)
		{
			return classToCheck.GetInterfaces ().Any (iface => targetInteface.ThisClass.Name.Value == iface.BinaryName);
		}

		void FixUpParameters (ClassFile toUpdate, List<ClassFile> implementations)
		{
			foreach (var method in toUpdate.Methods) {
				var targetParams = method.GetParameters ();
				if (targetParams.All (p => !IsGeneratedName (p.Name)))
					continue;
				var candidates =
					(from  candidateMethod in implementations.SelectMany (x => x.Methods)
					 where candidateMethod.Name == method.Name
					 where candidateMethod.Descriptor == method.Descriptor
					 let   candidateParams = candidateMethod.GetParameters ()
					 where candidateParams.Length == targetParams.Length
					 where candidateParams.All (p => !IsGeneratedName (p.Name))
					 select candidateParams);
				if (!candidates.Any ())
					continue;

				var parameters = new List<ParameterInfo> [targetParams.Length];
				foreach (var candidate in candidates) {
					for (int i = 0; i < candidate.Length; i++) {
						if (parameters [i] == null)
							parameters [i] = new List<ParameterInfo> ();
						parameters [i].Add (candidate [i]);
					}
				}

				for (int i = 0; i < parameters.Length; i++) {
					var r  = parameters [i].GroupBy (x => x.Name)
						.Select (group => new { 
							Name = group.Key, 
							Count = group.Count () 
						})
						.OrderByDescending (x => x.Count)
						.FirstOrDefault ();
					if (r != null)
						targetParams [i].Name = r.Name;
				}
			}
		}

		void FixUpParametersFromClasses ()
		{
			// Fix up the Parameters on all the abstract classes
			// and interfaces. We do the abstact classes first because
			// many interfaces are implemented in abstract classes. This
			// is the easiest way to ensure that the parameter name changes
			// percolate up.
			foreach (var abstractClass in classFiles.Where(c =>
							(c.AccessFlags & ClassAccessFlags.Abstract) != 0 &&
							(c.AccessFlags & ClassAccessFlags.Interface) == 0)) {

				List<ClassFile> implementations = GetDescendants (abstractClass, classFiles)
					.ToList();
				if (!implementations.Any ())
					continue;
				FixUpParameters (abstractClass, implementations);
			}
			foreach (var iface in classFiles.Where(c =>
						(c.AccessFlags & ClassAccessFlags.Abstract) != 0 &&
						(c.AccessFlags & ClassAccessFlags.Interface) != 0)) {
				List<ClassFile> implementations = GetInterfaceImplemetations (iface, classFiles)
					.ToList();
				if (!implementations.Any ())
					continue;
				for (int i=0; i < implementations.Count; i++) {
					implementations.AddRange ( GetDescendants (implementations [i], classFiles)
							.Where (x => !implementations.Contains(x)));
				}
				FixUpParameters (iface, implementations);
			}
		}

		void FixupParametersFromDocs (XElement api)
		{
			if (DocumentationPaths == null)
				return;
			foreach (var path in DocumentationPaths) {
				if (!Directory.Exists (path) && !File.Exists (path))
					continue;
				FixupParametersFromDocs (api, path);
			}
		}

		IJavaMethodParameterNameProvider CreateDocScraper (string src)
		{
			switch (JavaMethodParameterNameProvider.GetDocletType (src)) {
			default: return new DroidDoc2Scraper (src);
			case JavaDocletType.DroidDoc: return new DroidDocScraper (src);
			case JavaDocletType.Java6: return new JavaDocScraper (src);
			case JavaDocletType.Java7: return new Java7DocScraper (src);
			case JavaDocletType.Java8: return new Java8DocScraper (src);
			case JavaDocletType._ApiXml: return new ApiXmlDocScraper (src);
			case JavaDocletType.JavaApiParameterNamesXml: return new JavaParameterNamesLoader (src);
			}
		}

		void FixupParametersFromDocs (XElement api, string path)
		{
			var jdoc = CreateDocScraper (path);
			var elements = api.XPathSelectElements ("./package/class[@visibility = 'public' or @visibility = 'protected']").ToList ();
			elements.AddRange (api.XPathSelectElements ("./package/interface[@visibility = 'public' or @visibility = 'protected']"));
			foreach (var elem in elements) {
				var currentpackage = elem.Parent.Attribute ("name").Value;
				var className = elem.Attribute ("name").Value;

				var methodsAndConstructors = elem.XPathSelectElements ("./method[@visibility = 'public' or @visibility = 'protected']").ToList ();
				methodsAndConstructors.AddRange (elem.XPathSelectElements ("./constructor[@visibility = 'public' or @visibility = 'protected']"));

				foreach (var method in methodsAndConstructors) {
					var currentMethod = method.Attribute ("name").Value;

					var parameterElements = method.Elements ("parameter").ToList ();
					if (!parameterElements.Select (x => x.Attribute ("name").Value).Any (p => IsGeneratedName (p)))
						continue;

					var parameters = parameterElements.Select (p => p.Attribute ("type").Value);

					if (!parameters.Any ())
						continue;

					var pnames = jdoc.GetParameterNames (currentpackage, className, currentMethod, parameters.ToArray (), isVarArgs: false);
					if (pnames == null || pnames.Length != parameterElements.Count)
						continue;
					for (int i = 0; i < parameterElements.Count; i++) {
						parameterElements [i].Attribute ("name").Value = pnames [i];
					}
				}
			}
		}

		public XElement ToXElement ()
		{
			// In general, don't do this. It brings metadata fixup incompatibility.
			// API XML incompatibility makes adoption of class-parse impossible and
			// goes against the entire purpose of class-parse existence.
			// Bringing metadata fixup incompatibility means more API difference bugs.
			// This optional behavior is to bring compatibility with old behavior.
			if (AutoRename)
				FixUpParametersFromClasses ();

			KotlinFixups.Fixup (classFiles);

			var packagesDictionary = GetPackages ();
			var api = new XElement ("api",
					GetApiSource (),
					GetPlatform (),
					packagesDictionary.Keys.OrderBy (p => p, StringComparer.OrdinalIgnoreCase)
					.Select (p => new XElement ("package",
						new XAttribute ("name", p),
						new XAttribute ("jni-name", p.Replace ('.', '/')),
						packagesDictionary [p].OrderBy (c => c.ThisClass.Name.Value, StringComparer.OrdinalIgnoreCase)
						.Select (c => new XmlClassDeclarationBuilder (c).ToXElement ()))));
			FixupParametersFromDocs (api);
			return api;
		}

		public void SaveXmlDescription (string fileName)
		{
			var encoding    = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false);
			using (var output = new StreamWriter (fileName, append:false, encoding:encoding)) {
				SaveXmlDescription (output);
			}
		}

		public void SaveXmlDescription (TextWriter textWriter)
		{
			var settings = new XmlWriterSettings () {
				Indent              = true,
				OmitXmlDeclaration  = true,
				NewLineOnAttributes = true,
			};
			var contents    = ToXElement ();
			using (var writer = XmlWriter.Create (textWriter, settings))
				contents.Save (writer);
			textWriter.WriteLine ();
		}
	}
}
