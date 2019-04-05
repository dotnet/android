using System;
using System.Reflection.Metadata;

namespace Xamarin.Android.Tasks
{
	public static class MetadataExtensions
	{
		public static string GetCustomAttributeFullName (this MetadataReader reader, CustomAttribute attribute)
		{
			if (attribute.Constructor.Kind == HandleKind.MemberReference) {
				var ctor = reader.GetMemberReference ((MemberReferenceHandle)attribute.Constructor);
				var type = reader.GetTypeReference ((TypeReferenceHandle)ctor.Parent);
				return reader.GetString (type.Namespace) + "." + reader.GetString (type.Name);
			} else if (attribute.Constructor.Kind == HandleKind.MethodDefinition) {
				var ctor = reader.GetMethodDefinition ((MethodDefinitionHandle)attribute.Constructor);
				var type = reader.GetTypeDefinition (ctor.GetDeclaringType ());
				return reader.GetString (type.Namespace) + "." + reader.GetString (type.Name);
			}
			return null;
		}

		public static CustomAttributeValue<object> GetCustomAttributeArguments (this CustomAttribute attribute)
		{
			return attribute.DecodeValue (DummyCustomAttributeProvider.Instance);
		}

		/// <summary>
		/// Returns the TargetFrameworkIdentifier of an assembly, or null if not found
		/// </summary>
		public static string GetTargetFrameworkIdentifier (this AssemblyDefinition assembly, MetadataReader reader)
		{
			foreach (var handle in assembly.GetCustomAttributes ()) {
				var attribute = reader.GetCustomAttribute (handle);
				var name = reader.GetCustomAttributeFullName (attribute);
				if (name == "System.Runtime.Versioning.TargetFrameworkAttribute") {
					var arguments = attribute.GetCustomAttributeArguments ();
					foreach (var p in arguments.FixedArguments) {
						// Of the form "MonoAndroid,Version=v8.1"
						var value = p.Value?.ToString ();
						if (!string.IsNullOrEmpty (value)) {
							int commaIndex = value.IndexOf (",", StringComparison.Ordinal);
							if (commaIndex != -1) {
								return value.Substring (0, commaIndex);
							}
						}
					}
					return null;
				}
			}
			return null;
		}
	}
}
