using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Mono.Linker;

namespace Microsoft.Android.Sdk.ILLink
{
	public class LinkContextMetadataResolver : IMetadataResolver {
		LinkContext context;

		public LinkContextMetadataResolver (LinkContext context)
		{
			this.context = context;
		}

		public virtual TypeDefinition Resolve (TypeReference type)
		{
			return context.ResolveTypeDefinition (type);
		}

		public virtual FieldDefinition Resolve (FieldReference field)
		{
			return context.ResolveFieldDefinition (field);
		}

		public virtual MethodDefinition Resolve (MethodReference method)
		{
			return context.ResolveMethodDefinition (method);
		}
	}
}