#if ENABLE_MARSHAL_METHODS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;

using Java.Interop.Tools.TypeNameMappings;
using Java.Interop.Tools.JavaCallableWrappers;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks
{
	class MarshalMethodsNativeAssemblyGenerator : LlvmIrComposer
	{
		sealed class MarshalMethodInfo
		{
			public MarshalMethodEntry Method { get; }
			public string NativeSymbolName   { get; }
		}

		// This is here only to generate strongly-typed IR
		internal sealed class MonoClass
		{}

		struct MarshalMethodsManagedClass
		{
			uint       token;

			[NativePointer (IsNull = true)]
			MonoClass  klass;
		};

		public ICollection<string> UniqueAssemblyNames                       { get; set; }
		public int NumberOfAssembliesInApk                                   { get; set; }
		public IDictionary<string, IList<MarshalMethodEntry>> MarshalMethods { get; set; }

		StructureInfo<TypeMappingReleaseNativeAssemblyGenerator.MonoImage> monoImage;
		StructureInfo<MonoClass> monoClass;

		public override void Init ()
		{
			Console.WriteLine ($"Marshal methods count: {MarshalMethods?.Count ?? 0}");
		}

		protected override void MapStructures (LlvmIrGenerator generator)
		{
			monoImage = generator.MapStructure<TypeMappingReleaseNativeAssemblyGenerator.MonoImage> ();
			monoClass = generator.MapStructure<MonoClass> ();
		}

		protected override void Write (LlvmIrGenerator generator)
		{
			WriteAssemblyImageCache (generator);
		}

		void WriteAssemblyImageCache (LlvmIrGenerator generator)
		{
			if (UniqueAssemblyNames == null) {
				throw new InvalidOperationException ("Internal error: unique assembly names not provided");
			}

			if (UniqueAssemblyNames.Count != NumberOfAssembliesInApk) {
				throw new InvalidOperationException ("Internal error: number of assemblies in the apk doesn't match the number of unique assembly names");
			}

			bool is64Bit = generator.Is64Bit;
			generator.WriteStructureArray (monoImage, (ulong)NumberOfAssembliesInApk, "assembly_image_cache", isArrayOfPointers: true);

			if (is64Bit) {
				WriteHashes<ulong> ();
			} else {
				WriteHashes<uint> ();
			}

			void WriteHashes<T> () where T: struct
			{
				var hashes = new Dictionary<T, (string name, uint index)> ();
				uint index = 0;
				foreach (string name in UniqueAssemblyNames) {
					string clippedName = Path.GetFileNameWithoutExtension (name);
					ulong hashFull = HashName (name, is64Bit);
					ulong hashClipped = HashName (clippedName, is64Bit);

					//
					// If the number of name forms changes, xamarin-app.hh MUST be updated to set value of the
					// `number_of_assembly_name_forms_in_image_cache` constant to the number of forms.
					//
					hashes.Add ((T)Convert.ChangeType (hashFull, typeof(T)), (name, index));
					hashes.Add ((T)Convert.ChangeType (hashClipped, typeof(T)), (clippedName, index));

					index++;
				}
				List<T> keys = hashes.Keys.ToList ();
				keys.Sort ();

				generator.WriteCommentLine ("Each entry maps hash of an assembly name to an index into the `assembly_image_cache` array");
				generator.WriteArray (
					keys,
					LlvmIrVariableOptions.GlobalConstant,
					"assembly_image_cache_hashes",
					(int idx, T value) => $"{idx}: {hashes[value].name} => 0x{value:x} => {hashes[value].index}"
				);

				var indices = new List<uint> ();
				for (int i = 0; i < keys.Count; i++) {
					indices.Add (hashes[keys[i]].index);
				}
				generator.WriteArray (
					indices,
					LlvmIrVariableOptions.GlobalConstant,
					"assembly_image_cache_indices"
				);
			}
		}
	}
}
#endif
