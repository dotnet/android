using System;
using System.Collections.Generic;

namespace MonoDroid.Generation {

	// A Kotlin `@JvmInline value class` in a position where the JVM passes the
	// boxed value class (e.g. `Foo?`) instead of its underlying primitive. The
	// managed type is the nullable wrapper struct (`Foo?`), marshaled with the
	// struct's `box-impl`/`unbox-impl` based helpers.
	public class KotlinInlineClassBoxedSymbol : ISymbol {

		readonly ClassGen gen;

		public KotlinInlineClassBoxedSymbol (ClassGen gen)
		{
			this.gen = gen;
		}

		// Returns the symbol to use for `sym` in a position the JVM boxes.
		public static ISymbol Project (ISymbol sym)
		{
			return sym is ClassGen gen && gen.CanBoxKotlinInlineClass ? new KotlinInlineClassBoxedSymbol (gen) : sym;
		}

		// The struct does not implement IJavaObject, so generic collections can't
		// marshal it; generic type arguments use the boxed Java object instead.
		public static ISymbol ProjectTypeArgument (CodeGenerationOptions opt, ISymbol sym)
		{
			return sym is ClassGen gen && gen.IsKotlinInlineClass ? (opt.SymbolTable.Lookup ("java.lang.Object") ?? sym) : sym;
		}

		public string DefaultValue => "IntPtr.Zero";

		public string FullName => gen.FullName + "?";

		public string JavaName => gen.JavaName;

		public string JniName => gen.JniName;

		public string NativeType => "IntPtr";

		public bool IsEnum => false;

		public bool IsArray => false;

		public string ElementType => null;

		public string ReturnCast => string.Empty;

		public string GetObjectHandleProperty (CodeGenerationOptions opt, string variable) => variable;

		public string GetGenericType (Dictionary<string, string> mappings) => null;

		public string FromNative (CodeGenerationOptions opt, string var_name, bool owned)
		{
			return $"{opt.GetOutputName (gen.FullName)}.FromJniHandle ({var_name}, {(owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer")})";
		}

		public string ToNative (CodeGenerationOptions opt, string var_name, Dictionary<string, string> mappings = null)
		{
			return $"{opt.GetOutputName (gen.FullName)}.ToLocalJniHandle ({var_name})";
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			return gen.Validate (opt, type_params, context);
		}

		public string [] PreCallback (CodeGenerationOptions opt, string var_name, bool owned)
		{
			return new [] {
				$"var {opt.GetSafeIdentifier (var_name)} = {FromNative (opt, opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name)), owned)};",
			};
		}

		public string [] PostCallback (CodeGenerationOptions opt, string var_name) => [];

		public string [] PreCall (CodeGenerationOptions opt, string var_name)
		{
			return new [] {
				$"IntPtr {opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name))} = {ToNative (opt, opt.GetSafeIdentifier (var_name))};",
			};
		}

		public string Call (CodeGenerationOptions opt, string var_name)
		{
			return opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name));
		}

		public string [] PostCall (CodeGenerationOptions opt, string var_name)
		{
			return new [] {
				$"JNIEnv.DeleteLocalRef ({opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name))});",
			};
		}

		public bool NeedsPrep => true;
	}
}
