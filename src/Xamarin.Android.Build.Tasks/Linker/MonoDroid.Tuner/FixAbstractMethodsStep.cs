using Mono.Cecil;
using Mono.Linker.Steps;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner
{
	/// <summary>
	/// Simplified FixAbstractMethodsStep for the no-trim path (LinkAssembliesNoShrink).
	/// Core logic is in <see cref="FixAbstractMethodsHelper"/>.
	/// </summary>
	public class FixAbstractMethodsStep : BaseStep, IAssemblyModifierPipelineStep
	{
		MethodDefinition? abstractMethodErrorCtor;

		public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
		{
			// Only run this step on non-main user Android assemblies
			if (context.IsMainAssembly || !context.IsAndroidUserAssembly)
				return;

			context.IsAssemblyModified |= FixAbstractMethodsHelper.FixAbstractMethods (
				assembly,
				Context,
				ref abstractMethodErrorCtor,
				() => Context.GetAssembly ("Mono.Android"),
				(msg) => LogMessage (msg));
		}
	}
}
