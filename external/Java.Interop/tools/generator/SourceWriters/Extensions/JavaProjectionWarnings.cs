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
		const string CS8603 = "8603";
		const string CS8604 = "8604";
		const string CS8625 = "8625";
		const string CS8768 = "8768";
		const string CS8769 = "8769";

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
			"CS8764/CS8766/CS8768: Java's nullness annotations are advisory, so an overridden method may annotate its return type differently than the method it overrides.";
		const string NoHandlerReturnReason =
			"CS8603: The Java API declares this listener method's return value as non-null, but the generated implementor has no value to return until a handler is attached.";
		const string GenericMarshalArgumentReason =
			"CS8604: A generic Java type argument is marshalled through a conversion the compiler cannot prove to be non-null, while the instantiated member is annotated as non-null.";
		const string MarshalArgumentReason =
			"CS8604: Java's nullness annotations are advisory, so the member this marshals to may annotate a parameter as non-null where the overriding member does not.";
		const string NoHandlerArgumentReason =
			"CS8625: This overload exists to pass no value for a parameter the Java API annotates as non-null but accepts as null.";
		const string ParameterNullabilityReason =
			"CS8765/CS8767/CS8769: Java's nullness annotations are advisory, so an overridden method may annotate its parameters differently than the method it overrides.";

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

		public static void AddObsoleteSuppressions (ISuppressWarnings writer, Method method, CodeGenerationOptions opt, GenBase declaringType = null)
		{
			var self_obsolete = IsBoundAsObsolete (method.Deprecated, method.DeprecatedSince, opt);

			// C# does not report *use* of a deprecated binding from inside a type that is
			// itself `[Obsolete]`. CS0672 and CS0809 describe the declaration rather than a
			// use, so they are still reported there and are handled below.
			var in_obsolete_type = declaringType != null && IsBoundAsObsolete (declaringType, opt);

			if (!self_obsolete && !in_obsolete_type && SignatureUsesObsoleteType (method, opt))
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
		public static void AddObsoleteUseSuppressions (ISuppressWarnings writer, MethodBase method, CodeGenerationOptions opt)
		{
			if (IsBoundAsObsolete (method.Deprecated, method.DeprecatedSince, opt))
				return;

			if (SignatureUsesObsoleteType (method, opt))
				writer.Add (CS0618, ObsoleteUseReason);
		}

		// The generator synthesizes event argument types for the methods of a listener
		// interface. The synthesized type is not the bound listener method, so -- unlike the
		// members generated directly from that method -- it does not carry the method's
		// deprecation and cannot rely on it to silence references to deprecated types.
		public static void AddSynthesizedTypeObsoleteSuppressions (ISuppressWarnings writer, GenBase gen, Method method, CodeGenerationOptions opt)
		{
			if (IsBoundAsObsolete (gen, opt) ||
					IsBoundAsObsolete (method.Deprecated, method.DeprecatedSince, opt) ||
					SignatureUsesObsoleteType (method, opt))
				writer.Add (CS0618, GeneratedHelperReason);
		}

		// The generated event wires up a listener interface by calling the Java method that
		// registers it. Either can be deprecated without the event itself being bound as
		// deprecated.
		public static void AddListenerEventSuppressions (ISuppressWarnings writer, GenBase listener, Method registrationMethod, CodeGenerationOptions opt)
		{
			if (IsBoundAsObsolete (listener, opt)) {
				writer.Add (CS0618, ObsoleteUseReason);
				return;
			}

			if (registrationMethod == null)
				return;

			if (IsBoundAsObsolete (registrationMethod.Deprecated, registrationMethod.DeprecatedSince, opt) ||
					SignatureUsesObsoleteType (registrationMethod, opt))
				writer.Add (CS0618, ObsoleteUseReason);
		}

		// The marshalling callback the generator emits for a bound member dispatches to that
		// member. C# resolves a call to an overriding member against the declaration it
		// overrides, so dispatching to a member that overrides a deprecated one reports the
		// base member's deprecation at the call site.
		public static void AddCallbackObsoleteSuppressions (ISuppressWarnings writer, Method method, CodeGenerationOptions opt)
		{
			if (IsBoundAsObsolete (method.Deprecated, method.DeprecatedSince, opt))
				return;

			var base_method = method.OverriddenBaseMethod ?? method.OverriddenInterfaceMethod;

			if (SignatureUsesObsoleteType (method, opt) ||
					(base_method != null && IsBoundAsObsolete (base_method.Deprecated, base_method.DeprecatedSince, opt)))
				writer.Add (CS0618, ObsoleteUseReason);
		}

		public static void AddObsoleteSuppressions (ISuppressWarnings writer, Property property, CodeGenerationOptions opt, GenBase declaringType = null)
		{
			AddObsoleteSuppressions (writer, property.Getter, opt, declaringType);

			if (property.Setter != null)
				AddObsoleteSuppressions (writer, property.Setter, opt, declaringType);
		}

		// Java's `@Nullable`/`@NonNull` annotations are documentation, not part of the type
		// system, so a Java method is free to annotate its return type or parameters
		// differently than the method it overrides. Binding that faithfully produces a C#
		// nullability mismatch that only the Java author can resolve.
		// An explicit interface implementation is reported under a different pair of codes
		// than an implicit one, which is reported differently again from a class override.
		public static void AddNullabilitySuppressions (ISuppressWarnings writer, GenBase type, Method method, CodeGenerationOptions opt, bool explicitInterfaceImplementation = false)
		{
			if (!opt.SupportNullableReferenceTypes)
				return;

			if (type == null)
				return;

			foreach (var (base_method, via_interface) in GetOverriddenMembers (type, method)) {
				var return_code = !via_interface ? CS8764 : explicitInterfaceImplementation ? CS8768 : CS8766;
				var parameter_code = !via_interface ? CS8765 : explicitInterfaceImplementation ? CS8769 : CS8767;

				if (ReturnNullabilityDiffers (method, base_method))
					writer.Add (return_code, ReturnNullabilityReason);

				if (ParameterNullabilityDiffers (method, base_method))
					writer.Add (parameter_code, ParameterNullabilityReason);
			}
		}

		public static void AddNullabilitySuppressions (ISuppressWarnings writer, GenBase type, Property property, CodeGenerationOptions opt, bool explicitInterfaceImplementation = false)
		{
			AddNullabilitySuppressions (writer, type, property.Getter, opt, explicitInterfaceImplementation);

			if (property.Setter != null)
				AddNullabilitySuppressions (writer, type, property.Setter, opt, explicitInterfaceImplementation);
		}

		// The implementor generated for a Java listener has to return something before a
		// handler is attached. `default` is null for a reference type, which the Java API
		// may nonetheless have annotated as non-null.
		public static void AddNoHandlerReturnSuppression (ISuppressWarnings writer, Method method, CodeGenerationOptions opt)
		{
			if (!opt.SupportNullableReferenceTypes)
				return;

			if (method.IsVoid || method.IsEventHandlerWithHandledProperty)
				return;

			if (IsReferenceType (method.RetVal.Symbol) && method.RetVal.NotNull)
				writer.Add (CS8603, NoHandlerReturnReason);
		}

		// The generated overload that omits a parameter passes `null` for it, which the Java
		// API may have annotated as non-null even though it accepts null at runtime.
		public static void AddNoHandlerArgumentSuppression (ISuppressWarnings writer, Parameter parameter, CodeGenerationOptions opt)
		{
			if (!opt.SupportNullableReferenceTypes)
				return;

			if (IsReferenceType (parameter.Symbol) && parameter.NotNull)
				writer.Add (CS8625, NoHandlerArgumentReason);
		}

		// An instantiated generic interface member marshals its arguments through casts and
		// `ToString ()` calls that the compiler cannot prove to be non-null.
		public static void AddGenericMarshalSuppressions (ISuppressWarnings writer, Method method, CodeGenerationOptions opt)
		{
			if (!opt.SupportNullableReferenceTypes)
				return;

			if (method.Parameters.Any (p => IsReferenceType (p.Symbol) && p.NotNull))
				writer.Add (CS8604, GenericMarshalArgumentReason);
		}

		// The marshal body invokes the member through the interface it is declared on. When
		// this method is an explicit implementation, that resolves to the inherited
		// declaration, whose parameters may be annotated differently.
		public static void AddMarshalArgumentSuppressions (ISuppressWarnings writer, GenBase type, Method method, CodeGenerationOptions opt)
		{
			if (!opt.SupportNullableReferenceTypes || type == null)
				return;

			// The call only binds to the base declaration when the member is emitted as an
			// explicit interface implementation; an implicit implementation is reached through
			// the deriving type's own, more permissive, declaration.
			var is_explicit = method.ExplicitInterface.HasValue () ||
				(opt.SupportDefaultInterfaceMethods && type is InterfaceGen && method.OverriddenInterfaceMethod != null);

			if (!is_explicit)
				return;

			foreach (var (base_method, via_interface) in GetOverriddenMembers (type, method)) {
				if (!via_interface)
					continue;

				for (var i = 0; i < method.Parameters.Count && i < base_method.Parameters.Count; i++) {
					var p = method.Parameters [i];

					if (IsReferenceType (p.Symbol) && !p.NotNull && base_method.Parameters [i].NotNull) {
						writer.Add (CS8604, MarshalArgumentReason);
						return;
					}
				}
			}
		}

		// Return types are covariant, so returning non-null where the base member allows null
		// is safe and is not reported. Only the reverse -- widening the base member's
		// guarantee -- is a warning.
		static bool ReturnNullabilityDiffers (Method method, Method baseMethod) =>
			IsReferenceType (method.RetVal.Symbol) && !method.RetVal.NotNull && baseMethod.RetVal.NotNull;

		// Parameters are contravariant, so accepting null where the base member requires
		// non-null is safe and is not reported. Only the reverse -- refusing a null the base
		// member's callers are allowed to pass -- is a warning.
		static bool ParameterNullabilityDiffers (Method method, Method baseMethod)
		{
			if (method.Parameters.Count != baseMethod.Parameters.Count)
				return false;

			for (var i = 0; i < method.Parameters.Count; i++) {
				var p = method.Parameters [i];

				if (IsReferenceType (p.Symbol) && p.NotNull && !baseMethod.Parameters [i].NotNull)
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
				// A Java getter/setter pair is projected as a property, so the interface's
				// accessors are not in `Methods` and have to be looked at separately.
				var im = InterfaceMembers (iface).FirstOrDefault (m => m != method && IsSameJavaMethod (m, method));

				if (im != null)
					yield return (im, true);
			}
		}

		static IEnumerable<Method> InterfaceMembers (InterfaceGen iface)
		{
			foreach (var m in iface.Methods)
				yield return m;

			foreach (var p in iface.Properties) {
				if (p.Getter != null)
					yield return p.Getter;

				if (p.Setter != null)
					yield return p.Setter;
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

		static bool SignatureUsesObsoleteType (MethodBase method, CodeGenerationOptions opt)
		{
			if (method is Method m && IsBoundAsObsolete (m.RetVal.Symbol, opt))
				return true;

			return method.Parameters.Any (p => IsBoundAsObsolete (p.Symbol, opt));
		}

		public static bool IsBoundAsObsolete (ISymbol symbol, CodeGenerationOptions opt) =>
			ReferencedTypes (symbol).Any (gen => IsBoundAsObsolete (gen.DeprecatedComment, gen.DeprecatedSince, opt));

		static bool IsBoundAsObsolete (string deprecated, AndroidSdkVersion? deprecatedSince, CodeGenerationOptions opt) =>
			SourceWriterExtensions.EmitsObsoleteAttribute (deprecated, opt, deprecatedSince);

		// Yields every bound type a type reference mentions. A single reference can name more
		// than one type -- `java.util.List<org.apache.http.cookie.Cookie>` binds to
		// `IList<ICookie>`, and it is the type argument that carries the deprecation -- so the
		// arrays and generic instantiations that can sit in between have to be walked through
		// rather than simply unwrapped.
		static IEnumerable<GenBase> ReferencedTypes (ISymbol symbol)
		{
			switch (symbol) {
			case null:
				yield break;
			case GenBase gen:
				yield return gen;
				break;
			case GenericSymbol generic:
				if (generic.Gen != null)
					yield return generic.Gen;

				foreach (var type_param in generic.TypeParams ?? Array.Empty<ISymbol> ())
					foreach (var referenced in ReferencedTypes (type_param))
						yield return referenced;
				break;
			case CollectionSymbol collection:
				foreach (var type_param in collection.TypeParams ?? Array.Empty<ISymbol> ())
					foreach (var referenced in ReferencedTypes (type_param))
						yield return referenced;
				break;
			case ArraySymbol array:
				foreach (var referenced in ReferencedTypes (array.ElementSymbol))
					yield return referenced;
				break;
			}
		}
	}
}
