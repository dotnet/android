using System;
using Xamarin.Android.Tools.Bytecode;
using NUnit.Framework;
using System.Linq;

namespace Xamarin.Android.Tools.BytecodeTests
{
	[TestFixture]
	public class KotlinMetadataTests : ClassFileFixture
	{
		[Test]
		public void PublicKotlinClassFile ()
		{
			var meta = GetMetadataForClassFile ("PublicClass.class");

			Assert.AreEqual (KotlinMetadataKind.Class, meta.Kind);

			var klass_meta = meta.AsClassMetadata ();

			Assert.True (klass_meta.Flags.HasFlag (KotlinClassFlags.Public));
		}

		[Test]
		public void PrivateKotlinClassFile ()
		{
			var meta = GetMetadataForClassFile ("PrivateClass.class");

			Assert.AreEqual (KotlinMetadataKind.Class, meta.Kind);

			var klass_meta = meta.AsClassMetadata ();

			Assert.True (klass_meta.Flags.HasFlag (KotlinClassFlags.Private));
		}

		[Test]
		public void InternalKotlinClassFile ()
		{
			var meta = GetMetadataForClassFile ("InternalClass.class");

			Assert.AreEqual (KotlinMetadataKind.Class, meta.Kind);

			var klass_meta = meta.AsClassMetadata ();

			Assert.True (klass_meta.Flags.HasFlag (KotlinClassFlags.Internal));
		}

		[Test]
		public void ProtectedKotlinClassFile ()
		{
			var meta = GetMetadataForClassFile ("PublicClass$ProtectedClass.class");

			Assert.AreEqual (KotlinMetadataKind.Class, meta.Kind);

			var klass_meta = meta.AsClassMetadata ();

			Assert.True (klass_meta.Flags.HasFlag (KotlinClassFlags.Protected));
		}

		private KotlinMetadata GetMetadataForClassFile (string file)
		{
			var c = LoadClassFile (file);
			var attr = c.Attributes.OfType<RuntimeVisibleAnnotationsAttribute> ().FirstOrDefault ();
			var kotlin = attr?.Annotations.FirstOrDefault (a => a.Type == "Lkotlin/Metadata;");

			Assert.NotNull (kotlin);

			var meta = KotlinMetadata.FromAnnotation (kotlin);

			return meta;
		}
	}
}

