using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_PrepareProps : Step
	{
		const ConsoleColor StepColor = ConsoleColor.White;

		public Step_PrepareProps ()
			: base ("Preparing property files")
		{}

#pragma warning disable CS1998
		protected override async Task<bool> Execute (Context context)
		{
			string javaInteropDir = Configurables.Paths.ExternalJavaInteropDir;

			LogStep (context, "Configuring Java.Interop property overrides");
			var jiOverrideProps = new GeneratedPlaceholdersFile (
				new Dictionary<string, string> (StringComparer.Ordinal) {
					{ "@MonoCecilVersion@",    Context.Instance.Properties.GetRequiredValue (KnownProperties.MonoCecilVersion) },
					{ "@MicrosoftAndroidSdkOutDir@", Configurables.Paths.InstallMSBuildDir }
				},
				Path.Combine (Configurables.Paths.BuildToolsScriptsDir, "Configuration.Java.Interop.Override.in.props"),
				Path.Combine (javaInteropDir, "Configuration.Override.props")
			);
			jiOverrideProps.Generate (context);

			return true;
		}

		void LogStep (Context context, string step)
		{
			Log.StatusLine ($"  {context.Characters.Bullet} {step}", StepColor);
		}

#pragma warning restore CS1998
	}
}
