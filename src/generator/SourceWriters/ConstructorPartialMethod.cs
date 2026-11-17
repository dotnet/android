using Xamarin.SourceWriter;

namespace generator.SourceWriters;

public class ConstructorPartialMethod : MethodWriter
{
	public ConstructorPartialMethod (string partialMethodName)
	{
		Name = partialMethodName;
		IsPartial = true;
		IsDeclaration = true;
		ReturnType = new TypeReferenceWriter ("void");
	}
}
