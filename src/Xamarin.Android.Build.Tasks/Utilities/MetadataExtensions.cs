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
	}
}
