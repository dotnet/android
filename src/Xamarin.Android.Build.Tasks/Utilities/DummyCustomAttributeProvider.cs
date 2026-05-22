#nullable enable

using System.Reflection.Metadata;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// A helper type for System.Reflection.Metadata. Getting the value of custom attribute arguments is a bit convoluted, if you merely want the values.
	///
	/// This interface allows usage such as:
	///		CustomAttribute attribute = reader.GetCustomAttribute (handle);
	///		CustomAttributeValue<object> decoded = attribute.DecodeValue (DummyCustomAttributeProvider.Instance);
	/// Or better yet, used via the extension method:
	///		CustomAttributeValue<object> decoded = attribute.GetCustomAttributeArguments ();
	/// </summary>
	public class DummyCustomAttributeProvider : ICustomAttributeTypeProvider<object?>
	{
		public static readonly DummyCustomAttributeProvider Instance = new DummyCustomAttributeProvider ();
		static readonly object systemTypeSentinel = new object ();

		public object? GetPrimitiveType (PrimitiveTypeCode typeCode) => null;

		public object? GetSystemType () => systemTypeSentinel;

		public object? GetSZArrayType (object? elementType) => null;

		public object? GetTypeFromDefinition (MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
		{
			var type = reader.GetTypeDefinition (handle);
			return IsSystemType (reader.GetString (type.Namespace), reader.GetString (type.Name)) ? systemTypeSentinel : null;
		}

		public object? GetTypeFromReference (MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
		{
			var type = reader.GetTypeReference (handle);
			return IsSystemType (reader.GetString (type.Namespace), reader.GetString (type.Name)) ? systemTypeSentinel : null;
		}

		public object? GetTypeFromSerializedName (string name) => name;

		public PrimitiveTypeCode GetUnderlyingEnumType (object? type) => default (PrimitiveTypeCode);

		public bool IsSystemType (object? type) => type == systemTypeSentinel;

		static bool IsSystemType (string ns, string name) => ns == "System" && name == "Type";
	}
}
