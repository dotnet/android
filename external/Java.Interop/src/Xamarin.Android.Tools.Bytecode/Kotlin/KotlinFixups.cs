using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools.Bytecode
{
	public static class KotlinFixups
	{
		public static void Fixup (IList<ClassFile> classes)
		{
			// Pre-pass: identify Kotlin `@JvmInline value class` types and record
			// each one's JNI name -> backing primitive descriptor. We need this
			// map before processing methods so that a method on class A that takes
			// an inline class B as a parameter (via Kotlin metadata) can be stamped
			// even if B is later in `classes`. See dotnet/java-interop#1431.
			var inlineClasses = DetectInlineClasses (classes);

			foreach (var c in classes) {
				// See if this is a Kotlin class
				var attr = c.Attributes.OfType<RuntimeVisibleAnnotationsAttribute> ().FirstOrDefault ();
				var kotlin = attr?.Annotations.SingleOrDefault (a => a.Type == "Lkotlin/Metadata;");

				if (kotlin is null)
					continue;

				try {
					var km = KotlinMetadata.FromAnnotation (kotlin);
					var metadata = km.ParseMetadata ();

					if (metadata is null)
						continue;

					// Do fixups only valid for full classes
					var class_metadata = (metadata as KotlinClass);

					if (class_metadata != null) {
						FixupClassVisibility (c, class_metadata);

						if (!c.AccessFlags.IsPubliclyVisible ())
							continue;
						if (class_metadata.Constructors == null)
							continue;

						foreach (var con in class_metadata.Constructors)
							FixupConstructor (FindJavaConstructor (class_metadata, con, c), con);
					}

					// Do fixups valid for both classes and modules
					// (We pass "class_metadata" even though it's sometimes null because it's
					// used for generic type resolution if available for class types)
					FixupJavaMethods (c.Methods);

					if (metadata.Functions != null) {
						// We work from Java methods to Kotlin metadata because they aren't a 1:1 relation
						// and we need to find the "best" match for each Java method.
						foreach (var java_method in c.Methods)
							if (FindKotlinFunctionMetadata (metadata, java_method) is KotlinFunction function_metadata)
								FixupFunction (java_method, function_metadata, class_metadata, inlineClasses);
					}

					if (metadata.Properties != null) {
						foreach (var prop in metadata.Properties) {
							var getter = FindJavaPropertyGetter (metadata, prop, c, inlineClasses);
							var setter = FindJavaPropertySetter (metadata, prop, c, inlineClasses);

							FixupProperty (getter, setter, prop, inlineClasses);

							FixupField (FindJavaFieldProperty (metadata, prop, c), prop);
						}
					}

				} catch (Exception ex) {
					Log.Warning (0, $"class-parse: warning: Unable to parse Kotlin metadata on '{c.ThisClass.Name}': {ex}");
				}
			}
		}

		// Identifies Kotlin `@JvmInline value class` types in `classes` and stamps
		// each `ClassFile.KotlinInlineClassUnderlyingJniType` with the JNI descriptor
		// of its single backing field. Returns a map from the class's *Kotlin metadata*
		// class-name representation (e.g. `com/example/MyColor;`) to that descriptor,
		// for use when projecting `KotlinType.ClassName` references on parameters and
		// return types of OTHER methods. See dotnet/java-interop#1431 (Phase 2).
		static Dictionary<string, string> DetectInlineClasses (IList<ClassFile> classes)
		{
			var map = new Dictionary<string, string> (StringComparer.Ordinal);
			foreach (var c in classes) {
				var ann = c.Attributes.OfType<RuntimeVisibleAnnotationsAttribute> ().FirstOrDefault ();
				if (ann is null)
					continue;

				// `@JvmInline` is the JVM-level marker for Kotlin inline/value classes.
				if (!ann.Annotations.Any (a => a.Type == "Lkotlin/jvm/JvmInline;"))
					continue;

				// Sanity-check via Kotlin metadata: must be `kind == 1` (Class) and
				// have IsInlineClass set. This filters out `@JvmInline` on things
				// kotlinc may have emitted in the future for non-class kinds.
				var meta = ann.Annotations.SingleOrDefault (a => a.Type == "Lkotlin/Metadata;");
				if (meta is null)
					continue;

				try {
					var km = KotlinMetadata.FromAnnotation (meta);
					if (km.AsClassMetadata () is not KotlinClass kc)
						continue;
					if ((kc.Flags & KotlinClassFlags.IsInlineClass) == 0)
						continue;

					// The single non-synthetic, non-static instance field is the
					// inline-class backing value. (Synthetic fields like `Companion`
					// are filtered out.) We additionally require:
					//   - exactly one such field exists (Kotlin inline classes have
					//     a single property; multiple non-synthetic instance fields
					//     means something else is going on and we shouldn't trust
					//     this as the backing field).
					//   - the field is a JVM *primitive* descriptor — the wrapper
					//     struct currently emits the underlying as a primitive
					//     C# type, so reference-backed inline classes (e.g.
					//     `value class Tag(val s: String)`) would produce wrong
					//     bindings. Skip these for now; they fall back to the
					//     standard peer-class binding path.
					var instance_fields = c.Fields.Where (f =>
						!f.AccessFlags.HasFlag (FieldAccessFlags.Synthetic) &&
						!f.AccessFlags.HasFlag (FieldAccessFlags.Static)).ToList ();
					if (instance_fields.Count != 1)
						continue;
					var backing = instance_fields [0];
					if (!IsJvmPrimitiveDescriptor (backing.Descriptor))
						continue;

					c.KotlinInlineClassUnderlyingJniType = backing.Descriptor;

					// Kotlin's `KotlinType.ClassName` strings are stored without the
					// leading `L` but with a trailing `;` (e.g. `com/example/MyColor;`).
					// We index by that form so callers can look up directly from
					// `kotlin_p.Type.ClassName` without string surgery.
					var jvmName = c.ThisClass.Name.Value + ";";
					map [jvmName] = backing.Descriptor;
				} catch (Exception ex) {
					Log.Warning (0, $"class-parse: warning: Unable to detect inline class on '{c.ThisClass.Name}': {ex}");
				}
			}
			return map;
		}

		// JNI signature for the Kotlin inline class referenced by `kotlinTypeClassName`,
		// or null when projection should not apply. The returned form has a
		// leading `L` and trailing `;` so it matches `ClassFile.FullJniName`
		// and other JNI-signature strings used throughout the pipeline.
		//
		// `jvmDescriptor` is the *JVM-erased* descriptor of the actual position
		// (parameter / return / property) we're considering. We only project
		// when it equals the inline class's underlying primitive: that's the
		// case where Kotlin truly erased to the primitive and our wrapper
		// struct's `implicit operator <primitive>` makes JNI marshaling work
		// transparently. Boxed / nullable / generic positions keep their JVM
		// reference signature (`L...MyColor;` or `Ljava/lang/Object;`); for
		// those, projecting to a struct would mismatch JNI marshaling, so we
		// fall through and let them keep the legacy peer-class binding path.
		static string? GetInlineClassJniType (string? kotlinTypeClassName, string? jvmDescriptor, IDictionary<string, string> inlineClasses)
		{
			if (kotlinTypeClassName is null || jvmDescriptor is null)
				return null;
			if (!inlineClasses.TryGetValue (kotlinTypeClassName, out var underlying))
				return null;
			if (jvmDescriptor != underlying)
				return null;
			return "L" + kotlinTypeClassName;
		}

		// Returns true for JVM primitive descriptors (Z/B/C/D/F/I/J/S). Excludes
		// `V` (void), reference (`L...;`), and array (`[...`) descriptors.
		static bool IsJvmPrimitiveDescriptor (string? descriptor)
		{
			if (descriptor is null || descriptor.Length != 1)
				return false;
			return descriptor [0] switch {
				'Z' or 'B' or 'C' or 'D' or 'F' or 'I' or 'J' or 'S' => true,
				_ => false,
			};
		}

		static void FixupClassVisibility (ClassFile klass, KotlinClass metadata)
		{
			// Hide class if it isn't Public/Protected
			if (klass.AccessFlags.IsPubliclyVisible () && !metadata.Visibility.IsPubliclyVisible ()) {

				// Interfaces should be set to "package-private"
				if (klass.AccessFlags.HasFlag (ClassAccessFlags.Interface)) {
					Log.Debug ($"Kotlin: Setting internal interface {klass.ThisClass.Name.Value} to package-private");
					klass.AccessFlags = SetVisibility (klass.AccessFlags, null);

					foreach (var ic in klass.InnerClasses) {
						Log.Debug ($"Kotlin: Setting nested type {ic.InnerClass.Name.Value} in an internal interface to package-private");
						ic.InnerClassAccessFlags = SetVisibility (ic.InnerClassAccessFlags, null);
					}

					return;
				}

				Log.Debug ($"Kotlin: Hiding internal class {klass.ThisClass.Name.Value}");
				klass.AccessFlags = SetVisibility (klass.AccessFlags, ClassAccessFlags.Private);

				foreach (var ic in klass.InnerClasses) {
					Log.Debug ($"Kotlin: Hiding nested internal type {ic.InnerClass.Name.Value}");
					ic.InnerClassAccessFlags = SetVisibility (ic.InnerClassAccessFlags, ClassAccessFlags.Private);
				}

				return;
			}
		}

		// Passing null for 'newVisibility' parameter means 'package-private'
		internal static ClassAccessFlags SetVisibility (ClassAccessFlags existing, ClassAccessFlags? newVisibility)
		{
			// First we need to remove any existing visibility flags,
			// without modifying other flags like Abstract
			existing = (existing ^ ClassAccessFlags.Public) & existing;
			existing = (existing ^ ClassAccessFlags.Protected) & existing;
			existing = (existing ^ ClassAccessFlags.Private) & existing;

			// Package-private is stored as "no visibility flags", so only add flag if specified
			if (newVisibility.HasValue)
				existing |= newVisibility.Value;

			return existing;
		}

		static MethodAccessFlags SetVisibility (MethodAccessFlags existing, MethodAccessFlags newVisibility)
		{
			// First we need to remove any existing visibility flags,
			// without modifying other flags like Abstract
			existing = (existing ^ MethodAccessFlags.Public) & existing;
			existing = (existing ^ MethodAccessFlags.Protected) & existing;
			existing = (existing ^ MethodAccessFlags.Private) & existing;
			existing = (existing ^ MethodAccessFlags.Internal) & existing;

			existing |= newVisibility;

			return existing;
		}

		static FieldAccessFlags SetVisibility (FieldAccessFlags existing, FieldAccessFlags newVisibility)
		{
			// First we need to remove any existing visibility flags,
			// without modifying other flags like Abstract
			existing = (existing ^ FieldAccessFlags.Public) & existing;
			existing = (existing ^ FieldAccessFlags.Protected) & existing;
			existing = (existing ^ FieldAccessFlags.Private) & existing;
			existing = (existing ^ FieldAccessFlags.Internal) & existing;

			existing |= newVisibility;

			return existing;
		}

		static void FixupJavaMethods (Methods methods)
		{
			// We do the following method level fixups here because we can operate on all methods,
			// not just ones that have corresponding Kotlin metadata, like FixupFunction does.

			// Hide Kotlin generated methods like "add-impl" that aren't intended for end users
			foreach (var method in methods.Where (m => m.IsPubliclyVisible && m.Name.IndexOf ("-impl", StringComparison.Ordinal) >= 0)) {
				Log.Debug ($"Kotlin: Hiding implementation method {method.DeclaringType?.ThisClass.Name.Value} - {method.Name}");
				method.AccessFlags = MethodAccessFlags.Private;
			}

			// Hide constructor if it's the synthetic DefaultConstructorMarker one
			foreach (var method in methods.Where (method => method.IsDefaultConstructorMarker ())) {
				Log.Debug ($"Kotlin: Hiding synthetic default constructor in class '{method.DeclaringType?.ThisClass.Name.Value}' with signature '{method.Descriptor}'");
				method.AccessFlags = ((method.AccessFlags ^ MethodAccessFlags.Public) & method.AccessFlags) | MethodAccessFlags.Private;
			}

			// Better parameter names in extension methods
			foreach (var method in methods.Where (m => m.IsPubliclyVisible && m.AccessFlags.HasFlag (MethodAccessFlags.Static)))
				FixupExtensionMethod (method);
		}

		static void FixupConstructor (MethodInfo? method, KotlinConstructor metadata)
		{
			if (method is null)
				return;

			// Hide constructor if it isn't Public/Protected
			if (method.IsPubliclyVisible && !metadata.Flags.IsPubliclyVisible ()) {
				Log.Debug ($"Kotlin: Hiding internal constructor {method.DeclaringType?.ThisClass.Name.Value} - {metadata.GetSignature ()}");
				method.AccessFlags = SetVisibility (method.AccessFlags, MethodAccessFlags.Internal);
			}
		}

		static void FixupFunction (MethodInfo? method, KotlinFunction metadata, KotlinClass? kotlinClass, IDictionary<string, string> inlineClasses)
		{
			if (method is null || !method.IsPubliclyVisible)
				return;

			// Hide function if it isn't Public/Protected
			if (!metadata.Flags.IsPubliclyVisible ()) {
				Log.Debug ($"Kotlin: Hiding internal method {method.DeclaringType?.ThisClass.Name.Value} - {metadata.GetSignature ()}");
				method.AccessFlags = SetVisibility (method.AccessFlags, MethodAccessFlags.Internal);
				return;
			}

			(var start, var end) = CreateParameterMap (method, metadata, kotlinClass);

			var java_parameters = method.GetParameters ();

			for (var i = 0; i < end - start; i++) {
				var java_p = java_parameters [start + i];
				var kotlin_p = metadata.ValueParameters == null ? null : metadata.ValueParameters [i];
				if (kotlin_p == null || kotlin_p.Type == null || kotlin_p.Name == null)
					continue;

				// Kotlin provides actual parameter names
				if (TypesMatch (java_p.Type, kotlin_p.Type, kotlinClass) && java_p.IsUnnamedParameter () && !kotlin_p.IsUnnamedParameter ()) {
					Log.Debug ($"Kotlin: Renaming parameter {method.DeclaringType?.ThisClass.Name.Value} - {method.Name} - {java_p.Name} -> {kotlin_p.Name}");
					java_p.Name = kotlin_p.Name;
				}

				// Handle erasure of Kotlin unsigned types
				java_p.KotlinType = GetKotlinType (java_p.Type.TypeSignature, kotlin_p.Type.ClassName);

				// Inline-class projection: if the Kotlin source-level type for this
				// parameter is a `@JvmInline value class` we know about AND the
				// JVM-erased parameter descriptor is the inline class's
				// underlying primitive, record its JNI signature so the
				// generator can later swap the parameter type for a strongly-
				// typed wrapper struct while keeping JNI marshaling on the
				// underlying primitive. Boxed positions are skipped.
				// See dotnet/java-interop#1431 (Phase 2).
				java_p.KotlinInlineClassJniType = GetInlineClassJniType (kotlin_p.Type.ClassName, java_p.Type.TypeSignature, inlineClasses);
			}

			// Handle erasure of Kotlin unsigned types
			method.KotlinReturnType = GetKotlinType (method.ReturnType.TypeSignature, metadata.ReturnType?.ClassName);

			// Same projection as above, but for the return type.
			method.KotlinInlineClassReturnJniType = GetInlineClassJniType (metadata.ReturnType?.ClassName, method.ReturnType.TypeSignature, inlineClasses);

			// Recover the unmangled Kotlin source-level name when the Kotlin
			// compiler mangled the JVM name for inline-class binary compat
			// (e.g. JVM name `tint-Rn_QMJI`, Kotlin name `tint`). The generator
			// will emit this as the C# binding name (PascalCased to match
			// `managedName` conventions); the JVM name stays the JNI invocation
			// target. See dotnet/java-interop#1431 (Phase 2).
			if (metadata.Name != null && metadata.JvmName != null && metadata.Name != metadata.JvmName)
				method.KotlinName = PascalCase (metadata.Name);
		}

		static string PascalCase (string name)
		{
			if (string.IsNullOrEmpty (name) || char.IsUpper (name [0]))
				return name;
			return char.ToUpperInvariant (name [0]) + name.Substring (1);
		}

		public static (int start, int end) CreateParameterMap (MethodInfo method, KotlinFunction function, KotlinClass? kotlinClass)
		{
			var parameters = method.GetParameters ();
			var start = 0;
			var end = parameters.Length;

			// If the parameter counts are the same, that's good enough (because we know signatures matched)
			if (IsValidParameterMap (method, function, start, end))
				return (start, end);

			// Remove the "hidden" receiver type parameter from the start of the parameter list
			if (function.ReceiverType != null)
				start++;

			if (IsValidParameterMap (method, function, start, end))
				return (start, end);

			var last_p = parameters.Last ();

			// Remove the "hidden" coroutine continuation type parameter from the end of the parameter list
			// We try to restrict it to compiler generated paramteres because a user might have actually used it as a parameter
			if (last_p.Type.BinaryName == "Lkotlin/coroutines/Continuation;" && (last_p.IsUnnamedParameter () || last_p.IsCompilerNamed ()))
				end--;

			if (IsValidParameterMap (method, function, start, end))
				return (start, end);

			// Remove the "hidden" "this" type parameter for a static method from the start of the parameter list
			// Note we do this last because sometimes it isn't there.
			if (method.AccessFlags.HasFlag (MethodAccessFlags.Static))
				start++;

			if (IsValidParameterMap (method, function, start, end))
				return (start, end);

			return (0, 0);
		}

		static bool IsValidParameterMap (MethodInfo method, KotlinFunction function, int start, int end) => function.ValueParameters?.Count == end - start;

		static void FixupExtensionMethod (MethodInfo method)
		{
			// Kotlin "extension" methods give the first parameter an ugly name
			// like "$this$toByteString", we change it to "obj" to be a bit nicer.
			var param = method.GetParameters ();

			if (param.Length > 0 && param [0].Name.StartsWith ("$this$", StringComparison.Ordinal)) {
				Log.Debug ($"Kotlin: Renaming extension parameter {method.DeclaringType?.ThisClass.Name.Value} - {method.Name} - {param [0].Name} -> obj");
				param [0].Name = "obj";
			}
		}

		static void FixupProperty (MethodInfo? getter, MethodInfo? setter, KotlinProperty metadata, IDictionary<string, string> inlineClasses)
		{
			if (getter is null && setter is null)
				return;

			// Hide getters/setters if property is Internal
			if (metadata.IsInternalVisibility) {

				if (getter?.IsPubliclyVisible == true) {
					Log.Debug ($"Kotlin: Hiding internal getter method {getter.DeclaringType?.ThisClass.Name.Value} - {getter.Name}");
					getter.AccessFlags = SetVisibility (getter.AccessFlags, MethodAccessFlags.Internal);
				}

				if (setter?.IsPubliclyVisible == true) {
					Log.Debug ($"Kotlin: Hiding internal setter method {setter.DeclaringType?.ThisClass.Name.Value} - {setter.Name}");
					setter.AccessFlags = SetVisibility (setter.AccessFlags, MethodAccessFlags.Internal);
				}

				return;
			}

			// Handle erasure of Kotlin unsigned types
			if (getter != null) {
				getter.KotlinReturnType = GetKotlinType (getter.ReturnType.TypeSignature, metadata.ReturnType?.ClassName);
				getter.KotlinInlineClassReturnJniType = GetInlineClassJniType (metadata.ReturnType?.ClassName, getter.ReturnType.TypeSignature, inlineClasses);
			}

			if (setter != null) {
				var setter_parameter = setter.GetParameters ().First ();

				if (setter_parameter.IsUnnamedParameter () || setter_parameter.Name == "<set-?>") {
					Log.Debug ($"Kotlin: Renaming setter parameter {setter.DeclaringType?.ThisClass.Name.Value} - {setter.Name} - {setter_parameter.Name} -> value");
					setter_parameter.Name = "value";
				}

				// Handle erasure of Kotlin unsigned types
				setter_parameter.KotlinType = GetKotlinType (setter_parameter.Type.TypeSignature, metadata.ReturnType?.ClassName);
				setter_parameter.KotlinInlineClassJniType = GetInlineClassJniType (metadata.ReturnType?.ClassName, setter_parameter.Type.TypeSignature, inlineClasses);
			}
		}

		static void FixupField (FieldInfo? field, KotlinProperty metadata)
		{
			if (field is null)
				return;

			// Hide field if it isn't Public/Protected
			if (!metadata.Flags.IsPubliclyVisible ()) {

				if (field.IsPubliclyVisible) {
					Log.Debug ($"Kotlin: Hiding internal field {field.DeclaringType?.ThisClass.Name.Value} - {field.Name}");
					field.AccessFlags = SetVisibility (field.AccessFlags, FieldAccessFlags.Internal);
				}
			}

			// Handle erasure of Kotlin unsigned types
			field.KotlinType = GetKotlinType (field.Descriptor, metadata.ReturnType?.ClassName);
		}

		static MethodInfo? FindJavaConstructor (KotlinClass kotlinClass, KotlinConstructor constructor, ClassFile klass)
		{
			var all_constructors = klass.Methods.Where (method => method.Name == "<init>" || method.Name == "<clinit>");
			var possible_constructors = all_constructors.Where (method => method.GetFilteredParameters ().Length == constructor.ValueParameters?.Count);

			foreach (var method in possible_constructors) {
				if (ParametersMatch (kotlinClass, method, constructor.ValueParameters!))
					return method;
			}

			return null;
		}

		static KotlinFunction? FindKotlinFunctionMetadata (KotlinFile? kotlinFile, MethodInfo javaMethod)
		{
			if (kotlinFile?.Functions is null)
				return null;

			var java_descriptor = javaMethod.Descriptor;

			// The method name absolutely has to match
			var possible_functions = kotlinFile.Functions.Where (f => f.JvmName == javaMethod.Name).ToArray ();

			// If we have metadata with a Descriptor/JvmSignature match, that means all parameters and return type match
			if (possible_functions.SingleOrDefault (f => f.JvmSignature != null && f.JvmSignature == java_descriptor) is KotlinFunction kf)
				return kf;

			// Sometimes JvmSignature is null (or unhelpful), so we're going to construct one ourselves and see if they match
			if (possible_functions.SingleOrDefault (f => f.ConstructJvmSignature () == java_descriptor) is KotlinFunction kf2)
				return kf2;

			// If that didn't work, let's try it the hard way!
			// This catches cases where Kotlin only wrote one metadata entry for multiple methods with the same mangled JvmName (ex: contains-WZ4Q5Ns)
			var java_param_count = javaMethod.GetFilteredParameters ().Length;

			foreach (var function in possible_functions.Where (f => f.ValueParameters?.Count == java_param_count)) {
				if (function.ReturnType == null)
					continue;
				if (!TypesMatch (javaMethod.ReturnType, function.ReturnType, kotlinFile))
					continue;

				if (!ParametersMatch (kotlinFile, javaMethod, function.ValueParameters!))
					continue;

				return function;
			}

			return null;
		}

		static FieldInfo? FindJavaFieldProperty (KotlinFile kotlinClass, KotlinProperty property, ClassFile klass)
		{
			var possible_methods = klass.Fields.Where (field => field.Name == property.Name &&
					property.ReturnType != null &&
					TypesMatch (new TypeInfo (field.Descriptor, field.Descriptor), property.ReturnType, kotlinClass));

			return possible_methods.FirstOrDefault ();
		}

		static MethodInfo? FindJavaPropertyGetter (KotlinFile kotlinClass, KotlinProperty property, ClassFile klass, IDictionary<string, string> inlineClasses)
		{
			// Private properties do not have getters
			if (property.IsPrivateVisibility)
				return null;

			// Public/protected getters look like "getFoo"
			// Public/protected getters with unsigned types look like "getFoo-abcdefg"
			// Internal getters look like "getFoo$main"
			// Internal getters with unsigned types look like "getFoo-WZ4Q5Ns$main"
			var possible_methods = property.IsInternalVisibility ?
				klass.Methods.Where (method => method.GetMethodNameWithoutUnsignedSuffix ().StartsWith ($"get{property.Name.Capitalize ()}$", StringComparison.Ordinal)) :
				klass.Methods.Where (method => method.GetMethodNameWithoutUnsignedSuffix ().Equals ($"get{property.Name.Capitalize ()}", StringComparison.Ordinal));

			possible_methods = possible_methods.Where (method =>
					method.GetParameters ().Length == 0 &&
					property.ReturnType != null &&
					TypesMatch (method.ReturnType, property.ReturnType, kotlinClass, inlineClasses));

			return possible_methods.FirstOrDefault ();
		}

		static MethodInfo? FindJavaPropertySetter (KotlinFile kotlinClass, KotlinProperty property, ClassFile klass, IDictionary<string, string> inlineClasses)
		{
			// Private properties do not have setters
			if (property.IsPrivateVisibility)
				return null;

			// Public/protected setters look like "setFoo"
			// Public/protected setters with unsigned types look like "setFoo-abcdefg"
			// Internal setters look like "setFoo$main"
			// Internal setters with unsigned types look like "setFoo-WZ4Q5Ns$main"
			var possible_methods = property.IsInternalVisibility ?
				klass.Methods.Where (method => method.GetMethodNameWithoutUnsignedSuffix ().StartsWith ($"set{property.Name.Capitalize ()}$", StringComparison.Ordinal)) :
				klass.Methods.Where (method => method.GetMethodNameWithoutUnsignedSuffix ().Equals ($"set{property.Name.Capitalize ()}", StringComparison.Ordinal));

			possible_methods = possible_methods.Where (method => 
						property.ReturnType != null &&
						method.GetParameters ().Length == 1 &&
						method.ReturnType.BinaryName == "V" &&
						TypesMatch (method.GetParameters () [0].Type, property.ReturnType, kotlinClass, inlineClasses));

			return possible_methods.FirstOrDefault ();
		}

		static bool ParametersMatch (KotlinFile kotlinClass, MethodInfo method, List<KotlinValueParameter> kotlinParameters)
		{
			var java_parameters = method.GetFilteredParameters ();

			if (java_parameters.Length == 0 && kotlinParameters.Count == 0)
				return true;

			for (var i = 0; i < java_parameters.Length; i++) {
				var java_p = java_parameters [i];
				var kotlin_p = kotlinParameters [i];

				if (kotlin_p.Type == null || !TypesMatch (java_p.Type, kotlin_p.Type, kotlinClass))
					return false;
			}

			return true;
		}

		static bool TypesMatch (TypeInfo javaType, KotlinType kotlinType, KotlinFile? kotlinFile, IDictionary<string, string>? inlineClasses = null)
		{
			// Generic type
			if (!string.IsNullOrWhiteSpace (kotlinType.TypeParameterName) && $"T{kotlinType.TypeParameterName};" == javaType.TypeSignature)
				return true;

			if (javaType.BinaryName == KotlinUtilities.ConvertKotlinTypeSignature (kotlinType, kotlinFile))
				return true;

			// Could be a generic type erasure
			if (javaType.BinaryName == "Ljava/lang/Object;")
				return true;

			// dotnet/java-interop#1431 (Phase 2): the JVM erases @JvmInline value
			// class types to their underlying primitive descriptor, so e.g. a
			// `MyColor` property (ULong-backed) appears in the bytecode as `()J`
			// even though the Kotlin metadata still says `MyColor`. Accept the
			// match when the JVM primitive matches the inline class's recorded
			// underlying-primitive descriptor.
			if (inlineClasses != null && IsJvmPrimitiveDescriptor (javaType.BinaryName) &&
					kotlinType.ClassName != null &&
					inlineClasses.TryGetValue (kotlinType.ClassName, out var underlying) &&
					underlying == javaType.BinaryName)
				return true;

			// Sometimes Kotlin keeps its native types rather than converting them to Java native types
			// ie: "Lkotlin/UShort;" instead of "S"
			if (javaType.BinaryName.StartsWith ("L", StringComparison.Ordinal) && javaType.BinaryName.EndsWith (";", StringComparison.Ordinal)) {
				if (KotlinUtilities.ConvertKotlinClassToJava (javaType.BinaryName.Substring (1, javaType.BinaryName.Length - 2)) == KotlinUtilities.ConvertKotlinTypeSignature (kotlinType, kotlinFile))
					return true;
			}

			// Same for some arrays
			if (javaType.BinaryName.StartsWith ("[L", StringComparison.Ordinal) && javaType.BinaryName.EndsWith (";", StringComparison.Ordinal)) {
				if ("[" + KotlinUtilities.ConvertKotlinClassToJava (javaType.BinaryName.Substring (2, javaType.BinaryName.Length - 3)) == KotlinUtilities.ConvertKotlinTypeSignature (kotlinType, kotlinFile))
					return true;
			}

			return false;
		}

		static string? GetKotlinType (string? jvmType, string? kotlinClass)
		{
			// Handle erasure of Kotlin unsigned types
			if (jvmType == "I" && kotlinClass == "kotlin/UInt;")
				return "uint";
			if (jvmType == "[I" && kotlinClass == "kotlin/UIntArray;")
				return "uint[]";
			if (jvmType == "S" && kotlinClass == "kotlin/UShort;")
				return "ushort";
			if (jvmType == "[S" && kotlinClass == "kotlin/UShortArray;")
				return "ushort[]";
			if (jvmType == "J" && kotlinClass == "kotlin/ULong;")
				return "ulong";
			if (jvmType == "[J" && kotlinClass == "kotlin/ULongArray;")
				return "ulong[]";
			if (jvmType == "B" && kotlinClass == "kotlin/UByte;")
				return "ubyte";
			if (jvmType == "[B" && kotlinClass == "kotlin/UByteArray;")
				return "ubyte[]";

			return null;
		}
	}
}
