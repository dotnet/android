#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Xml;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

using Xamarin.Android.Tasks;
using Xamarin.Android.Tasks.JniRemapping;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks
{
	/// <summary>
	/// Converts an R8 mapping file into runtime JNI remapping XML without modifying managed
	/// assemblies.
	/// </summary>
	public class GenerateR8JniRemapping : AndroidTask
	{
		public override string TaskPrefix => "GR8JR";

		[Required]
		public string MappingFile { get; set; } = "";

		[Required]
		public string OutputFile { get; set; } = "";

		public ITaskItem []? ExistingRemapXmlFiles { get; set; }

		public ITaskItem []? LinkedAssemblies { get; set; }

		public bool NativeAot { get; set; }

		public string? NativeAotObjectFile { get; set; }

		readonly Dictionary<string, string> existingEntries = new Dictionary<string, string> (StringComparer.Ordinal);
		readonly HashSet<string> preexistingEntryKeys = new HashSet<string> (StringComparer.Ordinal);
		readonly HashSet<string> externallyOwnedTypes = new HashSet<string> (StringComparer.Ordinal);

		public override bool RunTask ()
		{
			if (!File.Exists (MappingFile)) {
				LogR8JniRemappingError (string.Format (CultureInfo.InvariantCulture, Properties.Resources.XA4327_MappingNotFound, MappingFile));
				return false;
			}

			R8Mapping mapping;
			try {
				mapping = R8Mapping.Load (MappingFile);
			} catch (Exception ex) when (ex is FormatException || ex is IOException || ex is UnauthorizedAccessException) {
				LogR8JniRemappingError (string.Format (CultureInfo.InvariantCulture, Properties.Resources.XA4327_MappingDataFailure, MappingFile, ex.Message));
				return false;
			}

			ReadExistingEntries ();

			HashSet<string>? requiredEntries;
			if (NativeAot) {
				if (NativeAotObjectFile.IsNullOrEmpty () || !File.Exists (NativeAotObjectFile)) {
					LogR8JniRemappingError (string.Format (CultureInfo.InvariantCulture, Properties.Resources.XA4327_NativeAotObjectRequired, NativeAotObjectFile ?? ""));
					return false;
				}
				try {
					requiredEntries = NativeAotJniRetention.GetRequiredEntries (NativeAotObjectFile, mapping);
				} catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException) {
					LogR8JniRemappingError (string.Format (CultureInfo.InvariantCulture, Properties.Resources.XA4327_NativeAotObjectReadFailure, NativeAotObjectFile, ex.Message));
					return false;
				}
				Log.LogDebugMessage ($"Post-ILC NativeAOT JNI retention selected {requiredEntries.Count} mapping entries.");
			} else {
				if (!NativeAotObjectFile.IsNullOrEmpty ()) {
					LogR8JniRemappingError (Properties.Resources.XA4327_NativeAotModeRequired);
					return false;
				}
				ScanLinkedAssemblies (mapping);
				requiredEntries = LinkedAssemblies?.Length > 0
					? new HashSet<string> (mapping.AccessedEntries, StringComparer.Ordinal)
					: null;
			}
			if (Log.HasLoggedErrors) {
				return false;
			}

			string content = GenerateContent (mapping, requiredEntries);
			if (Log.HasLoggedErrors) {
				return false;
			}
			string? directory = Path.GetDirectoryName (OutputFile);
			if (!directory.IsNullOrEmpty ()) {
				Directory.CreateDirectory (directory);
			}
			File.WriteAllText (OutputFile, content, Files.UTF8withoutBOM);
			return !Log.HasLoggedErrors;
		}

		void ScanLinkedAssemblies (R8Mapping mapping)
		{
			if (LinkedAssemblies == null) {
				return;
			}

			var seen = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (ITaskItem assembly in LinkedAssemblies) {
				string path = assembly.ItemSpec;
				if (!seen.Add (path) || !File.Exists (path)) {
					continue;
				}

				try {
					using var stream = File.OpenRead (path);
					using var peReader = new PEReader (stream);
					if (!peReader.HasMetadata) {
						continue;
					}
					JniRemappingAssemblyScanner.Scan (peReader, peReader.GetMetadataReader (), mapping, Log);
				} catch (BadImageFormatException ex) {
					Log.LogDebugMessage ($"Could not read assembly '{path}': {ex.Message}");
				} catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
					LogR8JniRemappingError (string.Format (CultureInfo.InvariantCulture, Properties.Resources.XA4327_AssemblyReadFailure, path, ex.Message));
				}
			}
		}

		string GenerateContent (R8Mapping mapping, HashSet<string>? requiredEntries)
		{
			var allClassMappings = new List<R8ClassMapping> (mapping.EnumerateClassMappings ());
			var classMappings = new List<R8ClassMapping> ();
			foreach (R8ClassMapping classMapping in allClassMappings) {
				if (requiredEntries == null || requiredEntries.Contains (R8Mapping.BuildClassEntry (classMapping.OriginalJniName))) {
					classMappings.Add (classMapping);
				}
			}
			var classRenames = new Dictionary<string, string> (StringComparer.Ordinal);
			var requiredOriginalClasses = new Dictionary<string, string?> (StringComparer.Ordinal);
			foreach (R8ClassMapping classMapping in allClassMappings) {
				classRenames [classMapping.OriginalJniName] = classMapping.ObfuscatedJniName;
			}
			foreach (R8ClassMapping classMapping in classMappings) {
				if (requiredOriginalClasses.TryGetValue (classMapping.ObfuscatedJniName, out string? existing) &&
						!string.Equals (existing, classMapping.OriginalJniName, StringComparison.Ordinal)) {
					requiredOriginalClasses [classMapping.ObfuscatedJniName] = null;
				} else if (!requiredOriginalClasses.ContainsKey (classMapping.ObfuscatedJniName)) {
					requiredOriginalClasses [classMapping.ObfuscatedJniName] = classMapping.OriginalJniName;
				}
			}
			string? RenameClass (string className)
				=> classRenames.TryGetValue (className, out string? renamed) ? renamed : null;

			var settings = new XmlWriterSettings {
				Encoding = Files.UTF8withoutBOM,
				Indent = true,
				IndentChars = "  ",
				NewLineChars = "\n",
				OmitXmlDeclaration = true,
			};

			var output = new StringBuilder ();
			using (var writer = XmlWriter.Create (output, settings)) {
				writer.WriteStartElement ("replacements");
				var skippedClasses = new HashSet<string> (StringComparer.Ordinal);
				foreach (R8ClassMapping classMapping in classMappings) {
					if (!WriteClass (writer, classMapping, requiredOriginalClasses)) {
						skippedClasses.Add (classMapping.OriginalJniName);
					}
				}
				foreach (R8ClassMapping classMapping in classMappings) {
					if (skippedClasses.Contains (classMapping.OriginalJniName)) {
						continue;
					}
					foreach (R8FieldMapping field in classMapping.Fields) {
						if (requiredEntries != null &&
								!requiredEntries.Contains (R8Mapping.BuildFieldEntry (classMapping.OriginalJniName, field.OriginalName))) {
							continue;
						}
						WriteField (writer, classMapping, field, RenameClass);
					}
					foreach (R8MethodMapping method in classMapping.Methods) {
						string methodKey = R8Mapping.BuildMethodKey (method.OriginalName, method.JavaParameterTypes, method.JavaReturnType);
						if (requiredEntries != null &&
								!requiredEntries.Contains (R8Mapping.BuildMethodEntry (classMapping.OriginalJniName, methodKey))) {
							continue;
						}
						WriteMethod (writer, classMapping, method, RenameClass);
					}
				}
				writer.WriteEndElement ();
			}
			output.Append ('\n');
			return output.ToString ();
		}

		bool WriteClass (XmlWriter writer, R8ClassMapping classMapping, Dictionary<string, string?> requiredOriginalClasses)
		{
			bool ownedExternally = externallyOwnedTypes.Contains (BuildTypeKey (classMapping.OriginalJniName));
			if (classMapping.IsRenamed) {
				if (TryClaimEntry ("replace-type", BuildTypeKey (classMapping.OriginalJniName), classMapping.ObfuscatedJniName)) {
					writer.WriteStartElement ("replace-type");
					writer.WriteAttributeString ("from", classMapping.OriginalJniName);
					writer.WriteAttributeString ("to", classMapping.ObfuscatedJniName);
					writer.WriteEndElement ();
				} else {
					ownedExternally = true;
				}
			}

			if (ownedExternally) {
				return false;
			}
			if (!classMapping.IsRenamed ||
					!requiredOriginalClasses.TryGetValue (classMapping.ObfuscatedJniName, out string? originalJniName) ||
					!string.Equals (originalJniName, classMapping.OriginalJniName, StringComparison.Ordinal)) {
				return true;
			}

			if (TryClaimEntry ("reverse-type", BuildReverseTypeKey (classMapping.ObfuscatedJniName), classMapping.OriginalJniName)) {
				writer.WriteStartElement ("reverse-type");
				writer.WriteAttributeString ("from", classMapping.ObfuscatedJniName);
				writer.WriteAttributeString ("to", classMapping.OriginalJniName);
				writer.WriteEndElement ();
			}
			return true;
		}

		void WriteField (XmlWriter writer, R8ClassMapping classMapping, R8FieldMapping field, Func<string, string?> renameClass)
		{
			if (field.JavaFieldType.Length == 0) {
				return;
			}

			string sourceSignature;
			try {
				sourceSignature = JniDescriptorText.JavaSourceTypeToJniTypeToken (field.JavaFieldType);
			} catch (ArgumentException) {
				LogR8JniRemappingWarning (string.Format (
					CultureInfo.InvariantCulture,
					Properties.Resources.XA4328_UnsupportedSignature,
					$"{classMapping.OriginalJniName}.{field.OriginalName}",
					field.JavaFieldType));
				return;
			}

			JniDescriptorText.TryRewriteDescriptor (sourceSignature, renameClass, out string targetSignature);
			if (!classMapping.IsRenamed && !field.IsRenamed &&
					string.Equals (sourceSignature, targetSignature, StringComparison.Ordinal)) {
				return;
			}

			if (!TryClaimEntry (
					"replace-field",
					BuildFieldKey (classMapping.ObfuscatedJniName, field.OriginalName, sourceSignature),
					$"{classMapping.ObfuscatedJniName}\t{field.ObfuscatedName}\t{targetSignature}")) {
				return;
			}

			writer.WriteStartElement ("replace-field");
			writer.WriteAttributeString ("source-type", classMapping.ObfuscatedJniName);
			writer.WriteAttributeString ("source-field-name", field.OriginalName);
			writer.WriteAttributeString ("source-field-signature", sourceSignature);
			writer.WriteAttributeString ("target-type", classMapping.ObfuscatedJniName);
			writer.WriteAttributeString ("target-field-name", field.ObfuscatedName);
			writer.WriteAttributeString ("target-field-signature", targetSignature);
			writer.WriteEndElement ();
		}

		void WriteMethod (XmlWriter writer, R8ClassMapping classMapping, R8MethodMapping method, Func<string, string?> renameClass)
		{
			string sourceSignature;
			try {
				sourceSignature = JniDescriptorText.JavaSourceTypesToMethodDescriptor (method.JavaParameterTypes, method.JavaReturnType);
			} catch (ArgumentException) {
				LogR8JniRemappingWarning (string.Format (
					CultureInfo.InvariantCulture,
					Properties.Resources.XA4328_UnsupportedSignature,
					$"{classMapping.OriginalJniName}.{method.OriginalName}",
					string.Join (",", method.JavaParameterTypes)));
				return;
			}

			JniDescriptorText.TryRewriteDescriptor (sourceSignature, renameClass, out string targetSignature);
			if (!classMapping.IsRenamed && !method.IsRenamed &&
					string.Equals (sourceSignature, targetSignature, StringComparison.Ordinal)) {
				return;
			}

			if (!TryClaimEntry (
					"replace-method",
					BuildMethodKey (classMapping.ObfuscatedJniName, method.OriginalName, sourceSignature),
					$"{classMapping.ObfuscatedJniName}\t{method.ObfuscatedName}\t{targetSignature}")) {
				return;
			}

			writer.WriteStartElement ("replace-method");
			writer.WriteAttributeString ("source-type", classMapping.ObfuscatedJniName);
			writer.WriteAttributeString ("source-method-name", method.OriginalName);
			writer.WriteAttributeString ("source-method-signature", sourceSignature);
			writer.WriteAttributeString ("target-type", classMapping.ObfuscatedJniName);
			writer.WriteAttributeString ("target-method-name", method.ObfuscatedName);
			writer.WriteAttributeString ("target-method-signature", targetSignature);
			writer.WriteAttributeString ("target-method-instance-to-static", "false");
			writer.WriteEndElement ();
		}

		bool TryClaimEntry (string elementName, string key, string target)
		{
			if (!existingEntries.TryGetValue (key, out string? existingTarget)) {
				existingEntries [key] = target;
				return true;
			}

			if (string.Equals (existingTarget, target, StringComparison.Ordinal)) {
				Log.LogDebugMessage ($"Skipping duplicate `{elementName}` entry for `{key.Replace ('\t', ' ')}`.");
				return false;
			}

			if (!preexistingEntryKeys.Contains (key)) {
				LogR8JniRemappingError (string.Format (
					CultureInfo.InvariantCulture,
					Properties.Resources.XA4327_AmbiguousEntry,
					elementName,
					key.Replace ('\t', ' '),
					existingTarget.Replace ('\t', ' '),
					target.Replace ('\t', ' ')));
				return false;
			}

			LogR8JniRemappingWarning (string.Format (
				CultureInfo.InvariantCulture,
				Properties.Resources.XA4328_ConflictingEntry,
				elementName,
				key.Replace ('\t', ' '),
				existingTarget.Replace ('\t', ' '),
				target.Replace ('\t', ' ')));
			return false;
		}

		void ReadExistingEntries ()
		{
			if (ExistingRemapXmlFiles == null) {
				return;
			}

			var readerSettings = new XmlReaderSettings {
				XmlResolver = null,
			};
			foreach (ITaskItem item in ExistingRemapXmlFiles) {
				string file = item.ItemSpec;
				if (string.Equals (Path.GetFullPath (file), Path.GetFullPath (OutputFile), StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				if (!File.Exists (file)) {
					Log.LogDebugMessage ($"Existing remapping input `{file}` does not exist yet.");
					continue;
				}

				try {
					using var stream = File.OpenRead (file);
					using var reader = XmlReader.Create (stream, readerSettings);
					ReadExistingEntries (reader);
				} catch (Exception ex) when (ex is XmlException || ex is IOException || ex is UnauthorizedAccessException) {
					Log.LogDebugMessage ($"Existing remapping input `{file}` could not be read: {ex.Message}");
				}
			}
		}

		void ReadExistingEntries (XmlReader reader)
		{
			while (reader.Read ()) {
				if (reader.NodeType != XmlNodeType.Element) {
					continue;
				}

				switch (reader.LocalName) {
				case "replace-type":
					AddExistingEntry (BuildTypeKey (reader.GetAttribute ("from")), reader.GetAttribute ("to"), externallyOwnedType: true);
					break;
				case "reverse-type":
					AddExistingEntry (BuildReverseTypeKey (reader.GetAttribute ("from")), reader.GetAttribute ("to"));
					break;
				case "replace-field":
					AddExistingEntry (
						BuildFieldKey (reader.GetAttribute ("source-type"), reader.GetAttribute ("source-field-name"), reader.GetAttribute ("source-field-signature")),
						$"{reader.GetAttribute ("target-type")}\t{reader.GetAttribute ("target-field-name")}\t{reader.GetAttribute ("target-field-signature")}");
					break;
				case "replace-method":
					AddExistingEntry (
						BuildMethodKey (reader.GetAttribute ("source-type"), reader.GetAttribute ("source-method-name"), reader.GetAttribute ("source-method-signature")),
						$"{reader.GetAttribute ("target-type")}\t{reader.GetAttribute ("target-method-name")}\t{reader.GetAttribute ("target-method-signature")}");
					break;
				}
			}
		}

		void AddExistingEntry (string key, string? target, bool externallyOwnedType = false)
		{
			if (key.Length == 0) {
				return;
			}
			existingEntries [key] = target ?? "";
			preexistingEntryKeys.Add (key);
			if (externallyOwnedType) {
				externallyOwnedTypes.Add (key);
			}
		}

		static string BuildTypeKey (string? from) => from.IsNullOrEmpty () ? "" : $"T\t{from}";
		static string BuildReverseTypeKey (string? from) => from.IsNullOrEmpty () ? "" : $"R\t{from}";
		static string BuildFieldKey (string? sourceType, string? fieldName, string? signature)
			=> sourceType.IsNullOrEmpty () || fieldName.IsNullOrEmpty () ? "" : $"F\t{sourceType}\t{fieldName}\t{signature}";
		static string BuildMethodKey (string? sourceType, string? methodName, string? signature)
			=> sourceType.IsNullOrEmpty () || methodName.IsNullOrEmpty () ? "" : $"M\t{sourceType}\t{methodName}\t{signature}";

		void LogR8JniRemappingError (string detail)
			=> Log.LogCodedError ("XA4327", Properties.Resources.XA4327, detail);

		void LogR8JniRemappingWarning (string detail)
			=> Log.LogCodedWarning ("XA4328", Properties.Resources.XA4328, detail);
	}
}
