using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class CompileNativeAssembly : AsyncTask
	{
		public override string TaskPrefix => "CNA";

		[Required]
		public ITaskItem[] Sources { get; set; }

		[Required]
		public bool DebugBuild { get; set; }

		[Required]
		public new string WorkingDirectory { get; set; }

		[Required]
		public string AndroidBinUtilsDirectory { get; set; }

		public override System.Threading.Tasks.Task RunTaskAsync ()
		{
			var context = new NativeAssemblerCompilation.AssemblerRunContext (
				Log,
				Path.GetFullPath (WorkingDirectory),
				registerForCancellation: RegisterForCancellation,
				cancel: Cancel
			);

			return this.WhenAll (
				GetAssemblerConfigs (),
				(NativeAssemblerCompilation.AssemblerConfig config) => NativeAssemblerCompilation.RunAssembler (context, config)
			);
		}

		void RegisterForCancellation (Process proc)
		{
			CancellationToken.Register (() => {
				try {
					proc.Kill ();
				} catch (Exception) {
				}
			});
		}

		IEnumerable<NativeAssemblerCompilation.AssemblerConfig> GetAssemblerConfigs ()
		{
			foreach (ITaskItem item in Sources) {
				yield return NativeAssemblerCompilation.GetAssemblerConfig (AndroidBinUtilsDirectory, item, stripFilePaths: true);
			}
		}
	}
}
