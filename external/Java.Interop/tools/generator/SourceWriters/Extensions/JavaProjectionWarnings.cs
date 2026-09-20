using System;
using System.Collections.Generic;
using System.Linq;
using Java.Interop.Tools.Generator;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	// A handful of C# diagnostics are unavoidable consequences of faithfully projecting a
	// Java API into C#: Java allows a `finalize ()` method, allows a deprecated member to
	// be overridden by a non-deprecated one (and vice versa), and its nullness annotations
	// are advisory rather than enforced, so an override may legitimately disagree with the
	// member it overrides.
	//
	// Disabling those diagnostics for a whole project would also hide genuine problems in
	// hand-written code. Instead, the generator works out exactly which member provokes
	// which diagnostic and emits a `#pragma warning disable` scoped to that single member,
	// so an unexpected occurrence anywhere else still breaks the build.
	static class JavaProjectionWarnings
	{
		const string CS0465 = "0465";
		const string CS0618 = "0618";
		const string CS0672 = "0672";
		const string CS0809 = "0809";
		const string CS8764 = "8764";
		const string CS8765 = "8765";
		const string CS8766 = "8766";
		const string CS8767 = "8767";

		const string FinalizeReason =
			"CS0465: Java types may declare a `finalize ()` method, which is bound as a normal `Finalize ()` method.";
		const string ObsoleteUseReason =
			"CS0618: This member's signature refers to a Java API that has been deprecated.";
		const string ObsoleteOverrideReason =
			"CS0672: Java allows a non-deprecated member to override a deprecated one.";
		const string GeneratedHelperReason =
			"CS0618: This type is generated to support a Java API that has been deprecated.";
		const string ObsoleteOverriddenReason =
			"CS0809: Java allows a deprecated member to override a non-deprecated one.";
		const string ReturnNullabilityReason =
			"CS8764/CS8766: Java's nullness annotations are advisory, so an overridden method may annotate its return type differently than the method it overrides.";
		const string ParameterNullabilityReason =
			"CS8765/CS8767: Java's nullness annotations are advisory, so an overridden method may annotate its parameters differently than the method it overrides.";

		public static void Add (this ISuppressWarnings writer, string code, string reason)
		{
			if (writer.SuppressWarnings.Any (w => w.Code == code))
				return;

			writer.SuppressWarnings.Add (new WarningSuppression (code, reason));
		}

		// C# reserves `Finalize ()` for the finalizer; Java does not.
		public static void AddFinalizeSuppression (ISuppressWarnings writer, Method method)
		{
			if (method.AdjustedName == "Finalize" && method.Parameters.Count == 0 && method.RetVal.IsVoid && !method.IsStatic)
				writer.Add (CS0465, FinalizeReason);
		}

		public static void AddObsoleteSuppressions (ISuppressWarnings writer, Method method, CodeGenerationOptions opt)
		{
			var self_obsolete = IsBoundAsObsolete (method.Deprecated, method.DeprecatedSince, opt);

			if (!self_obsolete && SignatureUsesObsoleteType (method, opt))
				writer.Add (CS0618, ObsoleteUseReason);

			var base_method = method.OverriddenBaseMethod ?? method.OverriddenInterfaceMethod;

			if (base_method == null)
				return;

			var base_obsolete = IsBoundAsObsolete (base_method.Deprecated, base_method.DeprecatedSince, opt);

			if (!self_obsolete && base_obsolete)
				writer.Add (CS0672, ObsoleteOverrideReason);
			else if (self_obsolete && !base_obsolete)
				writer.Add (CS0809, ObsoleteOverriddenReason);
		}

		// The generator synthesizes helper types (invokers, implementors, event arguments)
		// for a bound Java type. When the Java type is deprecated the helper unavoidably
		// derives from, implements, or mentions it, so suppress CS0618 for the helper.
		public static void AddGeneratedHelperSuppressions (ISuppressWarnings writer, GenBase gen, CodeGenerationOptions opt)
		{
			if (IsBoundAsObsolete (gen, opt))
				writer.Add (CS0618, GeneratedHelperReason);
		}

		// The bound type is not deprecated itself, but derives from or implements a Java type
		// that is.
		public static void AddObsoleteBaseTypeSuppressions (ISuppressWarnings writer, GenBase gen, CodeGenerationOptions opt)
		{
			if (IsBoundAsObsolete (gen, opt))
				return;

			if (IsBoundAsObsolete (gen.BaseSymbol, opt) || gen.Interfaces.Any (i => IsBoundAsObsolete (i, opt)))
				writer.Add (CS0618, ObsoleteUseReason);
		}

		// The generated member's signature or body refers to a deprecated Java API.
		public static void AddObsoleteUseSuppressions (ISuppressWarnings writer, Method method, CodeGenerationOptions opt)
		{
			if (IsBoundAsObsolete (method.Deprecated, method.DeprecatedSince, opt))
				return;

			if (SignatureUsesObsoleteType (method, opt))
				writer.Add (CS0618, ObsoleteUseReason);
		}

		public static void AddObsoleteSuppressions (ISuppressWarnings writer, Property property, CodeGenerationOptions opt)
		{
			AddObsoleteSuppressions (writer, property.Getter, opt);

			if (property.Setter != null)
				AddObsoleteSuppressions (writer, property.Setter, opt);
		}

		// Java's `@Nullable`/`@NonNull` annotations are documentation, not part of the type
		// system, so a Java method is free to annotate its return type or parameters
		// differently than the method it overrides. Binding that faithfully produces a C#
		// nullability mismatch that only the Java author can resolve.
		public static void AddNullabilitySuppressions (ISuppressWarnings writer, GenBase type, Method method, CodeGenerationOptions opt)
		{
			if (!opt.SupportNullableReferenceTypes)
				return;

			if (type == null)
				return;

			foreach (var (base_method, via_interface) in GetOverriddenMembers (type, method)) {
				if (ReturnNullabilityDiffers (method, base_method))
					writer.Add (via_interface ? CS8766 : CS8764, ReturnNullabilityReason);

				if (ParameterNullabilityDiffers (method, base_method))
					writer.Add (via_interface ? CS8767 : CS8765, ParameterNullabilityReason);
			}
		}

		public static void AddNullabilitySuppressions (ISuppressWarnings writer, GenBase type, Property property, CodeGenerationOptions opt)
		{
			AddNullabilitySuppressions (writer, type, property.Getter, opt);

			if (property.Setter != null)
				AddNullabilitySuppressions (writer, type, property.Setter, opt);
		}

		static bool ReturnNullabilityDiffers (Method method, Method baseMethod) =>
			IsReferenceType (method.RetVal.Symbol) && method.RetVal.NotNull != baseMethod.RetVal.NotNull;

		static bool ParameterNullabilityDiffers (Method method, Method baseMethod)
		{
			if (method.Parameters.Count != baseMethod.Parameters.Count)
				return false;

			for (var i = 0; i < method.Parameters.Count; i++) {
				var p = method.Parameters [i];

				if (IsReferenceType (p.Symbol) && p.NotNull != baseMethod.Parameters [i].NotNull)
					return true;
			}

			return false;
		}

		// Nullability only applies to reference types; value types are never annotated.
		static bool IsReferenceType (ISymbol symbol) => symbol switch {
			null => false,
			// Arrays are reference types even when their element type is not.
			_ when symbol.IsArray => true,
			SimpleSymbol => false,
			_ => !symbol.IsEnum,
		};

		// All the members `method` overrides or implements, paired with whether the member
		// comes from an interface (which changes which diagnostic the compiler reports).
		static IEnumerable<(Method Method, bool ViaInterface)> GetOverriddenMembers (GenBase type, Method method)
		{
			var base_method = method.OverriddenBaseMethod
				?? type.BaseSymbol?.FindOverriddenMethod (method, true, true, match_visibility: false);

			if (base_method != null && base_method != method)
				yield return (base_method, false);

			if (method.OverriddenInterfaceMethod != null)
				yield return (method.OverriddenInterfaceMethod, true);

			foreach (var iface in GetImplementedInterfaces (type)) {
				var im = iface.Methods.FirstOrDefault (m => m != method && IsSameJavaMethod (m, method));

				if (im != null)
					yield return (im, true);
			}
		}

		// Java allows an override to narrow its return type, so only the parameters of the
		// JNI signature identify the method being overridden.
		static bool IsSameJavaMethod (Method a, Method b) =>
			a.JavaName == b.JavaName && JniParameters (a) == JniParameters (b);

		static string JniParameters (Method method)
		{
			var signature = method.JniSignature ?? "";
			var end = signature.IndexOf (')');

			return end < 0 ? signature : signature.Substring (0, end + 1);
		}

		// Every interface `type` implements, directly or transitively. C# reports a
		// nullability mismatch against each of them separately, so all must be considered.
		static IEnumerable<InterfaceGen> GetImplementedInterfaces (GenBase type)
		{
			var seen = new HashSet<string> ();
			var pending = new Queue<GenBase> ();

			for (var t = type; t != null; t = t.BaseSymbol)
				pending.Enqueue (t);

			// An invoker generated for an interface implements that interface itself, so its
			// members are compared against the interface's own declarations too.
			if (type is InterfaceGen self && seen.Add (self.FullName))
				yield return self;

			while (pending.Count > 0) {
				var current = pending.Dequeue ();

				foreach (var isym in current.Interfaces) {
					if ((isym is GenericSymbol gs ? gs.Gen : isym) is not InterfaceGen iface)
						continue;

					if (!seen.Add (iface.FullName))
						continue;

					pending.Enqueue (iface);

					yield return iface;
				}
			}
		}

		static bool SignatureUsesObsoleteType (Method method, CodeGenerationOptions opt)
		{
			if (IsBoundAsObsolete (method.RetVal.Symbol, opt))
				return true;

			return method.Parameters.Any (p => IsBoundAsObsolete (p.Symbol, opt));
		}

		public static bool IsBoundAsObsolete (ISymbol symbol, CodeGenerationOptions opt)
		{
			var gen = Unwrap (symbol);

			return gen != null && IsBoundAsObsolete (gen.DeprecatedComment, gen.DeprecatedSince, opt);
		}

		static bool IsBoundAsObsolete (string deprecated, AndroidSdkVersion? deprecatedSince, CodeGenerationOptions opt) =>
			SourceWriterExtensions.EmitsObsoleteAttribute (deprecated, opt, deprecatedSince);

		// Unwraps the layers of symbols (arrays, generic instantiations, ...) that can sit
		// between a type reference and the bound type it ultimately refers to.
		static GenBase Unwrap (ISymbol symbol)
		{
			while (true) {
				switch (symbol) {
				case null:
					return null;
				case GenBase gen:
					return gen;
				case GenericSymbol generic:
					return generic.Gen;
				case ArraySymbol array:
					symbol = array.ElementSymbol;
					continue;
				default:
					return null;
				}
			}
		}
	}
}
