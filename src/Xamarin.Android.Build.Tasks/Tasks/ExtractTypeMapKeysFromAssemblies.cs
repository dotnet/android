#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public class ExtractTypeMapKeysFromAssemblies : AndroidTask
	{
		public override string TaskPrefix => "ETMKA";

		[Required]
		public ITaskItem [] LinkedAssemblies { get; set; } = [];

		[Required]
		public string OutputFile { get; set; } = "";

		public override bool RunTask ()
		{
			if (LinkedAssemblies.Length == 0) {
				Log.LogCodedError ("XA4327", Properties.Resources.XA4327, nameof (LinkedAssemblies), "No linked assemblies were supplied.");
				return false;
			}

			var keys = new SortedSet<string> (StringComparer.Ordinal);
			foreach (var assembly in LinkedAssemblies) {
				try {
					ReadKeys (assembly.ItemSpec, keys);
				} catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is BadImageFormatException || ex is ArgumentException) {
					Log.LogCodedError ("XA4327", Properties.Resources.XA4327, assembly.ItemSpec, ex.Message);
				}
			}

			if (Log.HasLoggedErrors) {
				return false;
			}

			try {
				var directory = Path.GetDirectoryName (OutputFile);
				if (!directory.IsNullOrEmpty ()) {
					Directory.CreateDirectory (directory);
				}
				using var writer = new StreamWriter (OutputFile, append: false, new UTF8Encoding (encoderShouldEmitUTF8Identifier: false));
				writer.NewLine = "\n";
				foreach (string key in keys) {
					writer.WriteLine (key);
				}
			} catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) {
				Log.LogCodedError ("XA4327", Properties.Resources.XA4327, OutputFile, ex.Message);
			}

			return !Log.HasLoggedErrors;
		}

		void ReadKeys (string assemblyPath, SortedSet<string> keys)
		{
			using var stream = File.OpenRead (assemblyPath);
			using var pe = new PEReader (stream);
			if (!pe.HasMetadata) {
				throw new BadImageFormatException ("The input has no managed metadata.");
			}
			var reader = pe.GetMetadataReader ();
			if (!reader.IsAssembly) {
				throw new BadImageFormatException ("The input is not a managed assembly.");
			}

			// ILLink preserves the surviving TypeMap attributes, including their conditional
			// target argument. Alias arrays and Register attributes are not the linked map.
			foreach (var handle in reader.GetAssemblyDefinition ().GetCustomAttributes ()) {
				var attribute = reader.GetCustomAttribute (handle);
				if (reader.GetCustomAttributeFullName (attribute, Log) != "System.Runtime.InteropServices.TypeMapAttribute`1") {
					continue;
				}

				int argumentCount = GetArgumentCount (reader, attribute);
				var blob = reader.GetBlobReader (attribute.Value);
				if (blob.ReadUInt16 () != 1) {
					throw new BadImageFormatException ("Invalid TypeMap attribute prolog.");
				}
				string? key = blob.ReadSerializedString ();
				if (key.IsNullOrEmpty ()) {
					throw new BadImageFormatException ("Invalid TypeMap key.");
				}
				for (int i = 1; i < argumentCount; i++) {
					if (string.IsNullOrEmpty (blob.ReadSerializedString ())) {
						throw new BadImageFormatException ("Invalid TypeMap type argument.");
					}
				}
				if (blob.ReadUInt16 () != 0 || blob.RemainingBytes != 0) {
					throw new BadImageFormatException ("Invalid TypeMap attribute arguments.");
				}

				key = NormalizeKey (key);
				if (!GenerateTypeMapProguardConfiguration.IsClassName (key)) {
					throw new BadImageFormatException ($"Invalid TypeMap class name '{key}'.");
				}
				keys.Add (key);
			}
		}

		static int GetArgumentCount (MetadataReader reader, CustomAttribute attribute)
		{
			BlobHandle signature;
			StringHandle constructorName;
			if (attribute.Constructor.Kind == HandleKind.MemberReference) {
				var constructor = reader.GetMemberReference ((MemberReferenceHandle) attribute.Constructor);
				signature = constructor.Signature;
				constructorName = constructor.Name;
			} else {
				var constructor = reader.GetMethodDefinition ((MethodDefinitionHandle) attribute.Constructor);
				signature = constructor.Signature;
				constructorName = constructor.Name;
			}
			var blob = reader.GetBlobReader (signature);
			var header = blob.ReadSignatureHeader ();
			int count = blob.ReadCompressedInteger ();
			if (reader.GetString (constructorName) != ".ctor" || header.Kind != SignatureKind.Method || !header.IsInstance || header.IsGeneric ||
			    (count != 2 && count != 3) || blob.ReadSignatureTypeCode () != SignatureTypeCode.Void ||
			    blob.ReadSignatureTypeCode () != SignatureTypeCode.String) {
				throw new BadImageFormatException ("Invalid TypeMap constructor signature.");
			}
			for (int i = 1; i < count; i++) {
				if (blob.ReadSignatureTypeCode () != SignatureTypeCode.TypeHandle) {
					throw new BadImageFormatException ("Invalid TypeMap constructor type parameter.");
				}
				var typeHandle = blob.ReadTypeHandle ();
				string ns;
				string name;
				if (typeHandle.Kind == HandleKind.TypeReference) {
					var type = reader.GetTypeReference ((TypeReferenceHandle) typeHandle);
					ns = reader.GetString (type.Namespace);
					name = reader.GetString (type.Name);
				} else if (typeHandle.Kind == HandleKind.TypeDefinition) {
					var type = reader.GetTypeDefinition ((TypeDefinitionHandle) typeHandle);
					ns = reader.GetString (type.Namespace);
					name = reader.GetString (type.Name);
				} else {
					throw new BadImageFormatException ("Invalid TypeMap constructor type parameter.");
				}
				if (ns != "System" || name != "Type") {
					throw new BadImageFormatException ("Invalid TypeMap constructor type parameter.");
				}
			}
			if (blob.RemainingBytes != 0) {
				throw new BadImageFormatException ("Invalid TypeMap constructor signature.");
			}
			return count;
		}

		static string NormalizeKey (string key)
		{
			int start = key.LastIndexOf ('[');
			if (start <= 0 || start == key.Length - 2 || key [key.Length - 1] != ']') {
				return key;
			}
			for (int i = start + 1; i < key.Length - 1; i++) {
				if (key [i] < '0' || key [i] > '9') {
					return key;
				}
			}
			return key.Substring (0, start);
		}
	}
}
