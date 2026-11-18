using System.Globalization;
using Mono.Cecil;
using Mono.Linker.Steps;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner
{
	public class AddKeepAlivesStep : BaseStep, IAssemblyModifierPipelineStep
	{
		KeepAliveFixupReporter? reporter;

		public AddKeepAlivesStep ()
		{
		}

		internal AddKeepAlivesStep (KeepAliveFixupReporter reporter)
		{
			this.reporter = reporter;
		}

		public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
		{
			// Only run this step on user Android assemblies
			if (!context.IsAndroidUserAssembly)
				return;

			bool changed = AddKeepAlivesHelper.AddKeepAlives (
				assembly,
				Context,
				() => Context.GetAssembly ("System.Private.CoreLib"),
				(msg) => LogMessage (msg));
			context.IsAssemblyModified |= changed;

			if (changed) {
				reporter ??= new KeepAliveFixupReporter (
					(message, args) => Context.LogWarning (
						"XA0150",
						string.Format (CultureInfo.CurrentCulture, message, args)));
				reporter.Report (assembly, context.Source.ItemSpec);
			}
		}
	}
}
