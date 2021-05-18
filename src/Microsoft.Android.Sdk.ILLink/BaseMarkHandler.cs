using Mono.Linker;
using Mono.Linker.Steps;

namespace Mono.Linker
{
	public class BaseMarkHandler : IMarkHandler
	{
		protected LinkContext Context;
		protected AnnotationStore Annotations => Context?.Annotations;

		public virtual void Initialize (LinkContext context, MarkContext markContext)
		{
			Context = context;
		}
	}
}
