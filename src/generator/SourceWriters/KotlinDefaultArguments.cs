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

		public static void AddMethods (TypeWriter writer, GenBase type, IEnumerable<Method> methods, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			var signatures = new HashSet<string> (type.Methods.Where (m => m.IsValid).Select (m => GetSignature (m.AdjustedName, m.Parameters, m.Parameters.Count)));

			foreach (var method in methods) {
				if (!method.IsValid || method.KotlinDefaultJniSignature is null || method.KotlinDefaultOwner is null)
					continue;
				if (type is ClassGen && method.KotlinDefaultOwner != type.RawJniName)
					continue;

				var trailing = GetTrailingDefaultCount (method);
				if (trailing == 0)
					continue;

				var raw = CreateHelperMethod (type, method, opt, context);
				if (raw is null)
					continue;

				var members = "_members";
				if (type is InterfaceGen) {
					members = "_members_kotlin_default_" + method.KotlinDefaultOwner.Replace ('/', '_').Replace ('$', '_');
					if (!writer.Fields.Any (f => f.Name == members)) {
						var field = new PeerMembersField (opt, method.KotlinDefaultOwner, type.Name, method.KotlinDefaultOwner == type.RawJniName, members) {
							IsPrivate = true,
						};
						writer.Fields.Add (field);
					}
				}

				for (var count = method.Parameters.Count - 1; count >= method.Parameters.Count - trailing; count--) {
					if (!signatures.Add (GetSignature (method.AdjustedName, method.Parameters, count)))
						continue;
					writer.Methods.Add (new KotlinDefaultMethod (type, method, raw, count, members, opt));
				}
			}
		}

		public static void AddConstructors (ClassWriter writer, ClassGen klass, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			// A derived managed type is created through its Java Callable Wrapper,
			// which does not have the synthetic `DefaultConstructorMarker` constructor.
			if (!klass.IsFinal)
				return;

			var signatures = new HashSet<string> (klass.Ctors.Where (c => c.IsValid).Select (c => GetSignature (klass.Name, c.Parameters, c.Parameters.Count)));

			foreach (var ctor in klass.Ctors) {
				if (!ctor.IsValid || ctor.IsNonStaticNestedType || ctor.KotlinDefaultJniSignature is null)
					continue;

				var trailing = GetTrailingDefaultCount (ctor);
				if (trailing == 0)
					continue;

				var raw = new Ctor (klass) {
					Name = ctor.Name,
					Visibility = ctor.Visibility,
					Deprecated = ctor.Deprecated,
					DeprecatedSince = ctor.DeprecatedSince,
					ApiAvailableSince = ctor.ApiAvailableSince,
					ApiRemovedSince = ctor.ApiRemovedSince,
				};
				foreach (var p in ctor.Parameters)
					raw.Parameters.Add (p.Clone ());
				AddMaskParameters (raw.Parameters, ctor.Parameters.Count);
				raw.Parameters.Add (new Parameter (Marker, "java.lang.Object", null, false) {
					JniTypeOverride = "Lkotlin/jvm/internal/DefaultConstructorMarker;",
				});

				if (!raw.Validate (opt, klass.TypeParameters, context) || raw.JniSignature != ctor.KotlinDefaultJniSignature)
					continue;

				for (var count = ctor.Parameters.Count - 1; count >= ctor.Parameters.Count - trailing; count--) {
					if (!signatures.Add (GetSignature (klass.Name, ctor.Parameters, count)))
						continue;
					writer.Constructors.Add (new KotlinDefaultConstructor (klass, ctor, raw, count, opt, context));
				}
			}
		}

		// The number of parameters at the end of the list that declare a default value.
		static int GetTrailingDefaultCount (MethodBase method)
		{
			var count = 0;
			for (var i = method.Parameters.Count - 1; i >= 0 && method.KotlinDefaultParameters.Contains (i); i--)
				count++;
			return count;
		}

		static string GetSignature (string name, ParameterList parameters, int count)
			=> name + "(" + string.Join (",", parameters.Take (count).Select (p => p.JniType)) + ")";

		static void AddMaskParameters (ParameterList parameters, int count)
		{
			for (var i = 0; i < (count + 31) / 32; i++)
				parameters.Add (new Parameter ("__mask" + i, "int", null, false));
		}

		// Creates the static `$default` helper:
		//   static R name$default (Owner __self, <parameters>, int __mask0, Object __marker)
		static Method CreateHelperMethod (GenBase type, Method method, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			var raw = method.Clone (type);
			raw.JavaName = method.JavaName + "$default";
			raw.IsStatic = true;
			raw.IsFinal = true;
			raw.IsVirtual = false;
			raw.IsAbstract = false;
			raw.IsCompatVirtualMethod = false;
			raw.IsInterfaceDefaultMethod = false;
			raw.JniSignatureOverride = method.KotlinDefaultJniSignature;

			if (!method.IsStatic)
				raw.Parameters.AddFirst (new Parameter (Receiver, type.JavaName, null, false));
			AddMaskParameters (raw.Parameters, method.Parameters.Count);
			raw.Parameters.Add (new Parameter (Marker, "java.lang.Object", null, false));

			return raw.Validate (opt, type.TypeParameters, context) ? raw : null;
		}

		// Declares locals for the omitted parameters and the extra `$default` arguments.
		internal static void AddLocals (List<string> body, MethodBase method, int count, bool hasReceiver, CodeGenerationOptions opt)
		{
			if (hasReceiver)
				body.Add ($"var {Receiver} = this;");

			for (var i = count; i < method.Parameters.Count; i++) {
				var p = method.Parameters [i];
				var type = opt.GetTypeReferenceName (p);
				if (type.StartsWith ("params ", StringComparison.Ordinal))
					type = type.Substring ("params ".Length);
				body.Add ($"var {opt.GetSafeIdentifier (p.Name)} = default ({type.TrimEnd ('?')});");
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
		public KotlinDefaultMethod (GenBase type, Method method, Method helper, int count, string members, CodeGenerationOptions opt)
		{
			Name = method.AdjustedName;
			IsStatic = method.IsStatic;
			SetVisibility (type is InterfaceGen ? string.Empty : method.Visibility);
			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));

			Comments.Add ($"// Kotlin default arguments: calls '{method.KotlinDefaultOwner}.{helper.JavaName}' to compute the omitted parameters.");

			SourceWriterExtensions.AddObsolete (Attributes, method.Deprecated, opt, deprecatedSince: method.DeprecatedSince);
			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			this.AddMethodParameters (KotlinDefaultArguments.Take (method.Parameters, count), opt);

			KotlinDefaultArguments.AddLocals (Body, method, count, !method.IsStatic, opt);
			SourceWriterExtensions.AddMethodBody (Body, helper, opt, members);
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
