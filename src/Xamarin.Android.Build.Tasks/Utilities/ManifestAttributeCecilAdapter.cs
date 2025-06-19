using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Android.App;
using Android.Content;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Xamarin.Android.Tasks;

class ManifestAttributeCecilAdapter
{
	public static XNamespace AndroidXmlNamespace = "http://schemas.android.com/apk/res/android";
	static XNamespace androidNs = AndroidXmlNamespace;
	XName attName;
	string packageName = string.Empty;

	// (element, android:name attribute value) which must ALL be present for
	// the <activity/> to be considered a launcher
	static readonly Dictionary<string, string> LauncherIntentElements = new Dictionary<string, string> {
			{ "action",   "android.intent.action.MAIN" },
			{ "category", "android.intent.category.LAUNCHER" },
		};

	public List<string> Assemblies { get; }
	public TypeDefinitionCache Cache { get; }
	public TaskLoggingHelper Log { get; }
	public IAssemblyResolver Resolver { get; }
	public IList<TypeDefinition> Subclasses { get; }

	public ManifestAttributeCecilAdapter (List<string> assemblies, TypeDefinitionCache cache, IAssemblyResolver resolver, IList<TypeDefinition> subclasses, TaskLoggingHelper log)
	{
		attName = androidNs + "name";

		Assemblies = assemblies;
		Cache = cache;
		Log = log;
		Resolver = resolver;
		Subclasses = subclasses;
	}

