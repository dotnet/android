using System;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	// Emits a `readonly struct` wrapper for a Kotlin @JvmInline value class.
	// The struct holds the underlying primitive (e.g. long, float) that the
	// JVM passes across JNI, and provides implicit conversions so the wrapper
	// can be used directly in projected method signatures while existing JNI
	// thunks marshal the primitive value unchanged.
	public class KotlinInlineClassStruct : TypeWriter
	{
		readonly ClassGen klass;
		readonly string underlying_csharp_type;
		readonly string nullable;

		public KotlinInlineClassStruct (ClassGen klass, CodeGenerationOptions opt)
		{
			this.klass = klass;
			underlying_csharp_type = JniPrimitiveToCSharpType (klass.KotlinInlineClassUnderlyingJniType);
			nullable = opt.NullableOperator;

			Name = klass.Name;
			SetVisibility (klass.Visibility);

			klass.JavadocInfo?.AddJavadocs (Comments);
			Comments.Add ($"// Metadata.xml XPath class reference: path=\"{klass.MetadataXPathReference}\"");
			Comments.Add ("// Kotlin @JvmInline value class wrapper.");
		}

		public override void WriteSignature (CodeWriter writer)
		{
			if (IsPublic)
				writer.Write ("public ");
			if (IsProtected)
				writer.Write ("protected ");
			if (IsInternal)
				writer.Write ("internal ");
			if (IsPrivate)
				writer.Write ("private ");

			writer.Write ("readonly partial struct ");
			writer.Write (Name + " ");
			writer.WriteLine (": global::System.IEquatable<" + Name + "> {");
			writer.Indent ();
		}

		public override void WriteMembers (CodeWriter writer)
		{
			var t = underlying_csharp_type;
			var n = Name;
			var q = nullable;

			writer.WriteLine ($"public readonly {t} Value;");
			writer.WriteLine ();
			writer.WriteLine ($"public {n} ({t} value) {{ Value = value; }}");
			writer.WriteLine ();
			writer.WriteLine ($"public static implicit operator {t} ({n} value) => value.Value;");
			writer.WriteLine ($"public static implicit operator {n} ({t} value) => new {n} (value);");
			writer.WriteLine ();
			writer.WriteLine ($"public static bool operator == ({n} left, {n} right) => left.Equals (right);");
			writer.WriteLine ($"public static bool operator != ({n} left, {n} right) => !left.Equals (right);");
			writer.WriteLine ();
			writer.WriteLine ($"public bool Equals ({n} other) => Value.Equals (other.Value);");
			writer.WriteLine ($"public override bool Equals (object{q} obj) => obj is {n} other && Equals (other);");
			writer.WriteLine ("public override int GetHashCode () => Value.GetHashCode ();");
			writer.WriteLine ($"public override string{q} ToString () => Value.ToString ();");

			WriteJniHelpers (writer);
		}

		// Kotlin always generates `box-impl`, `unbox-impl` and `constructor-impl` for a
		// value class. They let values cross positions where the JVM passes the boxed
		// value class (e.g. `Foo?`) instead of the underlying primitive.
		void WriteJniHelpers (CodeWriter writer)
		{
			var t = underlying_csharp_type;
			var n = Name;
			var q = nullable;
			var u = klass.KotlinInlineClassUnderlyingJniType;
			var call = JniPrimitiveToCallType (u);

			writer.WriteLine ();
			writer.WriteLine ($"static global::Java.Interop.JniType{q} __jniType;");
			writer.WriteLine ($"static global::Java.Interop.JniMethodInfo{q} __constructorImpl;");
			writer.WriteLine ($"static global::Java.Interop.JniMethodInfo{q} __boxImpl;");
			writer.WriteLine ($"static global::Java.Interop.JniMethodInfo{q} __unboxImpl;");
			writer.WriteLine ($"static global::Java.Interop.JniType __GetJniType () => global::Java.Interop.JniType.GetCachedJniType (ref __jniType, \"{klass.RawJniName}\");");

			writer.WriteLine ();
			writer.WriteLine ("// Calls Kotlin's `constructor-impl`, which runs the `init` block checks.");
			writer.WriteLine ($"public static {n} Create ({t} value)");
			writer.WriteLine ("{");
			writer.WriteLine ("\tunsafe {");
			writer.WriteLine ("\t\t// SAFETY: The cached method ID matches the one-argument signature, and the stack argument stays live for the synchronous call.");
			writer.WriteLine ($"\t\tvar __m = __GetJniType ().GetCachedStaticMethod (ref __constructorImpl, \"constructor-impl\", \"({u}){u}\");");
			writer.WriteLine ("\t\tglobal::Java.Interop.JniArgumentValue* __args = stackalloc global::Java.Interop.JniArgumentValue [1];");
			writer.WriteLine ("\t\t__args [0] = new global::Java.Interop.JniArgumentValue (value);");
			writer.WriteLine ($"\t\treturn new {n} (global::Java.Interop.JniEnvironment.StaticMethods.CallStatic{call}Method (__GetJniType ().PeerReference, __m, __args));");
			writer.WriteLine ("\t}");
			writer.WriteLine ("}");

			writer.WriteLine ();
			writer.WriteLine ("// Calls Kotlin's `box-impl`. The caller owns the returned JNI local reference.");
			writer.WriteLine ($"public static global::System.IntPtr ToLocalJniHandle ({n}? value)");
			writer.WriteLine ("{");
			writer.WriteLine ($"\tif (value is not {n} __value)");
			writer.WriteLine ("\t\treturn global::System.IntPtr.Zero;");
			writer.WriteLine ("\tunsafe {");
			writer.WriteLine ("\t\t// SAFETY: The cached method ID matches the one-argument signature, and the stack argument stays live for the synchronous call.");
			writer.WriteLine ($"\t\tvar __m = __GetJniType ().GetCachedStaticMethod (ref __boxImpl, \"box-impl\", \"({u})L{klass.RawJniName};\");");
			writer.WriteLine ("\t\tglobal::Java.Interop.JniArgumentValue* __args = stackalloc global::Java.Interop.JniArgumentValue [1];");
			writer.WriteLine ("\t\t__args [0] = new global::Java.Interop.JniArgumentValue (__value.Value);");
			writer.WriteLine ("\t\treturn global::Java.Interop.JniEnvironment.StaticMethods.CallStaticObjectMethod (__GetJniType ().PeerReference, __m, __args).Handle;");
			writer.WriteLine ("\t}");
			writer.WriteLine ("}");

			writer.WriteLine ();
			writer.WriteLine ("// Calls Kotlin's `unbox-impl` on a boxed value, then releases `handle` as `transfer` specifies.");
			writer.WriteLine ($"public static {n}? FromJniHandle (global::System.IntPtr handle, global::Android.Runtime.JniHandleOwnership transfer)");
			writer.WriteLine ("{");
			writer.WriteLine ("\tif (handle == global::System.IntPtr.Zero)");
			writer.WriteLine ("\t\treturn null;");
			writer.WriteLine ("\tunsafe {");
			writer.WriteLine ("\t\t// SAFETY: `handle` is a live reference to the boxed value class, released at most once below.");
			writer.WriteLine ("\t\ttry {");
			writer.WriteLine ($"\t\t\tvar __m = __GetJniType ().GetCachedInstanceMethod (ref __unboxImpl, \"unbox-impl\", \"(){u}\");");
			writer.WriteLine ($"\t\t\treturn new {n} (global::Java.Interop.JniEnvironment.InstanceMethods.Call{call}Method (new global::Java.Interop.JniObjectReference (handle), __m));");
			writer.WriteLine ("\t\t} finally {");
			writer.WriteLine ("\t\t\tif (transfer == global::Android.Runtime.JniHandleOwnership.TransferLocalRef)");
			writer.WriteLine ("\t\t\t\tglobal::Android.Runtime.JNIEnv.DeleteLocalRef (handle);");
			writer.WriteLine ("\t\t\telse if (transfer == global::Android.Runtime.JniHandleOwnership.TransferGlobalRef)");
			writer.WriteLine ("\t\t\t\tglobal::Android.Runtime.JNIEnv.DeleteGlobalRef (handle);");
			writer.WriteLine ("\t\t}");
			writer.WriteLine ("\t}");
			writer.WriteLine ("}");
		}

		static string JniPrimitiveToCallType (string jni)
		{
			return jni switch {
				"Z" => "Boolean",
				"B" => "Byte",
				"C" => "Char",
				"D" => "Double",
				"F" => "Float",
				"I" => "Int",
				"J" => "Long",
				"S" => "Short",
				_ => throw new ArgumentOutOfRangeException (nameof (jni), jni, "Unsupported JNI primitive descriptor"),
			};
		}

		static string JniPrimitiveToCSharpType (string jni)
		{
			return jni switch {
				"Z" => "bool",
				"B" => "sbyte",
				"C" => "char",
				"D" => "double",
				"F" => "float",
				"I" => "int",
				"J" => "long",
				"S" => "short",
				_ => throw new ArgumentOutOfRangeException (nameof (jni), jni, "Unsupported JNI primitive descriptor"),
			};
		}
	}
}
