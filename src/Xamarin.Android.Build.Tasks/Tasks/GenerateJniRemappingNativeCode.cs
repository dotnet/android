#nullable enable

using System;
using System.Globalization;
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

		public ITaskItem? RemappingXmlFilePath { get; set; }

		[Required]
		public string OutputDirectory { get; set; } = "";

		[Required]
		public string [] SupportedAbis { get; set; } = [];

		[Required]
		public string JniRemappingInfoFilePath { get; set; } = "";

		public bool GenerateEmptyCode { get; set; }

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
			Generate (new JniRemappingAssemblyGenerator (Log), typeReplacementsCount: 0);
		}

		void Generate (string remappingXmlFilePath)
		{
			var typeReplacements = new List<JniRemappingTypeReplacement> ();
			var methodReplacements = new List<JniRemappingMethodReplacement> ();

			var readerSettings = new XmlReaderSettings {
				XmlResolver = null,
			};

			using (var reader = XmlReader.Create (File.OpenRead (remappingXmlFilePath), readerSettings)) {
				if (reader.MoveToContent () != XmlNodeType.Element || reader.LocalName != "replacements") {
					Log.LogError ($"Input file `{remappingXmlFilePath}` does not start with `<replacements/>`");
				} else {
					ReadXml (reader, typeReplacements, methodReplacements, remappingXmlFilePath);
				}
			}

			Generate (new JniRemappingAssemblyGenerator (Log, typeReplacements, methodReplacements), typeReplacements.Count);
		}

		void Generate (JniRemappingAssemblyGenerator jniRemappingComposer, int typeReplacementsCount)
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

			int methodIndexEntryCount = jniRemappingComposer.ReplacementMethodIndexEntryCount;

			BuildEngine4.RegisterTaskObjectAssemblyLocal (
				ProjectSpecificTaskObjectKey (JniRemappingNativeCodeInfoKey),
				new JniRemappingNativeCodeInfo (typeReplacementsCount, methodIndexEntryCount),
				RegisteredTaskObjectLifetime.Build
			);

			WriteInfoFile (typeReplacementsCount, methodIndexEntryCount);
		}

		void WriteInfoFile (int typeReplacementsCount, int methodIndexEntryCount)
		{
			string contents = string.Format (
				CultureInfo.InvariantCulture,
				"version=1\nreplacement_type_count={0}\nreplacement_method_index_entry_count={1}\n",
				typeReplacementsCount,
				methodIndexEntryCount);
			Files.CopyIfStringChanged (contents, JniRemappingInfoFilePath);
		}

		internal static JniRemappingNativeCodeInfo? ReadInfoFile (string path, TaskLoggingHelper log)
		{
			if (!File.Exists (path)) {
				log.LogError ($"JNI remapping info file '{path}' not found. A clean rebuild may be required.");
				return null;
			}

			int typeCount = -1;
			int methodCount = -1;

			foreach (string line in File.ReadLines (path)) {
				if (line.StartsWith ("replacement_type_count=", StringComparison.Ordinal)) {
					typeCount = int.Parse (line.Substring ("replacement_type_count=".Length), NumberStyles.None, CultureInfo.InvariantCulture);
				} else if (line.StartsWith ("replacement_method_index_entry_count=", StringComparison.Ordinal)) {
					methodCount = int.Parse (line.Substring ("replacement_method_index_entry_count=".Length), NumberStyles.None, CultureInfo.InvariantCulture);
				}
			}

			if (typeCount < 0 || methodCount < 0) {
				log.LogError ($"JNI remapping info file '{path}' is malformed.");
				return null;
			}

			return new JniRemappingNativeCodeInfo (typeCount, methodCount);
		}

		void ReadXml (XmlReader reader, List<JniRemappingTypeReplacement> typeReplacements, List<JniRemappingMethodReplacement> methodReplacements, string remappingXmlFilePath)
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
						Log.LogError ($"Attribute 'target-method-instance-to-static' in element '{reader.LocalName}' value '{targetIsStatic}' cannot be parsed as boolean; {remappingXmlFilePath} line {GetCurrentLineNumber ()}");
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

				Log.LogError ($"Attribute '{attributeName}' missing from element '{reader.LocalName}'; {remappingXmlFilePath} line {GetCurrentLineNumber ()}");
				return false;
			}

			int GetCurrentLineNumber () => ((IXmlLineInfo)reader).LineNumber;
		}
	}
}
