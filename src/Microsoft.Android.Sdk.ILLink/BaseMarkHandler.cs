using Mono.Linker.Steps;
using Microsoft.Android.Sdk.ILLink;

namespace Mono.Linker
{
	public class BaseMarkHandler : IMarkHandler
	{
		protected LinkContext Context;
		protected AnnotationStore Annotations => Context?.Annotations;
		protected LinkContextMetadataResolver resolver;

		public virtual void Initialize (LinkContext context, MarkContext markContext)
		{
			Context = context;
			resolver = new LinkContextMetadataResolver (context);
		}
	}
}
