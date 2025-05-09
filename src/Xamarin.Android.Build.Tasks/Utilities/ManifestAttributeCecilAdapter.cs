using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;

namespace Xamarin.Android.Tasks;

class ManifestAttributeCecilAdapter
{
	public List<string> Assemblies { get; }
	public TypeDefinitionCache Cache { get; }
	public IAssemblyResolver Resolver { get; }
	public IList<TypeDefinition> Subclasses { get; }

	public ManifestAttributeCecilAdapter (List<string> assemblies, TypeDefinitionCache cache, IAssemblyResolver resolver, IList<TypeDefinition> subclasses)
	{
		Assemblies = assemblies;
		Cache = cache;
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
				var compatName = JavaNativeTypeManager.ToJniName (type, Cache).Replace ('/', '.');

				yield return (name, compatName);
			}
		}
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
