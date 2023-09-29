using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class CompileNativeAssembly : AndroidAsyncTask
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
			return this.WhenAll (GetAssemblerConfigs (), (NativeCompilationHelper.AssemblerConfig config) => NativeCompilationHelper.RunAssembler (config));
		}

		IEnumerable<NativeCompilationHelper.AssemblerConfig> GetAssemblerConfigs ()
		{
			string assemblerPath = NativeCompilationHelper.GetAssemblerPath (AndroidBinUtilsDirectory);
			string workingDirectory = Path.GetFullPath (WorkingDirectory);

			foreach (ITaskItem item in Sources) {
				// We don't need the directory since our WorkingDirectory is where all the sources are
				string sourceFile = Path.GetFileName (item.ItemSpec);

				yield return new NativeCompilationHelper.AssemblerConfig (
					log: Log,
					targetArch: MonoAndroidHelper.AbiToTargetArch (item.GetMetadata ("abi")),
					assemblerPath: assemblerPath,
					inputSource: sourceFile,
					workingDirectory: workingDirectory
				) {
					CancellationToken = CancellationToken,
					Cancel = () => Cancel (),
				};
			}
		}
	}
}
