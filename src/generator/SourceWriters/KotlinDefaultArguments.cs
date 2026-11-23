using System;
using System.Collections.Generic;
using System.Linq;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	// Kotlin default arguments: for each Kotlin method or constructor that has
	// trailing parameters with default values, generate C# overloads that omit
	// these parameters. The overloads call the `$default` helper (or the
	// `DefaultConstructorMarker` constructor) with a bit mask that tells Kotlin
	// which parameters to compute. For example:
	//
	//   fun greet (greeting: String, times: Int = 2)
	//
	// becomes the bound `Greet (string greeting, int times)` and also
	// `Greet (string greeting)`, which calls `greet$default (this, greeting, 0, 0b10, null)`.
	static class KotlinDefaultArguments
	{
		const string Receiver = "__self";
		const string Marker = "__marker";

		public static void AddMethods (TypeWriter writer, GenBase type, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			// Do not add an overload that already exists: string overloads, inherited methods,
			// interface methods, and the default-argument overloads of base types.
			var inherited = type.GetAllDerivedInterfaces ().SelectMany (i => i.Methods);
			for (var b = (type as ClassGen)?.BaseGen; b != null; b = b.BaseGen)
				inherited = inherited.Concat (b.Methods).Concat (b.GetAllDerivedInterfaces ().SelectMany (i => i.Methods));
			var members = type.Methods.Concat (inherited).Where (m => m.IsValid)
				.SelectMany (m => new [] { (m.AdjustedName, m.Parameters), (m.Name, m.Parameters) });
			var signatures = GetSignatures (members, opt);
			foreach (var m in inherited.Where (m => m.IsValid && IsSupported (m)))
				for (var count = m.Parameters.Count - GetTrailingDefaultCount (m); count < m.Parameters.Count; count++)
					signatures.Add (GetSignature (m.AdjustedName, m.Parameters, count, opt));

			foreach (var method in type.Methods) {
				if (!method.IsValid || !IsSupported (method) || method.IsReturnEnumified)
					continue;

				var trailing = GetTrailingDefaultCount (method);
				if (trailing == 0)
					continue;

				var helper = CreateHelperMethod (type, method, opt, context);
				if (helper is null)
					continue;

				for (var count = method.Parameters.Count - 1; count >= method.Parameters.Count - trailing; count--) {
					if (!signatures.Add (GetSignature (method.AdjustedName, method.Parameters, count, opt)))
						continue;
					// Interfaces only have `_members` when they have default or static methods.
					if (type is InterfaceGen && !writer.Fields.Any (f => f.Name == "_members"))
						writer.Fields.Add (new PeerMembersField (opt, type.RawJniName, type.Name, isInterface: true));
					writer.Methods.Add (new KotlinDefaultMethod (type, method, helper, count, opt));
				}
			}
		}

		public static void AddConstructors (ClassWriter writer, ClassGen klass, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			// A derived managed type is created through its Java Callable Wrapper,
			// which does not have the synthetic `DefaultConstructorMarker` constructor.
			if (!klass.IsFinal)
				return;

			var signatures = GetSignatures (klass.Ctors.Where (c => c.IsValid).Select (c => (klass.Name, c.Parameters)), opt);

			foreach (var ctor in klass.Ctors) {
				// C# does not allow protected constructors in sealed classes.
				if (!ctor.IsValid || ctor.IsNonStaticNestedType || ctor.Visibility == "protected" || !IsSupported (ctor))
					continue;

				var trailing = GetTrailingDefaultCount (ctor);
				if (trailing == 0)
					continue;

				var helper = new Ctor (klass) {
					Name = ctor.Name,
					Visibility = ctor.Visibility,
					AnnotatedVisibility = ctor.AnnotatedVisibility,
					Deprecated = ctor.Deprecated,
					DeprecatedSince = ctor.DeprecatedSince,
					ApiAvailableSince = ctor.ApiAvailableSince,
					ApiRemovedSince = ctor.ApiRemovedSince,
				};
				foreach (var p in ctor.Parameters)
					helper.Parameters.Add (p.Clone ());
				AddMaskParameters (helper.Parameters, ctor.Parameters.Count);
				helper.Parameters.Add (new Parameter (Marker, "java.lang.Object", null, false) {
					JniTypeOverride = "Lkotlin/jvm/internal/DefaultConstructorMarker;",
				});

				if (!helper.Validate (opt, klass.TypeParameters, context) || helper.JniSignature != ctor.KotlinDefaultJniSignature)
					continue;

				for (var count = ctor.Parameters.Count - 1; count >= ctor.Parameters.Count - trailing; count--) {
					if (!signatures.Add (GetSignature (klass.Name, ctor.Parameters, count, opt)))
						continue;
					writer.Constructors.Add (new KotlinDefaultConstructor (klass, ctor, helper, count, opt, context));
				}
			}
		}

		// The helper clones the parameters, which loses generated enum types.
		static bool IsSupported (MethodBase method)
			=> !string.IsNullOrEmpty (method.KotlinDefaultJniSignature) && !method.Parameters.Any (p => p.IsEnumified);

		static int GetTrailingDefaultCount (MethodBase method)
		{
			var count = 0;
			for (var i = method.Parameters.Count - 1; i >= 0 && method.KotlinDefaultParameters.Contains (i); i--)
				count++;
			return count;
		}

		static HashSet<string> GetSignatures (IEnumerable<(string Name, ParameterList Parameters)> members, CodeGenerationOptions opt)
		{
			var signatures = new HashSet<string> ();
			foreach (var (name, parameters) in members) {
				var signature = GetSignature (name, parameters, parameters.Count, opt);
				signatures.Add (signature);
				// The string overload of a Java.Lang.ICharSequence member.
				signatures.Add (signature.Replace ("Java.Lang.ICharSequence", "string"));
			}
			return signatures;
		}

		static string GetSignature (string name, ParameterList parameters, int count, CodeGenerationOptions opt)
			=> name + "(" + string.Join (",", parameters.Take (count).Select (p => GetTypeName (p, opt).Replace ("global::", ""))) + ")";

		// Keeps the `?` of a boxed Kotlin value class (`Nullable<T>`), but not nullable reference
		// annotations. `params` is not part of a C# signature.
		static string GetTypeName (Parameter p, CodeGenerationOptions opt)
		{
			var type = opt.GetOutputName (p.Type);
			return type.StartsWith ("params ", StringComparison.Ordinal) ? type.Substring ("params ".Length) : type;
		}

		static void AddMaskParameters (ParameterList parameters, int count)
		{
			for (var i = 0; i < (count + 31) / 32; i++)
				parameters.Add (new Parameter ("__mask" + i, "int", null, false));
		}

		// Creates the static `$default` helper:
		//   static R name$default (Owner __self, <parameters>, int __mask0, Object __marker)
		static Method CreateHelperMethod (GenBase type, Method method, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			var helper = method.Clone (type);
			helper.JavaName = method.JavaName + "$default";
			helper.IsStatic = true;
			helper.IsFinal = true;
			helper.IsVirtual = false;
			helper.IsAbstract = false;
			helper.IsCompatVirtualMethod = false;
			helper.IsInterfaceDefaultMethod = false;
			helper.JniSignatureOverride = method.KotlinDefaultJniSignature;

			if (!method.IsStatic)
				helper.Parameters.AddFirst (new Parameter (Receiver, type.JavaName, null, false));
			AddMaskParameters (helper.Parameters, method.Parameters.Count);
			helper.Parameters.Add (new Parameter (Marker, "java.lang.Object", null, false));

			return helper.Validate (opt, type.TypeParameters, context) ? helper : null;
		}

		// Declares locals for the omitted parameters and the extra `$default` arguments.
		internal static void AddLocals (List<string> body, MethodBase method, int count, bool hasReceiver, CodeGenerationOptions opt)
		{
			if (hasReceiver)
				body.Add ($"var {Receiver} = this;");

			for (var i = count; i < method.Parameters.Count; i++) {
				var p = method.Parameters [i];
				body.Add ($"var {opt.GetSafeIdentifier (p.Name)} = default ({GetTypeName (p, opt)});");
			}

			for (var mask = 0; mask < (method.Parameters.Count + 31) / 32; mask++) {
				var bits = 0;
				for (var i = Math.Max (count, mask * 32); i < Math.Min (method.Parameters.Count, (mask + 1) * 32); i++)
					bits |= 1 << (i % 32);
				body.Add ($"const int __mask{mask} = {bits};");
			}

			body.Add ($"var {Marker} = default (global::Java.Lang.Object);");
		}

		internal static ParameterList Take (ParameterList parameters, int count)
		{
			var result = new ParameterList ();
			foreach (var p in parameters.Take (count))
				result.Add (p);
			return result;
		}
	}

	public class KotlinDefaultMethod : MethodWriter
	{
		public KotlinDefaultMethod (GenBase type, Method method, Method helper, int count, CodeGenerationOptions opt)
		{
			Name = method.AdjustedName;
			IsStatic = method.IsStatic;
			SetVisibility (type is InterfaceGen ? string.Empty : method.Visibility);
			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));

			Comments.Add ($"// Kotlin default arguments: calls '{helper.JavaName}' to compute the omitted parameters.");

			SourceWriterExtensions.AddObsolete (Attributes, method.Deprecated, opt, deprecatedSince: method.DeprecatedSince);
			SourceWriterExtensions.AddRestrictToWarning (Attributes, method.AnnotatedVisibility, false, opt);
			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			var parameters = KotlinDefaultArguments.Take (method.Parameters, count);
			this.AddMethodParameters (parameters, opt);

			// For example, `toString (indent: Int = 0)` makes `ToString ()`, which hides `object.ToString ()`.
			var shape = new Method (type);
			foreach (var p in parameters)
				shape.Parameters.Add (p);
			IsShadow = type.RequiresNew (Name, shape);

			KotlinDefaultArguments.AddLocals (Body, method, count, !method.IsStatic, opt);
			SourceWriterExtensions.AddMethodBody (Body, helper, opt);
		}
	}

	public class KotlinDefaultConstructor : BoundConstructor
	{
		readonly Ctor original;
		readonly int count;

		public KotlinDefaultConstructor (ClassGen klass, Ctor original, Ctor helper, int count, CodeGenerationOptions opt, CodeGeneratorContext context)
			: base (klass, helper, klass.InheritsObject, opt, context)
		{
			this.original = original;
			this.count = count;

			Comments.Clear ();
			Comments.Add ("// Kotlin default arguments: calls the 'DefaultConstructorMarker' constructor to compute the omitted parameters.");

			// The [Register] signature is the full-argument constructor; do not register the synthetic one.
			Attributes.RemoveAll (a => a is RegisterAttr);

			Parameters.Clear ();
			this.AddMethodParameters (KotlinDefaultArguments.Take (original.Parameters, count), opt);
		}

		protected override void WriteBody (CodeWriter writer)
		{
			var locals = new List<string> ();
			KotlinDefaultArguments.AddLocals (locals, original, count, hasReceiver: false, opt);
			foreach (var line in locals)
				writer.WriteLine (line);
			base.WriteBody (writer);
		}
	}
}
