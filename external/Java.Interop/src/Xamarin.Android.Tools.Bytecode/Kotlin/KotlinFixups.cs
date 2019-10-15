using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools.Bytecode.Kotlin
{
	static class KotlinFixups
	{
		public static void Fixup (IList<ClassFile> classes)
		{
			foreach (var c in classes) {
				// See if this is a Kotlin class
				var attr = c.Attributes.OfType<RuntimeVisibleAnnotationsAttribute> ().FirstOrDefault ();
				var kotlin = attr?.Annotations.SingleOrDefault (a => a.Type == "Lkotlin/Metadata;");

				if (kotlin is null)
					continue;

				try {
					var km = KotlinMetadata.FromAnnotation (kotlin);

					if (km.Kind == KotlinMetadataKind.Class) {
						var class_metadata = km.AsClassMetadata ();

						FixupClassVisibility (c, class_metadata);
					} else {
						// We don't have explicit support for other types of Kotlin constructs yet,
						// so they are unlikely to work. Mark them as private and consumers
						// can override that if they want to fix them up.
						c.AccessFlags = ClassAccessFlags.Private;
					}
				} catch (Exception ex) {
					Log.Warning (0, $"class-parse: warning: Unable to parse Kotlin metadata on '{c.ThisClass.Name}': {ex}");
				}
			}
		}

		static void FixupClassVisibility (ClassFile klass, KotlinClass metadata)
		{
			// We don't have explicit support for these types of Kotlin constructs yet,
			// so they are unlikely to work. Mark them as private and consumers
			// can override that if they want to fix them up.
			if (metadata.Flags.HasFlag (KotlinClassFlags.AnnotationClass) || metadata.Flags.HasFlag (KotlinClassFlags.CompanionObject) || metadata.Flags.HasFlag (KotlinClassFlags.Object))
				klass.AccessFlags = ClassAccessFlags.Private;

			// Hide class if it isn't Public/Protected
			if (!metadata.Flags.HasFlag (KotlinClassFlags.Public) && !metadata.Flags.HasFlag (KotlinClassFlags.Protected))
				klass.AccessFlags = ClassAccessFlags.Private;
		}
	}
}
