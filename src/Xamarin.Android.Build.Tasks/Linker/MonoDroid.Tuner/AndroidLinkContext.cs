using Mono.Linker;
using Mono.Cecil;

namespace MonoDroid.Tuner
{
	public class AndroidLinkContext : LinkContext
	{
		public AndroidLinkContext (Pipeline pipeline, AssemblyResolver resolver)
			: base (pipeline, resolver) {}

		public AndroidLinkContext (Pipeline pipeline, AssemblyResolver resolver, ReaderParameters readerParameters, UnintializedContextFactory factory)
			: base (pipeline, resolver, readerParameters, factory) {}

		public bool PreserveJniMarshalMethods { get; set; }
	}
}
