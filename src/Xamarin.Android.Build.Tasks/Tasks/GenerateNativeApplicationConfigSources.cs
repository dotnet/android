// Copyright (C) 2011 Xamarin, Inc. All rights reserved.
#nullable enable
using System.IO;
using Microsoft.Build.Framework;

using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Creates the native assembly containing the remaining application format marker.
	/// </summary>
	public class GenerateNativeApplicationConfigSources : AndroidTask
	{
		public override string TaskPrefix => "GCA";

		[Required]
		public string EnvironmentOutputDirectory { get; set; } = "";

		[Required]
		public string [] SupportedAbis { get; set; } = [];

		/// <summary>
		/// When <c>true</c>, descriptive comments are written into the generated LLVM IR.  They make
		/// the <c>.ll</c> far easier to read, but have no effect on the object code produced from it.
		/// Set from the <c>$(_AndroidEmitLlvmIrComments)</c> MSBuild property.
		/// </summary>
		public bool EmitLlvmIrComments { get; set; }

		static internal AndroidTargetArch GetAndroidTargetArchForAbi (string abi) => MonoAndroidHelper.AbiToTargetArch (abi);

		public override bool RunTask ()
		{
			LLVMIR.LlvmIrComposer appConfigAsmGen = new ApplicationFormatTagNativeAssemblyGenerator (Log);
			LLVMIR.LlvmIrModule appConfigModule = appConfigAsmGen.Construct ();
			appConfigAsmGen.EmitComments = EmitLlvmIrComments;

			foreach (string abi in SupportedAbis) {
				string targetAbi = abi.ToLowerInvariant ();
				string environmentBaseAsmFilePath = Path.Combine (EnvironmentOutputDirectory, $"environment.{targetAbi}");
				string environmentLlFilePath  = $"{environmentBaseAsmFilePath}.ll";
				AndroidTargetArch targetArch = GetAndroidTargetArchForAbi (abi);

				using var appConfigWriter = MemoryStreamPool.Shared.CreateStreamWriter ();
				try {
					appConfigAsmGen.Generate (appConfigModule, targetArch, appConfigWriter, environmentLlFilePath);
				} catch {
					throw;
				} finally {
					appConfigWriter.Flush ();
					Files.CopyIfStreamChanged (appConfigWriter.BaseStream, environmentLlFilePath);
				}
			}

			return !Log.HasLoggedErrors;
		}
	}
}
