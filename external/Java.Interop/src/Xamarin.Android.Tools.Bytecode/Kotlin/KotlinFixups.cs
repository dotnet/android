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
						foreach (var met in metadata.Functions)
							FixupFunction (FindJavaMethod (metadata, met, c), met, class_metadata);
					}


					if (metadata.Properties != null) {
						foreach (var prop in metadata.Properties) {
							var getter = FindJavaPropertyGetter (metadata, prop, c);
							var setter = FindJavaPropertySetter (metadata, prop, c);

							FixupProperty (getter, setter, prop);

							FixupField (FindJavaFieldProperty (metadata, prop, c), prop);
						}
					}

				} catch (Exception ex) {
					Log.Warning (0, $"class-parse: warning: Unable to parse Kotlin metadata on '{c.ThisClass.Name}': {ex}");
				}
			}
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
		static ClassAccessFlags SetVisibility (ClassAccessFlags existing, ClassAccessFlags? newVisibility)
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

		static void FixupFunction (MethodInfo? method, KotlinFunction metadata, KotlinClass? kotlinClass)
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
			}

			// Handle erasure of Kotlin unsigned types
			method.KotlinReturnType = GetKotlinType (method.ReturnType.TypeSignature, metadata.ReturnType?.ClassName);
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

		static void FixupProperty (MethodInfo? getter, MethodInfo? setter, KotlinProperty metadata)
		{
			if (getter is null && setter is null)
				return;

			// Hide property if it isn't Public/Protected
			if (!metadata.Flags.IsPubliclyVisible ()) {

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
			if (getter != null)
				getter.KotlinReturnType = GetKotlinType (getter.ReturnType.TypeSignature, metadata.ReturnType?.ClassName);

			if (setter != null) {
				var setter_parameter = setter.GetParameters ().First ();

				if (setter_parameter.IsUnnamedParameter () || setter_parameter.Name == "<set-?>") {
					Log.Debug ($"Kotlin: Renaming setter parameter {setter.DeclaringType?.ThisClass.Name.Value} - {setter.Name} - {setter_parameter.Name} -> value");
					setter_parameter.Name = "value";
				}

				// Handle erasure of Kotlin unsigned types
				setter_parameter.KotlinType = GetKotlinType (setter_parameter.Type.TypeSignature, metadata.ReturnType?.ClassName);
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

		static MethodInfo? FindJavaMethod (KotlinFile kotlinFile, KotlinFunction function, ClassFile klass)
		{
			var possible_methods = klass.Methods.Where (method => method.Name == function.JvmName).ToArray ();
			var signature = function.JvmSignature;

			// If the Descriptor/Signature match, that means all parameters and return type match
			if (signature != null && possible_methods.SingleOrDefault (method => method.Descriptor == signature) is MethodInfo m)
					return m;

			// Sometimes JvmSignature is null (or unhelpful), so we're going to construct one ourselves and see if they match
			signature = function.ConstructJvmSignature ();

			if (possible_methods.SingleOrDefault (method => method.Descriptor == signature) is MethodInfo m2)
				return m2;

			// If that didn't work, let's do it the hard way!
			// I don't know if this catches anything additional, but it was the original code we shipped, so
			// we'll keep it just in case something in the wild requires it.
			foreach (var method in possible_methods.Where (method => method.GetFilteredParameters ().Length == function.ValueParameters?.Count)) {
				if (function.ReturnType == null)
					continue;
				if (!TypesMatch (method.ReturnType, function.ReturnType, kotlinFile))
					continue;

				if (!ParametersMatch (kotlinFile, method, function.ValueParameters!))
					continue;

				return method;
			}

			// Theoretically this should never be hit, but who knows. At worst, it just means
			// Kotlin niceties won't be applied to the method.
			Log.Debug ($"Kotlin: Could not find Java method to match '{function.Name} ({function.ConstructJvmSignature ()})'");

			return null;
		}

		static FieldInfo? FindJavaFieldProperty (KotlinFile kotlinClass, KotlinProperty property, ClassFile klass)
		{
			var possible_methods = klass.Fields.Where (field => field.Name == property.Name &&
					property.ReturnType != null &&
					TypesMatch (new TypeInfo (field.Descriptor, field.Descriptor), property.ReturnType, kotlinClass));

			return possible_methods.FirstOrDefault ();
		}

		static MethodInfo? FindJavaPropertyGetter (KotlinFile kotlinClass, KotlinProperty property, ClassFile klass)
		{
			var possible_methods = klass.Methods.Where (method => string.Compare (method.GetMethodNameWithoutSuffix (), $"get{property.Name}", StringComparison.OrdinalIgnoreCase) == 0 &&
					method.GetParameters ().Length == 0 &&
					property.ReturnType != null &&
					TypesMatch (method.ReturnType, property.ReturnType, kotlinClass));

			return possible_methods.FirstOrDefault ();
		}

		static MethodInfo? FindJavaPropertySetter (KotlinFile kotlinClass, KotlinProperty property, ClassFile klass)
		{
			var possible_methods = klass.Methods.Where (method => string.Compare (method.GetMethodNameWithoutSuffix (), $"set{property.Name}", StringComparison.OrdinalIgnoreCase) == 0 &&
									      property.ReturnType != null &&
									      method.GetParameters ().Length == 1 &&
									      method.ReturnType.BinaryName == "V" &&
									      TypesMatch (method.GetParameters () [0].Type, property.ReturnType, kotlinClass));

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

		static bool TypesMatch (TypeInfo javaType, KotlinType kotlinType, KotlinFile? kotlinFile)
		{
			// Generic type
			if (!string.IsNullOrWhiteSpace (kotlinType.TypeParameterName) && $"T{kotlinType.TypeParameterName};" == javaType.TypeSignature)
				return true;

			if (javaType.BinaryName == KotlinUtilities.ConvertKotlinTypeSignature (kotlinType, kotlinFile))
				return true;

			// Could be a generic type erasure
			if (javaType.BinaryName == "Ljava/lang/Object;")
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
