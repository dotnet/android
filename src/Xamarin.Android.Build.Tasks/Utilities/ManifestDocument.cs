using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Build.Utilities;

using Android.App;
using Android.Content;

using Mono.Cecil;
using Mono.Cecil.Cil;

using MonoDroid.Utils;
using Monodroid;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;

using System.Xml;
using System.Text;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks {

	internal class ManifestDocument
	{
		public static XNamespace AndroidXmlNamespace = "http://schemas.android.com/apk/res/android";

		const int maxVersionCode = 2100000000;

		static XNamespace androidNs = AndroidXmlNamespace;

		XDocument doc;
		
		XName attName;

		XElement app;

		// the elements and attributes which we apply the "." -> PackageName replacement on
		static readonly Dictionary<string, string []> ManifestAttributeFixups = new Dictionary<string, string []> {
			{ "activity", new string[] {
					"name",
				}
			},
			{ "application", new string[] {
					"backupAgent",
				}
			},
			{ "instrumentation", new string[] {
					"name",
				}
			},
			{ "provider", new string[] {
					"name",
				}
			},
			{ "receiver", new string[] {
					"name",
				}
			},
			{ "service", new string[] {
					"name",
				}
			},
		};

		// (element, android:name attribute value) which must ALL be present for
		// the <activity/> to be considered a launcher
		static readonly Dictionary<string, string> LauncherIntentElements = new Dictionary<string, string> {
			{ "action",   "android.intent.action.MAIN" },
			{ "category", "android.intent.category.LAUNCHER" },
		};
		
		public string PackageName { get; set; }
		public List<string> Addons { get; private set; }
		public string ApplicationName { get; set; }
		public string [] Placeholders { get; set; }
		public List<string> Assemblies { get; set; }
		public DirectoryAssemblyResolver Resolver { get; set; }
		public string SdkDir { get; set; }
		public string SdkVersion { get; set; }
		public bool Debug { get; set; }
		public bool MultiDex { get; set; }
		public bool NeedsInternet { get; set; }
		public bool InstantRunEnabled { get; set; }
		public string VersionCode {
			get {
				XAttribute attr = doc.Root.Attribute (androidNs + "versionCode");
				if (attr != null) {
					string code = attr.Value;
					if (!string.IsNullOrEmpty (code))
						return code;
				}
				return "1";
			}
			set {
				doc.Root.SetAttributeValue (androidNs + "versionCode", value);
			}
		}
		public string GetMinimumSdk () {
			int defaultMinSdkVersion = MonoAndroidHelper.SupportedVersions.MinStableVersion.ApiLevel;
			var minAttr = doc.Root.Element ("uses-sdk")?.Attribute (androidNs + "minSdkVersion");
			if (minAttr == null) {
				int minSdkVersion;
				if (!int.TryParse (SdkVersionName, out minSdkVersion))
					minSdkVersion = defaultMinSdkVersion;
				return Math.Min (minSdkVersion, defaultMinSdkVersion).ToString ();
			}
			return minAttr.Value;
		}

		TaskLoggingHelper log;

		public ManifestDocument (string templateFilename, TaskLoggingHelper log) : base ()
		{
			this.log = log;
			Addons = new List<string> ();
			Assemblies = new List<string> ();

			attName = androidNs + "name";

			if (!string.IsNullOrEmpty (templateFilename)) {
				doc = XDocument.Load (templateFilename, LoadOptions.SetLineInfo);
				AndroidResource.UpdateXmlResource (doc.Root);
			} else {
				doc = new XDocument (new XElement ("manifest"));
			}
		}

		string SdkVersionName {
			get { return MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (SdkVersion); }
		}

		string ToFullyQualifiedName (string typeName)
		{
			if (typeName.StartsWith ("."))
				return PackageName + typeName;
			if (typeName.Contains ("."))
				return typeName;
			return PackageName + "." + typeName;
		}

		XElement GetActivityWithName (XElement app, string name)
		{
			name = ToFullyQualifiedName (name);
			return app.Elements ("activity").FirstOrDefault (e => ToFullyQualifiedName ((string) e.Attribute (androidNs + "name")) == name);
		}

		void ReorderElements (XElement app)
		{
			var elements = app.ElementsAfterSelf ("uses-permission");
			foreach (var e in elements) {
				e.Remove ();
				app.AddBeforeSelf (e);
			}
			elements = app.ElementsAfterSelf ("permission");
			foreach (var e in elements) {
				e.Remove ();
				app.AddBeforeSelf (e);
			}
			elements = app.ElementsAfterSelf ("permissionGroup");
			foreach (var e in elements) {
				e.Remove ();
				app.AddBeforeSelf (e);
			}
			elements = app.ElementsAfterSelf ("permissionTree");
			foreach (var e in elements) {
				e.Remove ();
				app.AddBeforeSelf (e);
			}
			elements = app.ElementsAfterSelf ("permission-group");
			foreach (var e in elements) {
				e.Remove ();
				app.AddBeforeSelf (e);
			}
			elements = app.ElementsAfterSelf ("permission-tree");
			foreach (var e in elements) {
				e.Remove ();
				app.AddBeforeSelf (e);
			}
			elements = app.ElementsAfterSelf ("uses-feature");
			foreach (var e in elements) {
				e.Remove ();
				app.AddBeforeSelf (e);
			}
			elements = app.ElementsAfterSelf ("uses-library");
			foreach (var e in elements) {
				e.Remove ();
				app.AddBeforeSelf (e);
			}
			elements = app.ElementsAfterSelf ("supports-gl-texture");
			foreach (var e in elements) {
				e.Remove ();
				app.AddBeforeSelf (e);
			}
		}

		void ReorderActivityAliases (XElement app)
		{
			var aliases = app.Elements ("activity-alias").ToList ();

			foreach (XElement alias in aliases) {
				XAttribute attr = alias.Attribute (androidNs + "targetActivity");
				if (attr == null)
					continue;
				XElement activity = GetActivityWithName (app, attr.Value);
				if (activity != null) {
					alias.Remove ();
					activity.AddAfterSelf (alias);
				} else {
					log.LogWarning ("unable to find target activity for activity alias: " + attr.Value);
				}
			}
		}
		
		public IList<string> Merge (List<TypeDefinition> subclasses, List<string> selectedWhitelistAssemblies, string applicationClass, bool embed, string bundledWearApplicationName, IEnumerable<string> mergedManifestDocuments)
		{
			string applicationName  = ApplicationName;

			var manifest = doc.Root;

			if (manifest == null || manifest.Name != "manifest")
				throw new Exception ("Root element must be 'manifest'");
			
			var manifest_package = (string) manifest.Attribute ("package");

			if (!string.IsNullOrWhiteSpace (manifest_package))
				PackageName = manifest_package;
			
			manifest.SetAttributeValue (XNamespace.Xmlns + "android", "http://schemas.android.com/apk/res/android");
			if (manifest.Attribute (androidNs + "versionCode") == null)
				manifest.SetAttributeValue (androidNs + "versionCode", "1");
			if (manifest.Attribute (androidNs + "versionName") == null)
				manifest.SetAttributeValue (androidNs + "versionName", "1.0");
			
			app = CreateApplicationElement (manifest, applicationClass, subclasses, selectedWhitelistAssemblies);
			
			if (app.Attribute (androidNs + "label") == null && applicationName != null)
				app.SetAttributeValue (androidNs + "label", applicationName);

			var existingTypes = new HashSet<string> (
				app.Descendants ().Select (a => (string) a.Attribute (attName)).Where (v => v != null));
			
			if (!string.IsNullOrEmpty (bundledWearApplicationName)) {
				if (!app.Elements ("meta-data").Any (e => e.Attributes (androidNs + "name").Any (a => a.Value == bundledWearApplicationName)))
					app.Add (new XElement ("meta-data", new XAttribute (androidNs + "name", "com.google.android.wearable.beta.app"), new XAttribute (androidNs + "resource", "@xml/wearable_app_desc")));
			}

			// If no <uses-sdk> is specified, add it with both minSdkVersion and
			// targetSdkVersion set to TargetFrameworkVersion
			if (!manifest.Elements ("uses-sdk").Any ()) {
				manifest.AddFirst (
						new XElement ("uses-sdk",
							new XAttribute (androidNs + "minSdkVersion", SdkVersionName),
							new XAttribute (androidNs + "targetSdkVersion", SdkVersionName)));
			}

			// If no minSdkVersion is specified, set it to TargetFrameworkVersion
			var uses = manifest.Element ("uses-sdk");

			if (uses.Attribute (androidNs + "minSdkVersion") == null) {
				int minSdkVersion;
				if (!int.TryParse (SdkVersionName, out minSdkVersion))
					minSdkVersion = XABuildConfig.NDKMinimumApiAvailable;
				minSdkVersion = Math.Min (minSdkVersion, XABuildConfig.NDKMinimumApiAvailable);
				uses.SetAttributeValue (androidNs + "minSdkVersion", minSdkVersion.ToString ());
			}

			string targetSdkVersion;
			var tsv = uses.Attribute (androidNs + "targetSdkVersion");
			if (tsv != null)
				targetSdkVersion = tsv.Value;
			else {
				targetSdkVersion = SdkVersionName;
				uses.AddBeforeSelf (new XComment ("suppress UsesMinSdkAttributes"));
			}

			int? tryTargetSdkVersion  = MonoAndroidHelper.SupportedVersions.GetApiLevelFromId (targetSdkVersion);
			if (!tryTargetSdkVersion.HasValue)
				throw new InvalidOperationException (string.Format ("The targetSdkVersion ({0}) is not a valid API level", targetSdkVersion));
			int targetSdkVersionValue = tryTargetSdkVersion.Value;

			foreach (var t in subclasses) {
				if (t.IsAbstract)
					continue;

				if (PackageName == null)
					PackageName = t.Namespace;

				var name        = JavaNativeTypeManager.ToJniName (t).Replace ('/', '.');
				var compatName  = JavaNativeTypeManager.ToCompatJniName (t).Replace ('/', '.');
				if (((string) app.Attribute (attName)) == compatName) {
					app.SetAttributeValue (attName, name);
				}

				Func<TypeDefinition, string, int, XElement> generator = GetGenerator (t);
				if (generator == null)
					continue;

				try {
					// activity not present: create a launcher for it IFF it has attribute
					if (!existingTypes.Contains (name) && !existingTypes.Contains (compatName)) {
						XElement fromCode = generator (t, name, targetSdkVersionValue);
						if (fromCode == null)
							continue;

						IEnumerable <MethodDefinition> constructors = t.Methods.Where (m => m.IsConstructor).Cast<MethodDefinition> ();
						if (!constructors.Any (c => !c.HasParameters && c.IsPublic)) {
							string message = $"The type '{t.FullName}' must provide a public default constructor";
							SequencePoint sourceLocation = FindSource (constructors);

							if (sourceLocation != null && sourceLocation.Document?.Url != null) {
								log.LogError (
									subcategory:      String.Empty,
									errorCode:        "XA4213",
									helpKeyword:      String.Empty,
									file:             sourceLocation.Document.Url,
									lineNumber:       sourceLocation.StartLine,
									columnNumber:     sourceLocation.StartColumn,
									endLineNumber:    sourceLocation.EndLine,
									endColumnNumber:  sourceLocation.EndColumn,
									message:          message);
							} else
								log.LogCodedError ("XA4213", message);
							continue;
						}
						app.Add (fromCode);
					}
					foreach (var d in app.Descendants ().Where (a => ((string) a.Attribute (attName)) == compatName)) {
						d.SetAttributeValue (attName, name);
					}
				} catch (InvalidActivityNameException ex) {
					log.LogErrorFromException (ex);
				}
			}

			var icon = app.Attribute (androidNs + "icon");
			if (icon == null) {
				var activity = app.Element ("activity");
				if (activity != null) {
					var activityIcon = activity.Attribute (androidNs + "icon");
					if (activityIcon != null)
						app.Add (new XAttribute (androidNs + "icon", activityIcon.Value));
				}
			}

			PackageName = AndroidAppManifest.CanonicalizePackageName (PackageName);

			if (!PackageName.Contains ('.'))
				throw new InvalidOperationException ("/manifest/@package attribute MUST contain a period ('.').");
			
			manifest.SetAttributeValue ("package", PackageName);

			if (MultiDex)
				app.Add (CreateMonoRuntimeProvider ("mono.android.MultiDexLoader", null, initOrder: --AppInitOrder));

			var providerNames = AddMonoRuntimeProviders (app);

			if (Debug && !embed && InstantRunEnabled) {
				if (int.TryParse (SdkVersion, out int apiLevel) && apiLevel >= 19)
					app.Add (CreateMonoRuntimeProvider ("mono.android.ResourcePatcher", null, initOrder: --AppInitOrder));
			}
			if (Debug) {
				app.Add (new XComment ("suppress ExportedReceiver"));
				app.Add (new XElement ("receiver",
						new XAttribute (androidNs + "name", "mono.android.Seppuku"),
						new XElement ("intent-filter",
							new XElement ("action",
								new XAttribute (androidNs + "name", "mono.android.intent.action.SEPPUKU")),
							new XElement ("category",
								new XAttribute (androidNs + "name", "mono.android.intent.category.SEPPUKU." + PackageName)))));
				if (app.Attribute (androidNs + "debuggable") == null)
					app.Add (new XAttribute (androidNs + "debuggable", "true"));
			}
			if (Debug || NeedsInternet)
				AddInternetPermissionForDebugger ();

			if (!embed)
				AddFastDeployPermissions ();

			AddAddOns (app, SdkDir, SdkVersionName, Addons);

			// If the manifest has android:installLocation, but we are targeting
			// API 7 or lower, remove it for the user and show a warning
			if (manifest.Attribute (androidNs + "installLocation") != null) {
				if (targetSdkVersionValue < 8) {
					manifest.Attribute (androidNs + "installLocation").Remove ();
					Console.Error.WriteLine ("monodroid: warning 1 : installLocation cannot be specified for Android versions less than 2.2.  Attribute installLocation ignored.");
				}
			}

			AddInstrumentations (manifest, subclasses, targetSdkVersionValue);
			AddPermissions (app, selectedWhitelistAssemblies);
			AddPermissionGroups (app, selectedWhitelistAssemblies);
			AddPermissionTrees (app, selectedWhitelistAssemblies);
			AddUsesPermissions (app, selectedWhitelistAssemblies);
			AddUsesFeatures (app, selectedWhitelistAssemblies);
			AddSupportsGLTextures (app, selectedWhitelistAssemblies);

			ReorderActivityAliases (app);
			ReorderElements (app);

			if (mergedManifestDocuments != null) {
				foreach (var mergedManifest in mergedManifestDocuments) {
					try {
						MergeLibraryManifest (mergedManifest);
					} catch (Exception ex) {
						log.LogCodedWarning ("XA4302", "Unhandled exception merging `AndroidManifest.xml`: {0}", ex);
					}
				}
			}

			return providerNames;

			SequencePoint FindSource (IEnumerable<MethodDefinition> methods)
			{
				if (methods == null)
					return null;

				SequencePoint ret = null;
				foreach (MethodDefinition method in methods.Where (m => m != null && m.HasBody && m.DebugInformation != null)) {
					foreach (Instruction ins in method.Body.Instructions) {
						SequencePoint seq = method.DebugInformation.GetSequencePoint (ins);
						if (seq == null)
							continue;

						if (ret == null || seq.StartLine < ret.StartLine)
							ret = seq;
						break;
					}
				}

				return ret;
			}
		}

		// FIXME: our manifest merger is hacky.
		// To support complete manifest merger, we will have to implement fairly complicated one, described at
		// http://tools.android.com/tech-docs/new-build-system/user-guide/manifest-merger
		void MergeLibraryManifest (string mergedManifest)
		{
			var nsResolver = new XmlNamespaceManager (new NameTable ());
			nsResolver.AddNamespace ("android", androidNs.NamespaceName);
			var xdoc = XDocument.Load (mergedManifest);
			var package = xdoc.Root.Attribute ("package")?.Value ?? string.Empty;
			foreach (var top in xdoc.XPathSelectElements ("/manifest/*")) {
				var name = top.Attribute (AndroidXmlNamespace.GetName ("name"));
				var existing = (name != null) ?
					doc.XPathSelectElement (string.Format ("/manifest/{0}[@android:name='{1}']", top.Name.LocalName, name.Value), nsResolver) :
					doc.XPathSelectElement (string.Format ("/manifest/{0}", top.Name.LocalName));
				if (existing != null)
					// if there is existing node with the same android:name, then append contents to existing node.
					existing.Add (FixupNameElements (package, top.Nodes ()));
				else
					// otherwise, just add to the doc.
					doc.Root.Add (FixupNameElements (package, new XNode [] { top }));
			}
		}

		public IEnumerable<XElement> ResolveDuplicates (IEnumerable<XElement> elements)
		{
			foreach (var e in elements)
				foreach (var d in ResolveDuplicates (e.Elements ()))
					yield return d;
			foreach (var d in elements.GroupBy (x => x.ToFullString ()).SelectMany (x => x.Skip (1)))
				yield return d;
		}

		void RemoveDuplicateElements ()
		{
			var duplicates = ResolveDuplicates (doc.Elements ());
			foreach (var duplicate in duplicates)
				duplicate.Remove ();
			
		}

		IEnumerable<XNode> FixupNameElements(string packageName, IEnumerable<XNode> nodes)
		{
			foreach (var element in nodes.Select ( x => x as XElement).Where (x => x != null && ManifestAttributeFixups.ContainsKey (x.Name.LocalName))) {
				var attributes = ManifestAttributeFixups [element.Name.LocalName];
				foreach (var attr in element.Attributes ().Where (x => attributes.Contains (x.Name.LocalName))) {
					var typeName = attr.Value;
					attr.Value = typeName.StartsWith (".", StringComparison.InvariantCultureIgnoreCase) ? packageName + typeName : typeName;
				}
			}
			return nodes;
		}

		Func<TypeDefinition, string, int, XElement> GetGenerator (TypeDefinition type)
		{
			if (type.IsSubclassOf ("Android.App.Activity"))
				return ActivityFromTypeDefinition;
			if (type.IsSubclassOf ("Android.App.Service"))
				return (t, name, v) => ToElement (t, name, ServiceAttribute.FromTypeDefinition, x => x.ToElement (PackageName));
			if (type.IsSubclassOf ("Android.Content.BroadcastReceiver"))
				return (t, name, v) => ToElement (t, name, BroadcastReceiverAttribute.FromTypeDefinition, x => x.ToElement (PackageName));
			if (type.IsSubclassOf ("Android.Content.ContentProvider"))
				return (t, name, v) => ToProviderElement (t, name);
			return null;
		}

		XElement CreateApplicationElement (XElement manifest, string applicationClass, List<TypeDefinition> subclasses, List<string> selectedWhitelistAssemblies)
		{
			var application = manifest.Descendants ("application").FirstOrDefault ();

			List<ApplicationAttribute> assemblyAttr = 
				Assemblies.Select (path => ApplicationAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path)))
				.Where (attr => attr != null)
				.ToList ();
			List<MetaDataAttribute> metadata = 
				Assemblies.SelectMany (path => MetaDataAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path)))
					.Where (attr => attr != null)
					.ToList ();
			var usesLibraryAttr = 
				Assemblies.Concat (selectedWhitelistAssemblies).SelectMany (path => UsesLibraryAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path)))
				.Where (attr => attr != null);
			var usesConfigurationAttr =
				Assemblies.Concat (selectedWhitelistAssemblies).SelectMany (path => UsesConfigurationAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path)))
				.Where (attr => attr != null);
			if (assemblyAttr.Count > 1)
				throw new InvalidOperationException ("There can be only one [assembly:Application] attribute defined.");

			List<ApplicationAttribute> typeAttr = new List<ApplicationAttribute> ();
			List<UsesLibraryAttribute> typeUsesLibraryAttr = new List<UsesLibraryAttribute> ();
			List<UsesConfigurationAttribute> typeUsesConfigurationAttr = new List<UsesConfigurationAttribute> ();
			foreach (var t in subclasses) {
				ApplicationAttribute aa = ApplicationAttribute.FromCustomAttributeProvider (t);
				if (aa == null)
					continue;

				if (!t.IsSubclassOf ("Android.App.Application"))
					throw new InvalidOperationException (string.Format ("Found [Application] on type {0}.  [Application] can only be used on subclasses of Application.", t.FullName));

				typeAttr.Add (aa);
				metadata.AddRange (MetaDataAttribute.FromCustomAttributeProvider (t));
				
				typeUsesLibraryAttr.AddRange (UsesLibraryAttribute.FromCustomAttributeProvider (t));
			}

			if (typeAttr.Count > 1)
				throw new InvalidOperationException ("There can be only one type with an [Application] attribute; found: " +
						string.Join (", ", typeAttr.Select (aa => aa.Name).ToArray ()));

			if (assemblyAttr.Count > 0 && typeAttr.Count > 0)
				throw new InvalidOperationException ("Application cannot have both a type with an [Application] attribute and an [assembly:Application] attribute.");

			ApplicationAttribute appAttr = assemblyAttr.SingleOrDefault () ?? typeAttr.SingleOrDefault ();
			var ull1 = usesLibraryAttr ?? new UsesLibraryAttribute [0];
			var ull2 = typeUsesLibraryAttr.AsEnumerable () ?? new UsesLibraryAttribute [0];
			var usesLibraryAttrs = ull1.Concat (ull2);
			var ucl1 = usesConfigurationAttr ?? new UsesConfigurationAttribute [0];
			var ucl2 = typeUsesConfigurationAttr.AsEnumerable () ?? new UsesConfigurationAttribute [0];
			var usesConfigurationattrs = ucl1.Concat (ucl2);
			bool needManifestAdd = true;

			if (appAttr != null) {
				var newapp = appAttr.ToElement (Resolver, PackageName);
				if (application == null)
					application = newapp;
				else {
					needManifestAdd = false;
					foreach (var n in newapp.Attributes ())
						application.SetAttributeValue (n.Name, n.Value);
					foreach (var n in newapp.Nodes ())
						application.Add (n);
				}
			}
			else if (application == null)
				application = new XElement ("application");
			else
				needManifestAdd = false;
			application.Add (metadata.Select (md => md.ToElement (PackageName)));

			if (needManifestAdd)
				manifest.Add (application);
			
			AddUsesLibraries (application, usesLibraryAttrs);
			AddUsesConfigurations (application, usesConfigurationattrs);

			if (applicationClass != null && application.Attribute (androidNs + "name") == null)
				application.Add (new XAttribute (androidNs + "name", applicationClass));
				
			if (application.Attribute (androidNs + "allowBackup") == null)
				application.Add (new XAttribute (androidNs + "allowBackup", "true"));

			return application;
		}

		IList<string> AddMonoRuntimeProviders (XElement app)
		{
			app.Add (CreateMonoRuntimeProvider ("mono.MonoRuntimeProvider", null, --AppInitOrder));

			var providerNames = new List<string> ();

			var processAttrName = androidNs.GetName ("process");
			var procs = new List<string> ();
			foreach (XElement el in app.Elements ()) {
				var proc = el.Attribute (processAttrName);
				if (proc == null)
					continue;
				if (procs.Contains (proc.Value))
					continue;
				procs.Add (proc.Value);
				if (el.Name.NamespaceName != String.Empty)
					continue;
				switch (el.Name.LocalName) {
				case "provider":
					var autho = el.Attribute (androidNs.GetName ("authorities"));
					if (autho != null && autho.Value.EndsWith (".__mono_init__"))
						continue;
					goto case "activity";
				case "activity":
				case "receiver":
				case "service":
					string providerName = "MonoRuntimeProvider_" + procs.Count;
					providerNames.Add (providerName);
					app.Add (CreateMonoRuntimeProvider ("mono." + providerName, proc.Value, --AppInitOrder));
					break;
				}
			}

			return providerNames;
		}

		int AppInitOrder = 2000000000;

		XElement CreateMonoRuntimeProvider (string name, string processName, int initOrder)
		{
			var directBootAware = DirectBootAware ();
			return new XElement ("provider",
						new XAttribute (androidNs + "name", name),
						new XAttribute (androidNs + "exported", "false"),
						new XAttribute (androidNs + "initOrder", initOrder),
						directBootAware ? new XAttribute (androidNs + "directBootAware", "true") : null,
						processName == null ? null : new XAttribute (androidNs + "process", processName),
						new XAttribute (androidNs + "authorities", PackageName + "." + name + ".__mono_init__"));
		}

		bool IsMainLauncher (XElement intentFilter)
		{
			return LauncherIntentElements.All (entry => 
					intentFilter.Elements (entry.Key).Any (e => ((string) e.Attribute (attName) == entry.Value)));
		}

		/// <summary>
		/// Returns the value of //application/@android:extractNativeLibs.
		/// </summary>
		public bool ExtractNativeLibraries ()
		{
			string text = app?.Attribute (androidNs + "extractNativeLibs")?.Value;
			if (bool.TryParse (text, out bool value)) {
				return value;
			}

			// If android:extractNativeLibs is omitted, returns true.
			return true;
		}

		/// <summary>
		/// Returns true if an element has the @android:directBootAware attribute and its 'true'
		/// </summary>
		public bool DirectBootAware ()
		{
			var processAttrName = androidNs.GetName ("directBootAware");
			var appAttr = app.Attribute (processAttrName);
			bool value;
			if (appAttr != null && bool.TryParse (appAttr.Value, out value) && value)
				return true;
			foreach (XElement el in app.Elements ()) {
				var elAttr = el.Attribute (processAttrName);
				if (elAttr != null && bool.TryParse (elAttr.Value, out value) && value)
					return true;
			}

			// If android:directBootAware is omitted, returns false.
			return false;
		}

		XElement ActivityFromTypeDefinition (TypeDefinition type, string name, int targetSdkVersion)
		{
			if (name.StartsWith ("_"))
				throw new InvalidActivityNameException (string.Format ("Activity name '{0}' is invalid, because activity namespaces may not begin with an underscore.", type.FullName));

			return ToElement (type, name, 
					ActivityAttribute.FromTypeDefinition, 
					aa => aa.ToElement (Resolver, PackageName, targetSdkVersion), 
					(aa, element) => {
						if (aa.MainLauncher)
							AddLauncherIntentElements (element);
						var la = LayoutAttribute.FromTypeDefinition (type);
						if (la != null)
							element.Add (la.ToElement (Resolver, PackageName));
					});
		}

		XElement InstrumentationFromTypeDefinition (TypeDefinition type, string name, int targetSdkVersion)
		{
			return ToElement (type, name, 
					t => InstrumentationAttribute.FromCustomAttributeProvider (t).FirstOrDefault (),
					ia => {
						if (ia.TargetPackage == null)
							ia.SetTargetPackage (PackageName);
						return ia.ToElement (PackageName);
					});
		}

		XElement ToElement<TAttribute> (TypeDefinition type, string name, Func<TypeDefinition, TAttribute> parser, Func<TAttribute, XElement> toElement)
			where TAttribute : class
		{
			return ToElement (type, name, parser, toElement, null);
		}

		XElement ToElement<TAttribute> (TypeDefinition type, string name, Func<TypeDefinition, TAttribute> parser, Func<TAttribute, XElement> toElement, Action<TAttribute, XElement> update)
			where TAttribute : class
		{
			TAttribute attr = parser (type);
			if (attr == null)
				return null;

			IEnumerable<MetaDataAttribute> metadata = MetaDataAttribute.FromCustomAttributeProvider (type);
			IEnumerable<IntentFilterAttribute> intents = IntentFilterAttribute.FromTypeDefinition (type);

			XElement element = toElement (attr);
			if (element.Attribute (attName) == null)
				element.Add (new XAttribute (attName, name));
			element.Add (metadata.Select (md => md.ToElement (PackageName)));
			element.Add (intents.Select (intent => intent.ToElement (PackageName)));
			if (update != null)
				update (attr, element);
			return element;
		}

		XElement ToProviderElement (TypeDefinition type, string name)
		{
			var attr = ContentProviderAttribute.FromTypeDefinition (type);
			if (attr == null)
				return null;

			IEnumerable<MetaDataAttribute> metadata = MetaDataAttribute.FromCustomAttributeProvider (type);
			IEnumerable<GrantUriPermissionAttribute> grants = GrantUriPermissionAttribute.FromTypeDefinition (type);
			IEnumerable<IntentFilterAttribute> intents = IntentFilterAttribute.FromTypeDefinition (type);

			XElement element = attr.ToElement (PackageName);
			if (element.Attribute (attName) == null)
				element.Add (new XAttribute (attName, name));
			element.Add (metadata.Select (md => md.ToElement (PackageName)));
			element.Add (grants.Select (intent => intent.ToElement (PackageName)));
			element.Add (intents.Select (intent => intent.ToElement (PackageName)));

			return element;
		}

		void AddLauncherIntentElements (XElement activity)
		{
			if (activity.Elements ("intent-filter").Any (f => IsMainLauncher (f)))
				return;

			var filter = new XElement ("intent-filter");
			activity.AddFirst (filter);
			foreach (KeyValuePair<string, string> e in LauncherIntentElements) {
				if (!filter.Elements (e.Key).Any (x => ((string) x.Attribute (attName)) == e.Value))
					filter.Add (new XElement (e.Key, new XAttribute (attName, e.Value)));
			}
		}
		
		internal static void AddAddOns (XElement app, string sdkDir, string sdkVersionName, IList<string> addonList)
		{
			List<AndroidAddOnManifest> manifests = AndroidAddOnManifest.GetAddOnManifests (sdkDir).ToList ();
			foreach (string library in app.Elements ("uses-library")
					.Select (ul => {
						var n = (string) ul.Attribute (androidNs + "name");
						return n != null ? n.Trim () : n; 
					})
					.Where (ul => !string.IsNullOrEmpty (ul))) {
				AndroidAddOn addOn = GetAddOn (manifests, sdkVersionName, library);
				// uses-library could be used to specify such library that does not exist in
				// application or even on the host (even if "required" is true). The target
				// may contain that library. "android.test.runner" is such an example.
				if (addOn != null)
					addonList.Add (addOn.JarPath);
			}
		}
		
		static AndroidAddOn GetAddOn (List<AndroidAddOnManifest> manifests, string sdkVersionName, string libraryName)
		{
			// try exact match
			AndroidAddOn addon = manifests.SelectMany (manifest => manifest.Libraries)
				.Where (ao => ao.Name == libraryName && ao.ApiLevel == sdkVersionName)
				.FirstOrDefault ();
			if (addon != null)
				return addon;
			
			// Try to grab an addon with level <= sdkVersion
			// Requires that sdkVersion & AndroidAddOn.ApiLevel be convertible to System.Int32.
			//
			// So far preview L does not come up with google add-on, so it's OK to leave it unsupported.
			int targetLevel;
			if (!int.TryParse (sdkVersionName, out targetLevel)) {
				return null;
			}
			int curLevel = int.MinValue;
			foreach (AndroidAddOn attempt in manifests.SelectMany (manifest => manifest.Libraries)
					.Where (ao => ao.Name == libraryName)) {
				int level;
				if (!int.TryParse (attempt.ApiLevel, out level))
					continue;
				if (curLevel > level)
					continue;
				if (level > targetLevel)
					continue;
				curLevel = level;
				addon = attempt;
			}
			return addon;
		}

		public void AddInternetPermissionForDebugger ()
		{
			const string permInternet ="android.permission.INTERNET";
			if (!doc.Root.Descendants ("uses-permission").Any (x => (string)x.Attribute (attName) == permInternet))
				app.AddBeforeSelf (new XElement ("uses-permission", new XAttribute (attName, permInternet)));
		}

		public void AddFastDeployPermissions ()
		{
			const string permReadExternalStorage  ="android.permission.READ_EXTERNAL_STORAGE";
			if (!doc.Root.Descendants ("uses-permission").Any (x => (string)x.Attribute (attName) == permReadExternalStorage))
				app.AddBeforeSelf (new XElement ("uses-permission", new XAttribute (attName, permReadExternalStorage)));
		}

		void AddPermissions (XElement application, List<string> selectedWhitelistAssemblies)
		{
			// Look in user assemblies + whitelist (like Maps)
			var check_assemblies = Assemblies.Union (selectedWhitelistAssemblies);

			var assemblyAttrs = 
				check_assemblies.SelectMany (path => PermissionAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path)));
			// Add unique permissions to the manifest
			foreach (var pa in assemblyAttrs.Distinct (new PermissionAttribute.PermissionAttributeComparer ()))
				if (!application.Parent.Descendants ("permission").Any (x => (string)x.Attribute (attName) == pa.Name))
					application.AddBeforeSelf (pa.ToElement (PackageName));
		}

		void AddPermissionGroups (XElement application, List<string> selectedWhitelistAssemblies)
		{
			// Look in user assemblies + whitelist (like Maps)
			var check_assemblies = Assemblies.Union (selectedWhitelistAssemblies);

			var assemblyAttrs = 
				check_assemblies.SelectMany (path => PermissionGroupAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path)));

			// Add unique permissionGroups to the manifest
			foreach (var pga in assemblyAttrs.Distinct (new PermissionGroupAttribute.PermissionGroupAttributeComparer ()))
				if (!application.Parent.Descendants ("permissionGroup").Any (x => (string)x.Attribute (attName) == pga.Name))
					application.AddBeforeSelf (pga.ToElement (PackageName));
		}

		void AddPermissionTrees (XElement application, List<string> selectedWhitelistAssemblies)
		{
			// Look in user assemblies + whitelist (like Maps)
			var check_assemblies = Assemblies.Union (selectedWhitelistAssemblies);

			var assemblyAttrs = 
				check_assemblies.SelectMany (path => PermissionTreeAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path)));

			// Add unique permissionGroups to the manifest
			foreach (var pta in assemblyAttrs.Distinct (new PermissionTreeAttribute.PermissionTreeAttributeComparer ()))
				if (!application.Parent.Descendants ("permissionTree").Any (x => (string)x.Attribute (attName) == pta.Name))
					application.AddBeforeSelf (pta.ToElement (PackageName));
		}

		void AddUsesPermissions (XElement application, List<string> selectedWhitelistAssemblies)
		{
			// Look in user assemblies + whitelist (like Maps)
			var check_assemblies = Assemblies.Union (selectedWhitelistAssemblies);

			var assemblyAttrs = 
				check_assemblies.SelectMany (path => UsesPermissionAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path)));

			// Add unique permissions to the manifest
			foreach (var upa in assemblyAttrs.Distinct (new UsesPermissionAttribute.UsesPermissionComparer ()))
				if (!application.Parent.Descendants ("uses-permission").Any (x => (string)x.Attribute (attName) == upa.Name))
					application.AddBeforeSelf (upa.ToElement (PackageName));
		}

		void AddUsesConfigurations (XElement application, IEnumerable<UsesConfigurationAttribute> configs)
		{
			foreach (var uca in configs)
				application.Add (uca.ToElement (PackageName));
		}

		void AddUsesLibraries (XElement application, IEnumerable<UsesLibraryAttribute> libraries)
		{
			// Add unique libraries to the manifest
			foreach (var ula in libraries)
				if (!application.Descendants ("uses-library").Any (x => (string)x.Attribute (attName) == ula.Name))
					application.Add (ula.ToElement (PackageName));
		}

		void AddUsesFeatures (XElement application, List<string> selectedWhitelistAssemblies)
		{
			// Look in user assemblies + whitelist (like Maps)
			var check_assemblies = Assemblies.Union (selectedWhitelistAssemblies);

			var assemblyAttrs = 
				check_assemblies.SelectMany (path => UsesFeatureAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path)));

			// Add unique features by Name or glESVersion to the manifest
			foreach (var feature in assemblyAttrs) {
				if (!string.IsNullOrEmpty(feature.Name) && feature.GLESVersion == 0) {
					if (!application.Parent.Descendants ("uses-feature").Any (x => (string)x.Attribute (attName) == feature.Name)) {
						application.AddBeforeSelf (feature.ToElement (PackageName));
					}
				}
				if (feature.GLESVersion != 0){
					if (!application.Parent.Descendants ("uses-feature").Any (x => (string)x.Attribute (androidNs+"glEsVersion") == feature.GLESVesionAsString())) {
						application.AddBeforeSelf (feature.ToElement (PackageName));
					}
				}
				
			}
		}

		void AddSupportsGLTextures (XElement application, List<string> selectedWhitelistAssemblies)
		{
			// Look in user assemblies + whitelist (like Maps)
			var check_assemblies = Assemblies.Union (selectedWhitelistAssemblies);

			var assemblyAttrs = 
				check_assemblies.SelectMany (path => SupportsGLTextureAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path)));

			// Add unique items by Name to the manifest
			foreach (var feature in assemblyAttrs) {
				if (!application.Parent.Descendants ("supports-gl-texture").Any (x => (string)x.Attribute (attName) == feature.Name)) {
					application.AddBeforeSelf (feature.ToElement (PackageName));
				}
			}
		}

		void AddInstrumentations (XElement manifest, IList<TypeDefinition> subclasses, int targetSdkVersion)
		{
			var assemblyAttrs = 
				Assemblies.SelectMany (path => InstrumentationAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path)));

			// Add instrumentation to the manifest
			foreach (var ia in assemblyAttrs) {
				if (ia.TargetPackage == null)
					ia.SetTargetPackage (PackageName);
				if (!manifest.Descendants ("instrumentation").Any (x => (string) x.Attribute (attName) == ia.Name))
					manifest.Add (ia.ToElement (PackageName));
			}
			
			foreach (var type in subclasses)
				if (type.IsSubclassOf ("Android.App.Instrumentation")) {
					var xe = InstrumentationFromTypeDefinition (type, JavaNativeTypeManager.ToJniName (type).Replace ('/', '.'), targetSdkVersion);
					if (xe != null)
						manifest.Add (xe);
				}
		}
		
		public void Save (string filename)
		{
			using (var file = new StreamWriter (filename, append: false, encoding: new UTF8Encoding (false)))
				Save (file);
		}

		public void Save (Stream stream)
		{
			using (var file = new StreamWriter (stream, new UTF8Encoding (false), bufferSize: 1024, leaveOpen: true))
				Save (file);
		}

		public void Save (System.IO.TextWriter stream)
		{
			RemoveDuplicateElements ();
			var ms = new MemoryStream ();
			doc.Save (ms);
			ms.Flush ();
			ms.Position = 0;
			var s = new StreamReader (ms).ReadToEnd ();
			if (ApplicationName != null)
				s = s.Replace ("${applicationId}", ApplicationName);
			if (Placeholders != null)
				foreach (var entry in Placeholders.Select (e => e.Split (new char [] {'='}, 2, StringSplitOptions.None))) {
					if (entry.Length == 2)
						s = s.Replace ("${" + entry [0] + "}", entry [1]);
					else
						log.LogWarning ("Invalid application placeholders (AndroidApplicationPlaceholders) value. Use 'key1=value1;key2=value2, ...' format. The specified value was: " + Placeholders);
				}
			stream.Write (s);
		}

		public string GetLaunchableActivityName ()
		{
			var application = doc.Root.Descendants ("application").FirstOrDefault ();
			var aName = androidNs + "name";
			foreach (var activity in application.Elements ("activity")) {
				var filter = activity.Element ("intent-filter");
				if (filter != null) {
					foreach (var category in filter.Elements ("category"))
						if (category != null && (string)category.Attribute (aName) == "android.intent.category.LAUNCHER")
							return (string) activity.Attribute (aName);
				}
			}
			return null;
		}

		static int GetAbiCode (string abi)
		{
			switch (abi) {
			case "armeabi-v7a":
				return 2;
			case "x86":
				return 3;
			case "arm64-v8a":
				return 4;
			case "x86_64":
				return 5;
			default:
				throw new ArgumentOutOfRangeException ("abi", "unsupported ABI");
			}
		}

		public void SetAbi (string abi)
		{
			int code = 1;
			if (!string.IsNullOrEmpty (VersionCode)) {
				code = Convert.ToInt32 (VersionCode);
				if (code > maxVersionCode || code < 0)
					throw new ArgumentOutOfRangeException ("VersionCode", $"VersionCode is outside 0, {maxVersionCode} interval");
			}
			code |= GetAbiCode (abi) << 16;
			VersionCode = code.ToString ();
		}

		public void CalculateVersionCode (string currentAbi, string versionCodePattern, string versionCodeProperties)
		{
			var regex = new Regex ("\\{(?<key>([A-Za-z]+)):?[D0-9]*[\\}]");
			var kvp = new Dictionary<string, int> ();
			foreach (var item in versionCodeProperties?.Split (new char [] { ';', ':' }) ?? new string [0]) {
				var keyValue = item.Split (new char [] { '=' });
				int val;
				if (!int.TryParse (keyValue [1], out val))
					continue;
				kvp.Add (keyValue [0], val);
			}
			if (!kvp.ContainsKey ("abi") && !string.IsNullOrEmpty (currentAbi))
				kvp.Add ("abi", GetAbiCode (currentAbi));
			if (!kvp.ContainsKey ("versionCode"))
				kvp.Add ("versionCode", int.Parse (VersionCode));
			if (!kvp.ContainsKey ("minSDK")) {
				kvp.Add ("minSDK", int.Parse (GetMinimumSdk ()));
			}
			var versionCode = String.Empty;
			foreach (Match match in regex.Matches (versionCodePattern)) {
				var key = match.Groups ["key"].Value;
				var format = match.Value.Replace (key, "0");
				if (!kvp.ContainsKey (key))
					continue;
				versionCode += string.Format (format, kvp [key]);
			}
			int code;
			if (!int.TryParse (versionCode, out code))
				throw new ArgumentOutOfRangeException ("VersionCode", $"VersionCode {versionCode} is invalid. It must be an integer value.");
			if (code > maxVersionCode || code < 0)
				throw new ArgumentOutOfRangeException ("VersionCode", $"VersionCode {code} is outside 0, {maxVersionCode} interval");
			VersionCode = versionCode.TrimStart ('0');
		}
	}
}