	public List<InstrumentationAttribute> GetAssemblyInstrumentationAttributes ()
		=> Assemblies.SelectMany (path => InstrumentationAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path), Cache)).ToList ();

	public List<ApplicationAttribute> GetAssemblyApplicationAttributes ()
		=> Assemblies.Select (path => ApplicationAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path), Cache)).WhereNotNull ().ToList ();

	public List<MetaDataAttribute> GetAssemblyMetaDataAttributes ()
		=> Assemblies.SelectMany (path => MetaDataAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path), Cache)).ToList ();

	public List<PropertyAttribute> GetAssemblyPropertyAttributes ()
		=> Assemblies.SelectMany (path => PropertyAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path), Cache)).ToList ();

	public List<UsesLibraryAttribute> GetAssemblyUsesLibraryAttributes ()
		=> Assemblies.SelectMany (path => UsesLibraryAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path), Cache)).ToList ();

	public List<UsesConfigurationAttribute> GetAssemblyUsesConfigurationAttributes ()
		=> Assemblies.SelectMany (path => UsesConfigurationAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path), Cache)).ToList ();

	public List<PermissionAttribute> GetAssemblyPermissionAttributes ()
		=> Assemblies.SelectMany (path => PermissionAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path), Cache)).ToList ();

	public List<PermissionGroupAttribute> GetAssemblyPermissionGroupAttributes ()
		=> Assemblies.SelectMany (path => PermissionGroupAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path), Cache)).ToList ();

	public List<PermissionTreeAttribute> GetAssemblyPermissionTreeAttributes ()
		=> Assemblies.SelectMany (path => PermissionTreeAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path), Cache)).ToList ();

	public List<UsesPermissionAttribute> GetAssemblyUsesPermissionAttributes ()
		=> Assemblies.SelectMany (path => UsesPermissionAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path), Cache)).ToList ();

	public List<UsesFeatureAttribute> GetAssemblyUsesFeatureAttributes ()
		=> Assemblies.SelectMany (path => UsesFeatureAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path), Cache)).ToList ();

	public List<SupportsGLTextureAttribute> GetAssemblySupportsGLTextureAttributes ()
		=> Assemblies.SelectMany (path => SupportsGLTextureAttribute.FromCustomAttributeProvider (Resolver.GetAssembly (path), Cache)).ToList ();

	public List<ManifestAttributeWithMetadata<InstrumentationAttribute>> GetTypeInstrumentationAttributes ()
	{
		var results = new List<ManifestAttributeWithMetadata<InstrumentationAttribute>> ();

		foreach (TypeDefinition type in Subclasses) {
			if (type.IsSubclassOf ("Android.App.Instrumentation", Cache)) {
				var ia = InstrumentationAttribute.FromCustomAttributeProvider (type, Cache).FirstOrDefault ();

				if (ia is not null) {
					var attr = new ManifestAttributeWithMetadata<InstrumentationAttribute> (ia, JavaNativeTypeManager.ToJniName (type, Cache).Replace ('/', '.'));
					attr.AddMetadata (type, Cache, addIntentFilters: true);
					results.Add (attr);
				}
			}
		}

		return results;
	}

	public List<ManifestAttributeWithMetadata<ApplicationAttribute>> GetTypeApplicationAttributes ()
	{
		var results = new List<ManifestAttributeWithMetadata<ApplicationAttribute>> ();

		foreach (TypeDefinition type in Subclasses) {
			var aa = ApplicationAttribute.FromCustomAttributeProvider (type, Cache);

			if (aa is null)
				continue;

			if (!type.IsSubclassOf ("Android.App.Application", Cache))
				throw new InvalidOperationException (string.Format ("Found [Application] on type {0}.  [Application] can only be used on subclasses of Application.", type.FullName));

			var attr = new ManifestAttributeWithMetadata<ApplicationAttribute> (aa, JavaNativeTypeManager.ToJniName (type, Cache).Replace ('/', '.'));
			attr.AddMetadata (type, Cache, addUsesLibraries: true);
			results.Add (attr);
		}

		return results;
	}

	public IEnumerable<string> GetSubclassNamespaces ()
	{
		foreach (var type in Subclasses) {
			if (type.IsAbstract)
				yield return type.Namespace;
		}
	}

	public IEnumerable<(string name, string compatName)> GetApplicationSubclasses ()
	{
		foreach (var type in Subclasses) {
			if (type.IsAbstract)
				continue;

			if (type.IsSubclassOf ("Android.App.Application", Cache)) {
				var name = JavaNativeTypeManager.ToJniName (type, Cache).Replace ('/', '.');
				var compatName = JavaNativeTypeManager.ToCompatJniName (type, Cache).Replace ('/', '.');

				yield return (name, compatName);
			}
		}
	}

	public IEnumerable<(TypeDefinition type, Func<TypeDefinition, string, TypeDefinitionCache, int, XElement?> generator)> GetSubclassGenerators (string packageName)
	{
		this.packageName = packageName;

		foreach (var t in Subclasses) {
			if (t.IsAbstract)
				continue;

			var generator = GetGenerator (t, Cache);

			if (generator is null)
				continue;

			yield return (t, generator);
		}
	}

	public IEnumerable<(string name, string compatName, XElement? fromCode)> GetSubclassElements (string packageName, HashSet<string> existingTypes, int targetSdkVersionValue)
	{
		foreach ((var t, var generator) in GetSubclassGenerators (packageName)) {
				var name = JavaNativeTypeManager.ToJniName (t, Cache).Replace ('/', '.');
				var compatName = JavaNativeTypeManager.ToCompatJniName (t, Cache).Replace ('/', '.');

				// There's already a launcher for this type
				if (existingTypes.Contains (name) || existingTypes.Contains (compatName)) {
					yield return (name, compatName, null);
					continue;
				}

				XElement? element;

				try {
					element = generator (t, name, Cache, targetSdkVersionValue);
				} catch (InvalidActivityNameException ex) {
					Log.LogErrorFromException (ex);
					continue;
				}

				if (element is null) {
					yield return (name, compatName, null);
					continue;
				}

				// Must have a public parameterless constructor
				var constructors = t.Methods.Where (m => m.IsConstructor).Cast<MethodDefinition> ();

				if (!constructors.Any (c => !c.HasParameters && c.IsPublic)) {
					SequencePoint? sourceLocation = FindSource (constructors);

					if (sourceLocation != null && sourceLocation.Document?.Url != null) {
						Log.LogError (
							subcategory: String.Empty,
							errorCode: "XA4213",
							helpKeyword: String.Empty,
							file: sourceLocation.Document.Url,
							lineNumber: sourceLocation.StartLine,
							columnNumber: sourceLocation.StartColumn,
							endLineNumber: sourceLocation.EndLine,
							endColumnNumber: sourceLocation.EndColumn,
							message: Properties.Resources.XA4213,
							t.FullName);
					} else
						Log.LogCodedError ("XA4213", Properties.Resources.XA4213, t.FullName);

					yield return (name, compatName, null);
					continue;
				}


			yield return (name, compatName, element);
		}
	}

	Func<TypeDefinition, string, TypeDefinitionCache, int, XElement?>? GetGenerator (TypeDefinition type, TypeDefinitionCache cache)
	{
		if (type.IsSubclassOf ("Android.App.Activity", cache))
			return ActivityFromTypeDefinition;
		if (type.IsSubclassOf ("Android.App.Service", cache))
			return (t, name, c, v) => ToElement (t, name, ServiceAttribute.FromTypeDefinition, x => x.ToElement (packageName, c), c);
		if (type.IsSubclassOf ("Android.Content.BroadcastReceiver", cache))
			return (t, name, c, v) => ToElement (t, name, BroadcastReceiverAttribute.FromTypeDefinition, x => x.ToElement (packageName, c), c);
		if (type.IsSubclassOf ("Android.Content.ContentProvider", cache))
			return (t, name, c, v) => ToProviderElement (t, name, c);
		return null;
	}

	XElement? ActivityFromTypeDefinition (TypeDefinition type, string name, TypeDefinitionCache cache, int targetSdkVersion)
	{
		if (name.StartsWith ("_", StringComparison.Ordinal))
			throw new InvalidActivityNameException (string.Format ("Activity name '{0}' is invalid, because activity namespaces may not begin with an underscore.", type.FullName));

		return ToElement (type, name,
				ActivityAttribute.FromTypeDefinition,
				aa => aa.ToElement (Resolver, packageName, cache, targetSdkVersion),
				(aa, element) => {
					if (aa.MainLauncher)
						AddLauncherIntentElements (element);
					var la = LayoutAttribute.FromTypeDefinition (type, cache);
					if (la != null)
						element.Add (la.ToElement (Resolver, packageName, cache));
				},
				cache);
	}

	void AddLauncherIntentElements (XElement activity)
	{
		if (activity.Elements ("intent-filter").Any (f => IsMainLauncher (f)))
			return;

		var filter = new XElement ("intent-filter");

		// Add android:exported="true" if not already present
		XName exported = androidNs + "exported";
		if (activity.Attribute (exported) == null) {
			activity.Add (new XAttribute (exported, "true"));
		}

		activity.AddFirst (filter);
		foreach (KeyValuePair<string, string> e in LauncherIntentElements) {
			if (!filter.Elements (e.Key).Any (x => ((string) x.Attribute (attName)) == e.Value))
				filter.Add (new XElement (e.Key, new XAttribute (attName, e.Value)));
		}
	}

	bool IsMainLauncher (XElement intentFilter)
	{
		return LauncherIntentElements.All (entry =>
				intentFilter.Elements (entry.Key).Any (e => ((string) e.Attribute (attName) == entry.Value)));
	}

	XElement? ToProviderElement (TypeDefinition type, string name, TypeDefinitionCache cache)
	{
		var attr = ContentProviderAttribute.FromTypeDefinition (type, cache);
		if (attr == null)
			return null;

		XElement element = attr.ToElement (packageName, cache);
		if (element.Attribute (attName) == null)
			element.Add (new XAttribute (attName, name));
		foreach (var m in MetaDataAttribute.FromCustomAttributeProvider (type, cache)) {
			element.Add (m.ToElement (packageName, cache));
		}
		foreach (var g in GrantUriPermissionAttribute.FromTypeDefinition (type, cache)) {
			element.Add (g.ToElement (packageName, cache));
		}
		foreach (var i in IntentFilterAttribute.FromTypeDefinition (type, cache)) {
			element.Add (i.ToElement (packageName));
		}
		foreach (var p in PropertyAttribute.FromCustomAttributeProvider (type, cache)) {
			element.Add (p.ToElement (packageName, cache));
		}

		return element;
	}

	XElement? ToElement<TAttribute> (TypeDefinition type, string name, Func<TypeDefinition, TypeDefinitionCache, TAttribute> parser, Func<TAttribute, XElement> toElement, TypeDefinitionCache cache)
		where TAttribute : class
	{
		return ToElement (type, name, parser, toElement, update: null, cache);
	}

	XElement? ToElement<TAttribute> (TypeDefinition type, string name, Func<TypeDefinition, TypeDefinitionCache, TAttribute> parser, Func<TAttribute, XElement> toElement, Action<TAttribute, XElement>? update, TypeDefinitionCache cache)
		where TAttribute : class
	{
		TAttribute attr = parser (type, cache);
		if (attr == null)
			return null;

		XElement element = toElement (attr);
		if (element.Attribute (attName) == null)
			element.Add (new XAttribute (attName, name));
		foreach (var m in MetaDataAttribute.FromCustomAttributeProvider (type, cache)) {
			element.Add (m.ToElement (packageName, cache));
		}
		foreach (var i in IntentFilterAttribute.FromTypeDefinition (type, cache)) {
			element.Add (i.ToElement (packageName));
		}
		foreach (var p in PropertyAttribute.FromCustomAttributeProvider (type, cache)) {
			element.Add (p.ToElement (packageName, cache));
		}
		if (update != null)
			update (attr, element);
		return element;
	}

	SequencePoint? FindSource (IEnumerable<MethodDefinition> methods)
	{
		if (methods == null)
			return null;

		SequencePoint? ret = null;
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

class ManifestAttributeWithMetadata<T>
{
	public T Attribute { get; }
	public string JniName { get; }
	public List<MetaDataAttribute> Metadata { get; } = [];
	public List<IntentFilterAttribute> IntentFilters { get; } = [];
	public List<PropertyAttribute> Properties { get; } = [];
	public List<UsesLibraryAttribute> UsesLibraries { get; } = [];

	public ManifestAttributeWithMetadata (T attribute, string jniName)
	{
		Attribute = attribute;
		JniName = jniName;
	}

	public void AddMetadata (TypeDefinition type, TypeDefinitionCache cache, bool addIntentFilters = false, bool addUsesLibraries = false)
	{
		foreach (var m in MetaDataAttribute.FromCustomAttributeProvider (type, cache))
			Metadata.Add (m);

		foreach (var p in PropertyAttribute.FromCustomAttributeProvider (type, cache))
			Properties.Add (p);

		if (addIntentFilters) {
			foreach (var i in IntentFilterAttribute.FromTypeDefinition (type, cache))
				IntentFilters.Add (i);
		}

		if (addUsesLibraries) {
			foreach (var u in UsesLibraryAttribute.FromCustomAttributeProvider (type, cache))
				UsesLibraries.Add (u);
		}
	}
}
