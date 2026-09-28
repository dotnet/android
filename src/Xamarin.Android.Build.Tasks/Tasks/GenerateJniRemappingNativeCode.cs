#nullable enable

using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GenerateJniRemappingNativeCode : AndroidTask
	{
		internal const string JniRemappingNativeCodeInfoKey = ".:!JniRemappingNativeCodeInfo!:.";

		internal sealed class JniRemappingNativeCodeInfo
		{
			public int ReplacementTypeCount             { get; }
			public int ReplacementMethodIndexEntryCount { get; }
			public int ReverseTypeCount                 { get; }
			public int ReplacementFieldIndexEntryCount  { get; }

			public JniRemappingNativeCodeInfo (int replacementTypeCount, int replacementMethodIndexEntryCount,
			                                   int reverseTypeCount = 0, int replacementFieldIndexEntryCount = 0)
			{
				ReplacementTypeCount = replacementTypeCount;
				ReplacementMethodIndexEntryCount = replacementMethodIndexEntryCount;
				ReverseTypeCount = reverseTypeCount;
				ReplacementFieldIndexEntryCount = replacementFieldIndexEntryCount;
			}
		}

		public override string TaskPrefix => "GJRNC";

		public ITaskItem? RemappingXmlFilePath { get; set; }

		[Required]
		public string OutputDirectory { get; set; } = "";

		[Required]
		public string [] SupportedAbis { get; set; } = [];

		public bool GenerateEmptyCode { get; set; }

		/// <summary>Table sizes produced by the last run, exposed for focused validation.</summary>
		internal JniRemappingNativeCodeInfo? NativeCodeInfo { get; private set; }

		public override bool RunTask ()
		{
			if (!GenerateEmptyCode) {
				if (RemappingXmlFilePath == null) {
					throw new InvalidOperationException ("RemappingXmlFilePath parameter is required");
				}

				Generate (RemappingXmlFilePath.ItemSpec);
			} else {
				GenerateEmpty ();
			}

			return !Log.HasLoggedErrors;
		}

		void GenerateEmpty ()
		{
			Generate (new JniRemappingNativeCodeGenerator (Log));
		}

		void Generate (string remappingXmlFilePath)
		{
			var entries = JniRemappingXmlReader.Read (remappingXmlFilePath, Log);
			if (Log.HasLoggedErrors) {
				return;
			}

			Generate (new JniRemappingNativeCodeGenerator (
				Log, entries.TypeReplacements, entries.ReverseTypeReplacements, entries.MethodReplacements, entries.FieldReplacements));
		}

		void Generate (JniRemappingNativeCodeGenerator jniRemappingComposer)
		{
			LLVMIR.LlvmIrModule module =  jniRemappingComposer.Construct ();

			foreach (string abi in SupportedAbis) {
				string baseAsmFilePath = Path.Combine (OutputDirectory, $"jni_remap.{abi.ToLowerInvariant ()}");
				string llFilePath  = $"{baseAsmFilePath}.ll";

				using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
					jniRemappingComposer.Generate (module, GenerateNativeApplicationConfigSources.GetAndroidTargetArchForAbi (abi), sw, llFilePath);
					sw.Flush ();
					Files.CopyIfStreamChanged (sw.BaseStream, llFilePath);
				}
			}

			NativeCodeInfo = new JniRemappingNativeCodeInfo (
				jniRemappingComposer.ReplacementTypeCount,
				jniRemappingComposer.ReplacementMethodIndexEntryCount,
				jniRemappingComposer.ReverseTypeCount,
				jniRemappingComposer.ReplacementFieldIndexEntryCount
			);

			BuildEngine4.RegisterTaskObjectAssemblyLocal (
				ProjectSpecificTaskObjectKey (JniRemappingNativeCodeInfoKey),
				NativeCodeInfo,
				RegisteredTaskObjectLifetime.Build
			);
		}
	}
}
