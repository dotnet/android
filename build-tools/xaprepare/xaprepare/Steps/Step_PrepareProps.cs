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
			string monoSourceDir = Configurables.Paths.MonoSourceFullPath;
			string javaInteropDir = Configurables.Paths.ExternalJavaInteropDir;

			LogStep (context, "Copying Mono.Cecil files");
			Utilities.CopyFilesSimple (
				Directory.EnumerateFiles (Path.Combine (javaInteropDir, "external"), "Mono.Cecil*"),
				Path.Combine (monoSourceDir, "external")
			);

			LogStep (context, "Copying code signing keys");
			Utilities.CopyFileToDir (Path.Combine (javaInteropDir, "product.snk"), monoSourceDir);

			LogStep (context, "Configuring Java.Interop property overrides");
			var jiOverrideProps = new GeneratedPlaceholdersFile (
				new Dictionary<string, string> (StringComparer.Ordinal) {
					{ "@MonoCecilVersion@",    Context.Instance.Properties.GetRequiredValue (KnownProperties.MonoCecilVersion) },
					{ "@NetCoreBinDirectory@", Configurables.Paths.NetCoreBinDir }
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
