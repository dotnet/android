using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Mono.Linker;

namespace Microsoft.Android.Sdk.ILLink
{
	public class LinkContextMetadataResolver : TypeDefinitionCache {
		LinkContext context;

		public LinkContextMetadataResolver (LinkContext context)
		{
			this.context = context;
		}

		public override TypeDefinition Resolve (TypeReference typeReference)
		{
			return context.ResolveTypeDefinition (typeReference);
		}
	}
}