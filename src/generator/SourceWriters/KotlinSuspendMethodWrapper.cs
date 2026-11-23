using System;
using System.Collections.Generic;
using System.Linq;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	// Exposes a Kotlin `suspend` function as a `Task`-returning `*Async` method.
	// The bound method keeps its trailing `Continuation` parameter;
	// `Android.Runtime.KotlinCoroutines` supplies that parameter at run time.
	public class KotlinSuspendMethodWrapper : MethodWriter
	{
		const string Runtime = "global::Android.Runtime.KotlinCoroutines";

		public static bool ShouldGenerate (Method method, GenBase type) =>
			method.KotlinSuspendReturnType != null &&
			method.IsValid &&
			!method.IsOverride &&
			string.IsNullOrEmpty (method.ExplicitInterface) &&
			method.Parameters.Count > 0 &&
			!method.Asyncify &&
			!type.ContainsName (method.AdjustedName + "Async");

		public KotlinSuspendMethodWrapper (Method method, CodeGenerationOptions opt, string selfType = null)
		{
			Name = method.AdjustedName + "Async";
			IsStatic = method.IsStatic || selfType != null;

			SetVisibility (method.Visibility);

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);
			SourceWriterExtensions.AddObsolete (Attributes, method.Deprecated, opt, deprecatedSince: method.DeprecatedSince);
			SourceWriterExtensions.AddRestrictToWarning (Attributes, method.AnnotatedVisibility, false, opt);

			var parameters = new ParameterList ();
			foreach (var p in method.Parameters.Take (method.Parameters.Count - 1))
				parameters.Add (p);

			// The generated names must not collide with Kotlin parameter names.
			var names = new HashSet<string> (parameters.Select (p => opt.GetSafeIdentifier (p.Name)));
			string GetFreeName (string name)
			{
				while (!names.Add (name))
					name = "_" + name;
				return name;
			}
			var self = GetFreeName ("self");
			var token = GetFreeName ("cancellationToken");
			var continuation = GetFreeName ("__continuation");

			if (selfType != null)
				Parameters.Add (new MethodParameterWriter (self, new TypeReferenceWriter (selfType)) { IsExtension = true });
			this.AddMethodParameters (parameters, opt);
			Parameters.Add (new MethodParameterWriter (token, new TypeReferenceWriter ("global::System.Threading.CancellationToken")) { DefaultValue = "default" });

			var continuation_type = opt.GetOutputName (method.Parameters [method.Parameters.Count - 1].Type);
			var args = parameters.GetCall (opt);
			if (args.Length > 0)
				args += ", ";
			args += $"global::Android.Runtime.Extensions.JavaCast<{continuation_type}> ({continuation})";
			var target = selfType != null ? $"{self}.{method.AdjustedName}" : method.AdjustedName;
			var invoke = $"{continuation} => {target} ({args})";

			// A `kotlin/Unit` result is a non-generic `Task`, which `Task<Java.Lang.Object?>` is.
			var result = GetResultType (method.KotlinSuspendReturnType, opt);
			var call = result == null || method.KotlinSuspendReturnType.EndsWith ("?", StringComparison.Ordinal)
				? "CallNullableSuspendFunctionAsync"
				: "CallSuspendFunctionAsync";
			ReturnType = new TypeReferenceWriter ("global::System.Threading.Tasks.Task" + (result == null ? "" : $"<{result}>"));
			Body.Add ($"return {Runtime}.{call}<{result ?? "global::Java.Lang.Object"}> ({invoke}, {token});");
		}

		// Maps the Kotlin result type (e.g. `kotlin/Int?`) to a C# type.
		// Returns null for `kotlin/Unit`, which becomes a non-generic `Task`.
		static string GetResultType (string kotlinType, CodeGenerationOptions opt)
		{
			var nullable = kotlinType.EndsWith ("?", StringComparison.Ordinal);
			var name = nullable ? kotlinType.Substring (0, kotlinType.Length - 1) : kotlinType;

			var value_type = name switch {
				"kotlin/Boolean" => "bool",
				"kotlin/Byte"    => "sbyte",
				"kotlin/Char"    => "char",
				"kotlin/Short"   => "short",
				"kotlin/Int"     => "int",
				"kotlin/Long"    => "long",
				"kotlin/Float"   => "float",
				"kotlin/Double"  => "double",
				_                => null,
			};
			if (value_type != null)
				return nullable ? value_type + "?" : value_type;

			if (name == "kotlin/Unit" && !nullable)
				return null;

			string reference;
			if (name == "kotlin/String")
				reference = "string";
			// Inline classes are boxed in this position; generic, invalid or non-public types are not generated.
			else if (opt.SymbolTable.Lookup (name.Replace ('/', '.')) is GenBase { IsValid: true, IsGeneric: false, RawVisibility: "public" } gen && gen is not ClassGen { IsKotlinInlineClass: true })
				reference = opt.GetOutputName (gen.FullName);
			else
				reference = "global::Java.Lang.Object";

			return nullable ? reference + opt.NullableOperator : reference;
		}
	}
}
