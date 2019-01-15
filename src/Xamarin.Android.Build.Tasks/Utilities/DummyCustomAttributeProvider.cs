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
	public class DummyCustomAttributeProvider : ICustomAttributeTypeProvider<object>
	{
		public static readonly DummyCustomAttributeProvider Instance = new DummyCustomAttributeProvider ();

		public object GetPrimitiveType (PrimitiveTypeCode typeCode) => null;

		public object GetSystemType () => null;

		public object GetSZArrayType (object elementType) => null;

		public object GetTypeFromDefinition (MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => null;

		public object GetTypeFromReference (MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => null;

		public object GetTypeFromSerializedName (string name) => null;

		public PrimitiveTypeCode GetUnderlyingEnumType (object type) => default (PrimitiveTypeCode);

		public bool IsSystemType (object type) => false;
	}
}
