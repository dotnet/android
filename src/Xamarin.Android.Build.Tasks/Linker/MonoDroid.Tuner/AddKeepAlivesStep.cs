using Mono.Cecil;
using Mono.Linker.Steps;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner
{
	public class AddKeepAlivesStep : BaseStep, IAssemblyModifierPipelineStep
	{

		public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
		{
			// Only run this step on user Android assemblies
			if (!context.IsAndroidUserAssembly)
				return;

			context.IsAssemblyModified |= AddKeepAlivesHelper.AddKeepAlives (
				assembly,
				Context,
				() => Context.GetAssembly ("System.Private.CoreLib"),
				(msg) => LogMessage (msg));
		}
	}
}
