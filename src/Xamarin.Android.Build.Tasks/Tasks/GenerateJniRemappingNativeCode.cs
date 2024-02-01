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

			public JniRemappingNativeCodeInfo (int replacementTypeCount, int replacementMethodIndexEntryCount)
			{
				ReplacementTypeCount = replacementTypeCount;
				ReplacementMethodIndexEntryCount = replacementMethodIndexEntryCount;
			}
		}

		public override string TaskPrefix => "GJRNC";

		public ITaskItem RemappingXmlFilePath { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public string [] SupportedAbis { get; set; }

		public bool GenerateEmptyCode { get; set; }

		public override bool RunTask ()
		{
			if (!GenerateEmptyCode) {
				if (RemappingXmlFilePath == null) {
					throw new InvalidOperationException ("RemappingXmlFilePath parameter is required");
				}

				Generate ();
			} else {
				GenerateEmpty ();
			}

			return !Log.HasLoggedErrors;
		}

		void GenerateEmpty ()
		{
			Generate (new JniRemappingAssemblyGenerator (s => Log.LogDebugMessage (s)), typeReplacementsCount: 0);
		}

		void Generate ()
		{
			var typeReplacements = new List<JniRemappingTypeReplacement> ();
			var methodReplacements = new List<JniRemappingMethodReplacement> ();

			var readerSettings = new XmlReaderSettings {
				XmlResolver = null,
			};

			using (var reader = XmlReader.Create (File.OpenRead (RemappingXmlFilePath.ItemSpec), readerSettings)) {
				if (reader.MoveToContent () != XmlNodeType.Element || reader.LocalName != "replacements") {
					Log.LogError ($"Input file `{RemappingXmlFilePath.ItemSpec}` does not start with `<replacements/>`");
				} else {
					ReadXml (reader, typeReplacements, methodReplacements);
				}
			}

			Generate (new JniRemappingAssemblyGenerator (typeReplacements, methodReplacements, s => Log.LogDebugMessage (s)), typeReplacements.Count);
		}

		void Generate (JniRemappingAssemblyGenerator jniRemappingComposer, int typeReplacementsCount)
		{
			LLVMIR.LlvmIrModule module =  jniRemappingComposer.Construct ();

			foreach (string abi in SupportedAbis) {
				string baseAsmFilePath = Path.Combine (OutputDirectory, $"jni_remap.{abi.ToLowerInvariant ()}");
				string llFilePath  = $"{baseAsmFilePath}.ll";

				using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
					jniRemappingComposer.Generate (module, GeneratePackageManagerJava.GetAndroidTargetArchForAbi (abi), sw, llFilePath);
					sw.Flush ();
					Files.CopyIfStreamChanged (sw.BaseStream, llFilePath);
				}
			}

			BuildEngine4.RegisterTaskObjectAssemblyLocal (
				ProjectSpecificTaskObjectKey (JniRemappingNativeCodeInfoKey),
				new JniRemappingNativeCodeInfo (typeReplacementsCount, jniRemappingComposer.ReplacementMethodIndexEntryCount),
				RegisteredTaskObjectLifetime.Build
			);
		}

		void ReadXml (XmlReader reader, List<JniRemappingTypeReplacement> typeReplacements, List<JniRemappingMethodReplacement> methodReplacements)
		{
			bool haveAllAttributes;

			while (reader.Read ()) {
				if (reader.NodeType != XmlNodeType.Element) {
					continue;
				}

				haveAllAttributes = true;
				if (String.Compare ("replace-type", reader.LocalName, StringComparison.Ordinal) == 0) {
					haveAllAttributes &= GetRequiredAttribute ("from", out string from);
					haveAllAttributes &= GetRequiredAttribute ("to", out string to);
					if (!haveAllAttributes) {
						continue;
					}

					typeReplacements.Add (new JniRemappingTypeReplacement (from, to));
				} else if (String.Compare ("replace-method", reader.LocalName, StringComparison.Ordinal) == 0) {
					haveAllAttributes &= GetRequiredAttribute ("source-type", out string sourceType);
					haveAllAttributes &= GetRequiredAttribute ("source-method-name", out string sourceMethodName);
					haveAllAttributes &= GetRequiredAttribute ("target-type", out string targetType);
					haveAllAttributes &= GetRequiredAttribute ("target-method-name", out string targetMethodName);
					haveAllAttributes &= GetRequiredAttribute ("target-method-instance-to-static", out string targetIsStatic);

					if (!haveAllAttributes) {
						continue;
					}

					if (!Boolean.TryParse (targetIsStatic, out bool isStatic)) {
						Log.LogError ($"Attribute 'target-method-instance-to-static' in element '{reader.LocalName}' value '{targetIsStatic}' cannot be parsed as boolean; {RemappingXmlFilePath.ItemSpec} line {GetCurrentLineNumber ()}");
						continue;
					}

					string sourceMethodSignature = reader.GetAttribute ("source-method-signature");
					methodReplacements.Add (
						new JniRemappingMethodReplacement (
							sourceType, sourceMethodName, sourceMethodSignature,
							targetType, targetMethodName, isStatic
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

				Log.LogError ($"Attribute '{attributeName}' missing from element '{reader.LocalName}'; {RemappingXmlFilePath.ItemSpec} line {GetCurrentLineNumber ()}");
				return false;
			}

			int GetCurrentLineNumber () => ((IXmlLineInfo)reader).LineNumber;
		}
	}
}
