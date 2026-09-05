#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Xml;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Converts the naming-only R8 seed <c>mapping.txt</c> into a JNI remapping XML document that
	/// the existing <c>@(_AndroidRemapMembers)</c> -&gt; <c>MergeRemapXml</c> -&gt;
	/// <c>GenerateJniRemappingNativeCode</c> pipeline consumes.
	///
	/// Managed assemblies are *not* rewritten on this path, so they keep the original JNI names.
	/// The generated document is what teaches the runtime how those original names map onto the
	/// obfuscated names R8 produced, and how the obfuscated names map back for Java-to-managed
	/// lookups.
	///
	/// The document extends the existing schema in a backward-compatible way:
	///
	/// <list type="bullet">
	///   <item><c>&lt;replace-type from to /&gt;</c> - unchanged, one per renamed class.</item>
	///   <item><c>&lt;replace-method ... /&gt;</c> - unchanged attributes, plus the new optional
	///     <c>target-method-signature</c> carrying the JNI descriptor after its parameter and
	///     return types were themselves renamed.</item>
	///   <item><c>&lt;reverse-type from to /&gt;</c> - new; obfuscated-to-original class name, for
	///     Java-to-managed lookup. Only emitted when the reverse direction is unambiguous.</item>
	///   <item><c>&lt;replace-field ... /&gt;</c> - new; field renames and rewritten field
	///     descriptors.</item>
	/// </list>
	///
	/// Existing consumers ignore the new elements and attributes, and existing remapping inputs
	/// (for example the Intune/MAM mapping) are composed with rather than overridden: an entry that
	/// collides with one already contributed by another input is dropped, with a warning.
	/// </summary>
	public class GenerateR8JniRemapping : AndroidTask
	{
		public override string TaskPrefix => "GR8JR";

		/// <summary>The naming-only R8 seed mapping file.</summary>
		[Required]
		public string MappingFile { get; set; } = "";

		[Required]
		public string OutputFile { get; set; } = "";

		/// <summary>
		/// Remapping XML documents already contributed by other features. Entries colliding with
		/// these are not emitted, so the pre-existing inputs keep winning.
		/// </summary>
		public ITaskItem []? ExistingRemapXmlFiles { get; set; }

		public ITaskItem []? LinkedAssemblies { get; set; }

		/// <summary>Use post-ILC retention instead of treating pre-ILC assemblies as linked output.</summary>
		public bool NativeAot { get; set; }

		/// <summary>
		/// ILC's NativeObject, before native linking. Generated JNI identifiers must remain literal
		/// strings; runtime-constructed names require explicit remapping in ExistingRemapXmlFiles.
		/// </summary>
		public string? NativeAotObjectFile { get; set; }

		readonly Dictionary<string, string> existingEntries = new Dictionary<string, string> (StringComparer.Ordinal);

		// Types another remapping input already describes. Everything about such a type - its
		// reverse mapping and its members - is left to that input.
		readonly HashSet<string> externallyOwnedTypes = new HashSet<string> (StringComparer.Ordinal);

		public override bool RunTask ()
		{
			if (!File.Exists (MappingFile)) {
				LogR8JniRemappingError (string.Format (Properties.Resources.XA4327_SeedMappingNotFound, MappingFile));
				return false;
			}

			R8Mapping mapping;
			try {
				mapping = R8Mapping.Load (MappingFile);
			} catch (FormatException ex) {
				LogR8JniRemappingError (string.Format (Properties.Resources.XA4327_MappingDataFailure, MappingFile, ex.Message));
				return false;
			} catch (IOException ex) {
				LogR8JniRemappingError (string.Format (Properties.Resources.XA4327_MappingDataFailure, MappingFile, ex.Message));
				return false;
			} catch (UnauthorizedAccessException ex) {
				LogR8JniRemappingError (string.Format (Properties.Resources.XA4327_MappingDataFailure, MappingFile, ex.Message));
				return false;
			}

			ReadExistingEntries ();

			HashSet<string>? requiredEntries;
			if (NativeAot) {
				if (NativeAotObjectFile.IsNullOrEmpty () || !File.Exists (NativeAotObjectFile)) {
					LogR8JniRemappingError (string.Format (Properties.Resources.XA4327_NativeAotObjectRequired, NativeAotObjectFile ?? ""));
					return false;
				}
				try {
					requiredEntries = NativeAotJniRetention.GetRequiredEntries (NativeAotObjectFile, mapping);
				} catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException) {
					LogR8JniRemappingError (string.Format (Properties.Resources.XA4327_NativeAotObjectReadFailure, NativeAotObjectFile, ex.Message));
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
					MetadataReader reader = peReader.GetMetadataReader ();

					JniAssemblyRewriter.ScanAssembly (peReader, reader, mapping, Log);
				} catch (BadImageFormatException ex) {
					Log.LogDebugMessage ($"Could not read assembly '{path}': {ex.Message}");
				} catch (JniRewriteException ex) {
					LogR8JniRemappingError ($"The linked assembly '{path}' could not be scanned: {ex.Message}");
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
			foreach (R8ClassMapping classMapping in allClassMappings) {
				classRenames [classMapping.OriginalJniName] = classMapping.ObfuscatedJniName;
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
					if (!WriteClass (writer, mapping, classMapping)) {
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

		/// <summary>
		/// Writes the class-level entries. Returns false when another remapping input owns this
		/// type, in which case its members must be left to that input as well.
		/// </summary>
		bool WriteClass (XmlWriter writer, R8Mapping mapping, R8ClassMapping classMapping)
		{
			bool ownedExternally = externallyOwnedTypes.Contains (BuildTypeKey (classMapping.OriginalJniName));
			if (classMapping.IsRenamed) {
				if (TryClaimEntry (
						"replace-type",
						BuildTypeKey (classMapping.OriginalJniName),
						classMapping.ObfuscatedJniName)) {
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

			// R8 class merging can map several original classes onto one residual class; the
			// reverse direction is then ambiguous and must not be described at all.
			if (!classMapping.IsRenamed ||
					!mapping.TryGetOriginalClass (classMapping.ObfuscatedJniName, out string originalJniName) ||
					!string.Equals (originalJniName, classMapping.OriginalJniName, StringComparison.Ordinal)) {
				return true;
			}

			if (TryClaimEntry (
					"reverse-type",
					BuildReverseTypeKey (classMapping.ObfuscatedJniName),
					classMapping.OriginalJniName)) {
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
					BuildFieldKey (classMapping.OriginalJniName, field.OriginalName),
					$"{classMapping.ObfuscatedJniName}\t{field.ObfuscatedName}\t{targetSignature}")) {
				return;
			}

			writer.WriteStartElement ("replace-field");
			writer.WriteAttributeString ("source-type", classMapping.OriginalJniName);
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

			// The source signature is part of the key, so overloads stay distinct entries.
			if (!TryClaimEntry (
					"replace-method",
					BuildMethodKey (classMapping.OriginalJniName, method.OriginalName, sourceSignature),
					$"{classMapping.ObfuscatedJniName}\t{method.ObfuscatedName}\t{targetSignature}")) {
				return;
			}

			writer.WriteStartElement ("replace-method");
			writer.WriteAttributeString ("source-type", classMapping.OriginalJniName);
			writer.WriteAttributeString ("source-method-name", method.OriginalName);
			writer.WriteAttributeString ("source-method-signature", sourceSignature);
			writer.WriteAttributeString ("target-type", classMapping.ObfuscatedJniName);
			writer.WriteAttributeString ("target-method-name", method.ObfuscatedName);
			writer.WriteAttributeString ("target-method-signature", targetSignature);
			writer.WriteAttributeString ("target-method-instance-to-static", "false");
			writer.WriteEndElement ();
		}

		/// <summary>
		/// Records an entry, reporting a conflict when another remapping input already described
		/// the same source. Returns false when the entry must not be emitted.
		/// </summary>
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

			LogR8JniRemappingWarning (string.Format (
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
					// MergeRemapXml reports missing inputs (XA4316) later in the build.
					Log.LogDebugMessage ($"Existing remapping input `{file}` does not exist yet.");
					continue;
				}

				try {
					using var reader = XmlReader.Create (File.OpenRead (file), readerSettings);
					ReadExistingEntries (reader);
				} catch (Exception ex) when (ex is XmlException || ex is IOException || ex is UnauthorizedAccessException) {
					// MergeRemapXml reports unreadable inputs (XA4318) later in the build.
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
					AddExistingEntry (
						BuildTypeKey (reader.GetAttribute ("from")),
						reader.GetAttribute ("to"),
						externallyOwnedType: true);
					break;
				case "reverse-type":
					AddExistingEntry (
						BuildReverseTypeKey (reader.GetAttribute ("from")),
						reader.GetAttribute ("to"));
					break;
				case "replace-field":
					AddExistingEntry (
						BuildFieldKey (reader.GetAttribute ("source-type"), reader.GetAttribute ("source-field-name")),
						$"{reader.GetAttribute ("target-type")}\t{reader.GetAttribute ("target-field-name")}\t{reader.GetAttribute ("target-field-signature")}");
					break;
				case "replace-method":
					AddExistingEntry (
						BuildMethodKey (
							reader.GetAttribute ("source-type"),
							reader.GetAttribute ("source-method-name"),
							reader.GetAttribute ("source-method-signature")),
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
			if (externallyOwnedType) {
				externallyOwnedTypes.Add (key);
			}
		}

		static string BuildTypeKey (string? from) => from.IsNullOrEmpty () ? "" : $"T\t{from}";

		static string BuildReverseTypeKey (string? from) => from.IsNullOrEmpty () ? "" : $"R\t{from}";

		static string BuildFieldKey (string? sourceType, string? fieldName)
			=> sourceType.IsNullOrEmpty () || fieldName.IsNullOrEmpty () ? "" : $"F\t{sourceType}\t{fieldName}";

		// A method's source signature is part of its identity: overloads must not collapse.
		static string BuildMethodKey (string? sourceType, string? methodName, string? signature)
			=> sourceType.IsNullOrEmpty () || methodName.IsNullOrEmpty () ? "" : $"M\t{sourceType}\t{methodName}\t{signature}";

		void LogR8JniRemappingError (string detail)
			=> Log.LogCodedError ("XA4327", Properties.Resources.XA4327, detail);

		void LogR8JniRemappingWarning (string detail)
			=> Log.LogCodedWarning ("XA4328", Properties.Resources.XA4328, detail);
	}
}
