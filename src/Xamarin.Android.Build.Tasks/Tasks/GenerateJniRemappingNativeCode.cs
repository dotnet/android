#nullable enable

using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
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

		/// <summary>Table sizes produced by the last run; exposed for tests and for consumers
		/// which cannot reach the registered task object (for example the per-RID NativeAOT
		/// build).</summary>
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
			Generate (new JniRemappingAssemblyGenerator (Log));
		}

		void Generate (string remappingXmlFilePath)
		{
			var typeReplacements = new List<JniRemappingTypeReplacement> ();
			var reverseTypeReplacements = new List<JniRemappingTypeReplacement> ();
			var methodReplacements = new List<JniRemappingMethodReplacement> ();
			var fieldReplacements = new List<JniRemappingFieldReplacement> ();

			var readerSettings = new XmlReaderSettings {
				XmlResolver = null,
			};

			using (var reader = XmlReader.Create (File.OpenRead (remappingXmlFilePath), readerSettings)) {
				if (reader.MoveToContent () != XmlNodeType.Element || reader.LocalName != "replacements") {
					Log.LogCodedError ("XA1045", Properties.Resources.XA1045, remappingXmlFilePath);
				} else {
					ReadXml (reader, typeReplacements, reverseTypeReplacements, methodReplacements, fieldReplacements, remappingXmlFilePath);
				}
			}

			Generate (new JniRemappingAssemblyGenerator (Log, typeReplacements, reverseTypeReplacements, methodReplacements, fieldReplacements));
		}

		void Generate (JniRemappingAssemblyGenerator jniRemappingComposer)
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

		void ReadXml (XmlReader reader, List<JniRemappingTypeReplacement> typeReplacements,
		              List<JniRemappingTypeReplacement> reverseTypeReplacements,
		              List<JniRemappingMethodReplacement> methodReplacements,
		              List<JniRemappingFieldReplacement> fieldReplacements,
		              string remappingXmlFilePath)
		{
			bool haveAllAttributes;

			while (reader.Read ()) {
				if (reader.NodeType != XmlNodeType.Element) {
					continue;
				}

				haveAllAttributes = true;
				if (MonoAndroidHelper.StringEquals ("replace-type", reader.LocalName)) {
					haveAllAttributes &= GetRequiredAttribute ("from", out string from);
					haveAllAttributes &= GetRequiredAttribute ("to", out string to);
					if (!haveAllAttributes) {
						continue;
					}

					typeReplacements.Add (new JniRemappingTypeReplacement (from, to));
				} else if (MonoAndroidHelper.StringEquals ("reverse-type", reader.LocalName)) {
					haveAllAttributes &= GetRequiredAttribute ("from", out string from);
					haveAllAttributes &= GetRequiredAttribute ("to", out string to);
					if (!haveAllAttributes) {
						continue;
					}

					reverseTypeReplacements.Add (new JniRemappingTypeReplacement (from, to));
				} else if (MonoAndroidHelper.StringEquals ("replace-method", reader.LocalName)) {
					haveAllAttributes &= GetRequiredAttribute ("source-type", out string sourceType);
					haveAllAttributes &= GetRequiredAttribute ("source-method-name", out string sourceMethodName);
					haveAllAttributes &= GetRequiredAttribute ("target-type", out string targetType);
					haveAllAttributes &= GetRequiredAttribute ("target-method-name", out string targetMethodName);
					haveAllAttributes &= GetRequiredAttribute ("target-method-instance-to-static", out string targetIsStatic);

					if (!haveAllAttributes) {
						continue;
					}

					if (!Boolean.TryParse (targetIsStatic, out bool isStatic)) {
						Log.LogCodedError ("XA1046", Properties.Resources.XA1046, "target-method-instance-to-static", reader.LocalName, targetIsStatic, remappingXmlFilePath, GetCurrentLineNumber ());
						continue;
					}

					string sourceMethodSignature = reader.GetAttribute ("source-method-signature");
					// Optional: inputs which predate it (for example the Intune/MAM mapping) keep
					// the source signature on the target method.
					string targetMethodSignature = reader.GetAttribute ("target-method-signature");
					methodReplacements.Add (
						new JniRemappingMethodReplacement (
							sourceType, sourceMethodName, sourceMethodSignature,
							targetType, targetMethodName, targetMethodSignature, isStatic
						)
					);
				} else if (MonoAndroidHelper.StringEquals ("replace-field", reader.LocalName)) {
					haveAllAttributes &= GetRequiredAttribute ("source-type", out string sourceType);
					haveAllAttributes &= GetRequiredAttribute ("source-field-name", out string sourceFieldName);
					haveAllAttributes &= GetRequiredAttribute ("target-type", out string targetType);
					haveAllAttributes &= GetRequiredAttribute ("target-field-name", out string targetFieldName);

					if (!haveAllAttributes) {
						continue;
					}

					string sourceFieldSignature = reader.GetAttribute ("source-field-signature");
					string targetFieldSignature = reader.GetAttribute ("target-field-signature");
					fieldReplacements.Add (
						new JniRemappingFieldReplacement (
							sourceType, sourceFieldName, sourceFieldSignature,
							targetType, targetFieldName, targetFieldSignature
						)
					);
				}
			}

			bool GetRequiredAttribute (string attributeName, out string attributeValue)
			{
				attributeValue = reader.GetAttribute (attributeName);
				if (!String.IsNullOrEmpty (attributeValue)) {
					return true;
				}

				Log.LogCodedError ("XA1047", Properties.Resources.XA1047, attributeName, reader.LocalName, remappingXmlFilePath, GetCurrentLineNumber ());
				return false;
			}

			int GetCurrentLineNumber () => ((IXmlLineInfo)reader).LineNumber;
		}
	}
}
