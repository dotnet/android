using System;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Xamarin.Android.Tools.Bytecode
{
	// https://github.com/JetBrains/kotlin/blob/master/libraries/stdlib/jvm/runtime/kotlin/Metadata.kt
	public class KotlinMetadata
	{
		public KotlinMetadataKind Kind { get; set; }

		// The version of the metadata provided in the arguments of this annotation.
		public Version MetadataVersion { get; set; }

		// The version of the bytecode interface (naming conventions, signatures) of the class file annotated with this annotation.
		public Version ByteCodeVersion { get; set; }

		// Metadata in a custom format. The format may be different (or even absent) for different kinds.
		public string [] Data1 { get; set; }

		// An addition to [d1]: array of strings which occur in metadata, written in plain text so that strings already present
		// in the constant pool are reused. These strings may be then indexed in the metadata by an integer index in this array.
		public string [] Data2 { get; set; }

		public static KotlinMetadata FromAnnotation (Annotation annotation)
		{
			var km = new KotlinMetadata {
				ByteCodeVersion = ParseVersion (annotation, "bv"),
				Kind = (KotlinMetadataKind) ParseInteger (GetValue (annotation, "k")),
				MetadataVersion = ParseVersion (annotation, "mv")
			};

			km.Data1 = GetValues (annotation, "d1");
			km.Data2 = GetValues (annotation, "d2");

			return km;
		}

		public KotlinFile ParseMetadata ()
		{
			switch (Kind) {
				case KotlinMetadataKind.Class:
					return AsClassMetadata ();
				case KotlinMetadataKind.File:
					return AsFileMetadata ();
				default:
					return null;
			}
		}

		public KotlinClass AsClassMetadata ()
		{
			if (Kind != KotlinMetadataKind.Class)
				return null;

			var data = ParseStream<org.jetbrains.kotlin.metadata.jvm.Class> ();
			return KotlinClass.FromProtobuf (data.Item1, data.Item2);
		}

		public KotlinFile AsFileMetadata ()
		{
			if (Kind != KotlinMetadataKind.File)
				return null;

			var data = ParseStream<org.jetbrains.kotlin.metadata.jvm.Package> ();
			return KotlinFile.FromProtobuf (data.Item1, data.Item2);
		}

		Tuple<T, JvmNameResolver> ParseStream<T> ()
		{
			var md = KotlinBitEncoding.DecodeBytes (Data1);

			using (var ms = ToMemoryStream (md)) {

				// The first element is the length of the string table
				var first = ms.ReadByte ();

				if (first == -1)
					return null;

				ms.Position = 0;

				var size = KotlinBitEncoding.ReadRawVarint32 (ms);

				using (var partial = new PartialStream (ms, ms.Position, size)) {

					// Read the string table from the stream
					var string_table = Serializer.Deserialize<org.jetbrains.kotlin.metadata.jvm.StringTableTypes> (partial);
					var resolver = new JvmNameResolver (string_table, Data2.ToList ());

					partial.MoveNext ();

					// Read the metadata structure from the stream
					var metadata = Serializer.Deserialize<T> (partial);
					return Tuple.Create (metadata, resolver);
				}
			}
		}

		static MemoryStream ToMemoryStream (byte [] bytes) => new MemoryStream (bytes);

		static Version ParseVersion (Annotation annotation, string key)
		{
			var value = GetValue (annotation, key);

			if (value is null)
				return null;

			var values = value.Trim ('[', ']').Split (',').Select (v => ParseInteger (v)).ToArray ();

			return new Version (values [0], values [1], values [2]);
		}

		static string GetValue (Annotation annotation, string key)
		{
			return annotation.Values.FirstOrDefault (v => v.Key == key).Value?.ToString ();
		}

		static string[] GetValues (Annotation annotation, string key)
		{
			return (annotation.Values.FirstOrDefault (v => v.Key == key).Value as AnnotationElementArray)?.Values.Cast<AnnotationElementConstant> ().Select (c => c.Value).ToArray ();
		}

		static int ParseInteger (string value)
		{
			value = value.Replace ("Integer", "").Trim ('(', ')', ' ');

			return int.Parse (value);
		}
	}

	public enum KotlinMetadataKind
	{
		Class = 1,
		File = 2,
		SyntheticClass = 3,
		MultiFileClassFacade = 4,
		MultiFileClassPart = 5
	}
}
