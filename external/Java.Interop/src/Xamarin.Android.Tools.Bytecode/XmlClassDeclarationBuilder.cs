using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using System.Text;

namespace Xamarin.Android.Tools.Bytecode {

	public class XmlClassDeclarationBuilder {

		ClassFile       classFile;
		ClassFile?      packageInfo;
		ClassSignature? signature;
		bool            isNullMarked;

		bool IsInterface {
			get {return (classFile.AccessFlags & ClassAccessFlags.Interface) != 0;}
		}

		public XmlClassDeclarationBuilder (ClassFile classFile)
			: this (classFile, packageInfo: null)
		{
		}

		public XmlClassDeclarationBuilder (ClassFile classFile, ClassFile? packageInfo)
		{
			if (classFile == null)
				throw new ArgumentNullException ("classFile");

			this.classFile      = classFile;
			this.packageInfo    = packageInfo;
			signature           = classFile.GetSignature ();
			isNullMarked        = ComputeIsNullMarked ();
		}

		// Module-level and inner-class scope inheritance are not yet implemented.
		bool ComputeIsNullMarked ()
		{
			var classScope = GetJSpecifyScope (classFile.Attributes);
			if (classScope.HasValue)
				return classScope.Value;

			if (packageInfo != null) {
				var pkgScope = GetJSpecifyScope (packageInfo.Attributes);
				if (pkgScope.HasValue)
					return pkgScope.Value;
			}

			return false;
		}

		// `@NullMarked` / `@NullUnmarked` are `@Retention(RUNTIME)`, so
		// check both Visible and Invisible annotation tables.
		static bool? GetJSpecifyScope (AttributeCollection? attributes)
		{
			if (attributes == null)
				return null;
			foreach (var a in EnumerateDeclarationAnnotations (attributes)) {
				if (a.Type == "Lorg/jspecify/annotations/NullUnmarked;")
					return false;
				if (a.Type == "Lorg/jspecify/annotations/NullMarked;")
					return true;
			}
			return null;
		}

		static IEnumerable<Annotation> EnumerateDeclarationAnnotations (AttributeCollection attributes)
		{
			foreach (var v in attributes.OfType<RuntimeVisibleAnnotationsAttribute> ())
				foreach (var a in v.Annotations)
					yield return a;
			foreach (var i in attributes.OfType<RuntimeInvisibleAnnotationsAttribute> ())
				foreach (var a in i.Annotations)
					yield return a;
		}

		public XElement ToXElement ()
		{
			return new XElement (GetElementName (),
					new XAttribute ("abstract",                 (classFile.AccessFlags & ClassAccessFlags.Abstract) != 0),
					new XAttribute ("deprecated",               GetDeprecatedValue (classFile.Attributes)),
					GetEnclosingMethod (),
					GetExtends (),
					GetExtendsGenericAware (),
					new XAttribute ("final",                    (classFile.AccessFlags & ClassAccessFlags.Final) != 0),
					new XAttribute ("name",                     GetThisClassName ()),
					new XAttribute ("jni-signature",            classFile.FullJniName),
					GetSourceFile (),
					new XAttribute ("static",                   classFile.IsStatic),
					new XAttribute ("visibility",               GetVisibility (classFile.Visibility)),
					GetAnnotatedVisibility (classFile.Visibility, classFile.Attributes),
					GetKotlinInlineClassAttributes (),
					GetTypeParmeters (signature == null ? null : signature.TypeParameters),
					GetImplementedInterfaces (),
					GetConstructors (),
					GetMethods (),
					GetFields ()
			);
		}

		// dotnet/java-interop#1431 (Phase 2): when this class is a Kotlin
		// `@JvmInline value class`, emit two extra attributes that drive
		// generator-side wrapper-struct emission and parameter/return projection.
		IEnumerable<XAttribute> GetKotlinInlineClassAttributes ()
		{
			var underlying = classFile.KotlinInlineClassUnderlyingJniType;
			if (string.IsNullOrEmpty (underlying))
				yield break;
			yield return new XAttribute ("kotlin-inline-class", "true");
			yield return new XAttribute ("kotlin-inline-class-underlying-jni-type", underlying);
		}

		string GetElementName ()
		{
			if (IsInterface)
				return "interface";
			return "class";
		}

		static string GetDeprecatedValue (AttributeCollection attributes)
		{
			if (attributes.Get<DeprecatedAttribute> () == null)
				return "not deprecated";
			return "deprecated";
		}

		IEnumerable<XAttribute> GetEnclosingMethod ()
		{
			string? declaringClass, declaringMethod, declaringDescriptor;
			if (!classFile.TryGetEnclosingMethodInfo (out declaringClass, out declaringMethod, out declaringDescriptor)) {
				yield break;
			}
			if (declaringClass != null)
				yield return new XAttribute ("enclosing-method-jni-type",    "L" + declaringClass + ";");
			if (declaringMethod != null)
				yield return new XAttribute ("enclosing-method-name",        declaringMethod);
			if (declaringDescriptor != null)
				yield return new XAttribute ("enclosing-method-signature",   declaringDescriptor);
		}

		XAttribute[]? GetExtends ()
		{
			if (IsInterface)
				return null;
			if (classFile.SuperClass == null)
				return null;
			return new []{
				new XAttribute ("jni-extends",  "L" + classFile.SuperClass.Name.Value + ";"),
				new XAttribute ("extends",      BinaryNameToJavaClassName (classFile.SuperClass.Name.Value)),
			};
		}

		XAttribute? GetExtendsGenericAware ()
		{
			if (IsInterface)
				return null;
			if (classFile.SuperClass == null)
				return null;
			var superSig    = classFile.SuperClass.Name.Value;
			return new XAttribute ("extends-generic-aware",
				signature != null
					? SignatureToGenericJavaTypeName (signature.SuperclassSignature)
					: BinaryNameToJavaClassName (superSig));
		}

		static string GetVisibility (ClassAccessFlags accessFlags)
		{
			if ((accessFlags & ClassAccessFlags.Public) != 0)
				return "public";
			if ((accessFlags & ClassAccessFlags.Protected) != 0)
				return "protected";
			if ((accessFlags & ClassAccessFlags.Private) != 0)
				return "private";
			if (accessFlags.HasFlag (ClassAccessFlags.Internal))
				return "public";    // TODO: `kotlin-internal` at some point?  See also GetAnnotatedVisibility()
			return "";
		}

		string GetThisClassName ()
		{
			return GetName (classFile.ThisClass.Name.Value);
		}

		static string GetName (string value)
		{
			int s = value.LastIndexOf ('/');
			if (s >= 0)
				value = value.Substring (s + 1);
			return value.Replace ('$', '.');
		}

		static string BinaryNameToJavaClassName (string? value)
		{
			if (value == null || string.IsNullOrEmpty (value))
				return string.Empty;
			return value.Replace ('/', '.').Replace ('$', '.');
		}

		string SignatureToJavaTypeName (string? value)
		{
			if (value == null || string.IsNullOrEmpty (value))
				return string.Empty;
			int index   = 0;
			var array   = GetArraySuffix (value, ref index);
			var builtin = GetBuiltinName (value, ref index);
			if (builtin != null)
				return builtin + array;
			if (value [index] == 'L') {
				index++;
				var type    = new StringBuilder ();
				int depth   = 0;
				int e       = index;
				while (e < value.Length) {
					var c = value [e++];
					if (depth == 0 && c == ';')
						break;

					if (c == '<') {
						depth++;
					} else if (c == '>') {
						depth--;
					} else if (depth > 0) {
						;
					} else if (c == '/' || c == '$') {
						type.Append ('.');
					} else {
						type.Append (c);
					}
				}
				return type.Append (array).ToString ();
			}
			if (value [index] == 'T') {
				index++;
				var tp  = Signature.ExtractIdentifier (value, ref index);
				if (signature != null)
					return SignatureToJavaTypeName (signature.TypeParameters [tp].ClassBound) + array;
			}
			return value;
		}

		static string SignatureToGenericJavaTypeName (string? value)
		{
			if (value == null || string.IsNullOrEmpty (value))
				return string.Empty;
			int index   = 0;
			var type    = new StringBuilder ();
			return AppendGenericTypeNameFromSignature (type, value, ref index)
				.ToString ();
		}

		static StringBuilder AppendGenericTypeNameFromSignature (StringBuilder typeBuilder, string value, ref int index)
		{
			var array   = GetArraySuffix (value, ref index);
			var builtin = GetBuiltinName (value, ref index);
			if (builtin != null) {
				return typeBuilder.Append (builtin).Append (array);
			}
			switch (value [index]) {
			case 'L':
				index++;
				int depth   = 0;
				while (index < value.Length) {
					var c   = value [index++];
					if (depth == 0 && c == ';')
						break;

					if (c == '<') {
						depth++;
						typeBuilder.Append ("<");
						AppendGenericTypeNameFromSignature (typeBuilder, value, ref index);
					} else if (c == '>') {
						typeBuilder.Append (">");
						depth--;
					} else if (depth > 0) {
						index--;
						typeBuilder.Append (", ");
						AppendGenericTypeNameFromSignature (typeBuilder, value, ref index);
					} else if (c == '/' || c == '$') {
						typeBuilder.Append ('.');
					} else {
						typeBuilder.Append (c);
					}
				}
				return typeBuilder.Append (array);
			case 'T':
				index++;
				typeBuilder.Append (Signature.ExtractIdentifier (value, ref index));
				index++;    // consume ';'
				return typeBuilder.Append(array);
			case '*':
				index++;
				return typeBuilder.Append ("?");
			case '+':
				index++;
				typeBuilder.Append ("? extends ");
				return AppendGenericTypeNameFromSignature (typeBuilder, value, ref index);
			case '-':
				index++;
				typeBuilder.Append ("? super ");
				return AppendGenericTypeNameFromSignature (typeBuilder, value, ref index);
			}
			typeBuilder.Append ("/* should not be reached */").Append (value.Substring (index));
			index = value.Length;
			return typeBuilder;
		}

		static string? GetArraySuffix (string value, ref int index)
		{
			int o   = index;
			while (value [index] == '[') {
				index++;
			}
			if (o == index)
				return null;
			return string.Join ("", Enumerable.Repeat ("[]", index - o));
		}

		static string? GetBuiltinName (string value, ref int index)
		{
			switch (value [index]) {
			case 'B':   index++;    return "byte";
			case 'C':   index++;    return "char";
			case 'D':   index++;    return "double";
			case 'F':   index++;    return "float";
			case 'I':   index++;    return "int";
			case 'J':   index++;    return "long";
			case 'S':   index++;    return "short";
			case 'V':   index++;    return "void";
			case 'Z':   index++;    return "boolean";
			}
			return null;
		}

		XAttribute? GetSourceFile ()
		{
			var sourceFile  = classFile.SourceFileName;
			if (sourceFile == null)
				return null;
			return new XAttribute ("source-file-name", sourceFile);
		}

		XElement? GetTypeParmeters (TypeParameterInfoCollection? typeParameters)
		{
			if (typeParameters == null || typeParameters.Count == 0)
				return null;
			return new XElement ("typeParameters",
					typeParameters.Select (tp =>
						new XElement ("typeParameter",
							new XAttribute ("name",             tp.Identifier),
							new XAttribute ("jni-classBound",   tp.ClassBound ?? ""),
							new XAttribute ("classBound",       SignatureToGenericJavaTypeName (tp.ClassBound)),
							new XAttribute ("interfaceBounds",  string.Join (":", tp.InterfaceBounds.Select (_ => SignatureToGenericJavaTypeName (_)))),
							new XAttribute ("jni-interfaceBounds",  string.Join (":", tp.InterfaceBounds)))));
		}

		IEnumerable<XElement> GetImplementedInterfaces ()
		{
			if (signature != null) {
				if (signature.SuperinterfaceSignatures.Count != classFile.Interfaces.Count) {
					Console.Error.WriteLine ("class-parse: warning: class' Signature's superinterfaces count doesn't match Interfaces count!");
				}
				int max = Math.Min (signature.SuperinterfaceSignatures.Count, classFile.Interfaces.Count);
				for (int i = 0; i < max; ++i) {
					yield return new XElement ("implements",
						new XAttribute ("name",                 BinaryNameToJavaClassName (classFile.Interfaces [i].Name.Value)),
						new XAttribute ("name-generic-aware",   SignatureToGenericJavaTypeName (signature.SuperinterfaceSignatures [i])),
						new XAttribute ("jni-type",             signature.SuperinterfaceSignatures [i]));
				}
				yield break;
			}
			foreach (var c in classFile.Interfaces) {
				var n = BinaryNameToJavaClassName (c.Name.Value);
				yield return new XElement ("implements",
						new XAttribute ("name",                 n),
						new XAttribute ("name-generic-aware",   n),
						new XAttribute ("jni-type",             "L" + c.Name.Value + ";"));
			}
		}

		IEnumerable<XElement> GetConstructors ()
		{
			return classFile.Methods.Where (m => m.Name == "<init>" 
					&& (GetMethodVisibility(m.AccessFlags) == "public" || GetMethodVisibility(m.AccessFlags) == "protected" || GetMethodVisibility (m.AccessFlags) == "kotlin-internal"))
				.OrderBy (m => m.Name + m.Descriptor, StringComparer.OrdinalIgnoreCase)
				.Select (c => GetMethod ("constructor", GetThisClassName (), c, null));
		}

		XElement GetMethod (string element, string name, MethodInfo method, string? returns = null)
		{
			var abstr   = element == "method"
				? new XAttribute ("abstract", (method.AccessFlags & MethodAccessFlags.Abstract) != 0)
				: null;
			var ret     = returns != null
				? new XAttribute ("return",     SignatureToGenericJavaTypeName (returns))
				: null;
			if (!string.IsNullOrWhiteSpace (method.KotlinReturnType))
				ret?.SetValue (method.KotlinReturnType);
			var jniRet  = returns != null
				? new XAttribute ("jni-return", returns)
				: null;
			var msig    = method.GetSignature ();
			return new XElement (element,
				abstr,
				new XAttribute ("deprecated",   GetDeprecatedValue (method.Attributes)),
				new XAttribute ("final",        (method.AccessFlags & MethodAccessFlags.Final) != 0),
				new XAttribute ("name",         name),
				GetManagedName (method),
				GetNative (method),
				ret,
				jniRet,
				GetKotlinInlineClassReturnJniType (method),
				new XAttribute ("static",       (method.AccessFlags & MethodAccessFlags.Static) != 0),
				GetSynchronized (method),
				new XAttribute ("visibility",   GetVisibility (method.AccessFlags)),
				GetAnnotatedVisibility (method.Attributes),
				new XAttribute ("bridge",       (method.AccessFlags & MethodAccessFlags.Bridge) != 0),
				new XAttribute ("synthetic",    (method.AccessFlags & MethodAccessFlags.Synthetic) != 0),
				new XAttribute ("jni-signature",    method.Descriptor),
				GetNotNull (method),
				GetTypeParmeters (msig == null ? null : msig.TypeParameters),
				GetMethodParameters (method),
				GetExceptions (method));
		}

		// dotnet/java-interop#1431 (Phase 2): when the Kotlin compiler mangled
		// the JVM method name for inline-class binary compatibility (e.g.
		// `tint-Rn_QMJI`), expose the unmangled Kotlin source name via
		// `managedName` so the generator emits a clean C# overload. The JVM
		// name remains in `name`/`jni-signature` for JNI invocation.
		static XAttribute? GetManagedName (MethodInfo method)
		{
			if (string.IsNullOrEmpty (method.KotlinName))
				return null;
			return new XAttribute ("managedName", method.KotlinName);
		}

		// dotnet/java-interop#1431 (Phase 2): when a method's Kotlin source-level
		// return type was a `@JvmInline value class`, surface that type's JNI
		// signature so the generator can project the return type to a wrapper struct.
		static XAttribute? GetKotlinInlineClassReturnJniType (MethodInfo method)
		{
			if (string.IsNullOrEmpty (method.KotlinInlineClassReturnJniType))
				return null;
			return new XAttribute ("kotlin-inline-class-return-jni-type", method.KotlinInlineClassReturnJniType);
		}

		static XAttribute? GetNative (MethodInfo method)
		{
			if (method.IsConstructor)
				return null;
			return new XAttribute ("native",    (method.AccessFlags & MethodAccessFlags.Native) != 0);
		}

		static XAttribute? GetSynchronized (MethodInfo method)
		{
			if (method.IsConstructor)
				return null;
			return new XAttribute ("synchronized",  (method.AccessFlags & MethodAccessFlags.Synchronized) != 0);
		}

		static string GetVisibility (MethodAccessFlags accessFlags)
		{
			if (accessFlags.HasFlag (MethodAccessFlags.Internal))
				return "kotlin-internal";
			if ((accessFlags & MethodAccessFlags.Public) != 0)
				return "public";
			if ((accessFlags & MethodAccessFlags.Protected) != 0)
				return "protected";
			if ((accessFlags & MethodAccessFlags.Private) != 0)
				return "private";
			return "";
		}

		IEnumerable<XElement> GetMethodParameters (MethodInfo method)
		{
			var invisible = method.Attributes?.OfType<RuntimeInvisibleParameterAnnotationsAttribute> ().FirstOrDefault ()?.Annotations;
			var visible   = method.Attributes?.OfType<RuntimeVisibleParameterAnnotationsAttribute> ().FirstOrDefault ()?.Annotations;
			IList<ParameterAnnotation>? annotations = invisible;
			if (annotations == null) {
				annotations = visible;
			} else if (visible != null) {
				var merged = new List<ParameterAnnotation> (annotations);
				merged.AddRange (visible);
				annotations = merged;
			}
			var varargs     = (method.AccessFlags & MethodAccessFlags.Varargs) != 0;
			var parameters  = method.GetParameters ();
			for (int i = 0; i < parameters.Length; ++i) {
				var p           = parameters [i];
				var type        = p.Type.BinaryName;
				var genericType = p.Type.TypeSignature;
				var varargArray = (i == (parameters.Length - 1)) && varargs;
				if (varargArray) {
					Debug.Assert (p.Type.BinaryName.StartsWith ("[", StringComparison.Ordinal),
							"Varargs parameters MUST be arrays!");
					Debug.Assert (p.Type.TypeSignature != null && p.Type.TypeSignature.StartsWith ("[", StringComparison.Ordinal),
							"Varargs parameters MUST be arrays!");
					type        = type.Substring (1);
					genericType = genericType?.Substring (1);
				}
				genericType = SignatureToGenericJavaTypeName (genericType);
				if (!string.IsNullOrWhiteSpace (p.KotlinType))
					genericType = p.KotlinType;
				if (varargArray) {
					type        += "...";
					genericType += "...";
				}
				yield return new XElement ("parameter",
						new XAttribute ("name", p.Name),
						new XAttribute ("type",     genericType),
						new XAttribute ("jni-type", p.Type.TypeSignature ?? p.Type.BinaryName),
						GetKotlinInlineClassJniType (p),
						GetNotNull (method, annotations, i));
			}
		}

		// dotnet/java-interop#1431 (Phase 2): when the parameter's Kotlin source-level
		// type was a `@JvmInline value class`, surface that type's JNI signature so
		// the generator can project the parameter to a strongly-typed wrapper struct.
		static XAttribute? GetKotlinInlineClassJniType (ParameterInfo p)
		{
			if (string.IsNullOrEmpty (p.KotlinInlineClassJniType))
				return null;
			return new XAttribute ("kotlin-inline-class-jni-type", p.KotlinInlineClassJniType);
		}

		IEnumerable<XElement> GetExceptions (MethodInfo method)
		{
			foreach (var t in method.GetThrows ()) {
				yield return new XElement ("exception",
						new XAttribute ("name", t.BinaryName),
						new XAttribute ("type", BinaryNameToJavaClassName (t.BinaryName)),
						new XAttribute ("type-generic-aware",   t.TypeSignature != null
							? SignatureToGenericJavaTypeName (t.TypeSignature)
							: BinaryNameToJavaClassName (t.BinaryName)));
			}
		}

		static XAttribute? GetAnnotatedVisibility (ClassAccessFlags flags, AttributeCollection attributes)
		{
			var attr = GetAnnotatedVisibility (attributes);
			if (flags.HasFlag (ClassAccessFlags.Internal)) {
				if (attr == null) {
					attr = new XAttribute ("annotated-visibility", "module-info");
				} else {
					attr.Value += " module-info";
				}
			}
			return attr;
		}

		static XAttribute? GetAnnotatedVisibility (AttributeCollection attributes)
		{
			var annotations = attributes?.OfType<RuntimeInvisibleAnnotationsAttribute> ().FirstOrDefault ()?.Annotations;

			if (annotations?.FirstOrDefault (a => a.Type == "Landroidx/annotation/RestrictTo;") is Annotation annotation) {
				var annotation_element_values = (annotation.Values.FirstOrDefault ().Value as AnnotationElementArray)?.Values?.OfType<AnnotationElementEnum> ();

				if (annotation_element_values is null || !annotation_element_values.Any ())
					return null;

				var value_string = string.Join (" ", annotation_element_values.Select (v => v.ConstantName).Where (p => p != null));

				if (string.IsNullOrWhiteSpace (value_string))
					return null;

				return new XAttribute ("annotated-visibility", value_string);
			}

			return null;
		}

		XAttribute? GetNotNull (MethodInfo method)
		{
			var nullness = GetMethodReturnNullness (method);
			if (nullness == true)
				return new XAttribute ("return-not-null", "true");
			return null;
		}

		XAttribute? GetNotNull (MethodInfo method, IList<ParameterAnnotation>? annotations, int parameterIndex)
		{
			var nullness = GetParameterNullness (method, annotations, parameterIndex);
			if (nullness == true)
				return new XAttribute ("not-null", "true");
			return null;
		}

		XAttribute? GetNotNull (FieldInfo field)
		{
			var nullness = GetFieldNullness (field);
			if (nullness == true)
				return new XAttribute ("not-null", "true");
			return null;
		}

		bool? GetMethodReturnNullness (MethodInfo method)
		{
			if (HasDeclarationNotNullAnnotation (method.Attributes))
				return true;
			if (HasDeclarationNullableAnnotation (method.Attributes))
				return null;
			var typeNullness = GetTypeUseNullness (method.Attributes,
				ta => ta.TargetType == TypeAnnotationTargetType.MethodReturn);
			if (typeNullness.HasValue)
				return typeNullness;
			if (isNullMarked && IsReferenceTypeDescriptor (GetReturnDescriptor (method.Descriptor))
					&& !IsTopLevelTypeVariableSignature (method.ReturnType.TypeSignature))
				return true;
			return null;
		}

		bool? GetParameterNullness (MethodInfo method, IList<ParameterAnnotation>? annotations, int parameterIndex)
		{
			bool hasDeclarationNullable = false;
			if (annotations != null) {
				foreach (var pa in annotations) {
					if (pa.ParameterIndex != parameterIndex)
						continue;
					foreach (var a in pa.Annotations) {
						if (IsNotNullAnnotation (a))
							return true;
						if (IsNullableAnnotation (a))
							hasDeclarationNullable = true;
					}
				}
			}
			if (hasDeclarationNullable)
				return null;

			var typeNullness = GetTypeUseNullness (method.Attributes,
				ta => ta.TargetType == TypeAnnotationTargetType.MethodFormalParameter
					&& ta.FormalParameterIndex == parameterIndex);
			if (typeNullness.HasValue)
				return typeNullness;

			if (isNullMarked) {
				var parameters = method.GetParameters ();
				if (parameterIndex >= 0 && parameterIndex < parameters.Length
						&& IsReferenceTypeDescriptor (parameters [parameterIndex].Type.BinaryName)
						&& !IsTopLevelTypeVariableSignature (parameters [parameterIndex].Type.TypeSignature))
					return true;
			}
			return null;
		}

		bool? GetFieldNullness (FieldInfo field)
		{
			if (HasDeclarationNotNullAnnotation (field.Attributes))
				return true;
			if (HasDeclarationNullableAnnotation (field.Attributes))
				return null;
			var typeNullness = GetTypeUseNullness (field.Attributes,
				ta => ta.TargetType == TypeAnnotationTargetType.Field);
			if (typeNullness.HasValue)
				return typeNullness;
			if (isNullMarked && IsReferenceTypeDescriptor (field.Descriptor)
					&& !IsTopLevelTypeVariableSignature (field.GetSignature ()))
				return true;
			return null;
		}

		static bool HasDeclarationNotNullAnnotation (AttributeCollection? attributes)
		{
			if (attributes == null)
				return false;
			foreach (var a in EnumerateDeclarationAnnotations (attributes)) {
				if (IsNotNullAnnotation (a))
					return true;
			}
			return false;
		}

		static bool HasDeclarationNullableAnnotation (AttributeCollection? attributes)
		{
			if (attributes == null)
				return false;
			foreach (var a in EnumerateDeclarationAnnotations (attributes)) {
				if (IsNullableAnnotation (a))
					return true;
			}
			return false;
		}

		// Look in `RuntimeInvisibleTypeAnnotations` and `RuntimeVisibleTypeAnnotations`
		// for entries matching `predicate` (e.g. METHOD_RETURN, FIELD, or
		// a specific METHOD_FORMAL_PARAMETER index) at the top of the type
		// (no `type_path`). Returns true for `@NonNull`, false for
		// `@Nullable`, null for no match.
		static bool? GetTypeUseNullness (AttributeCollection? attributes, Func<TypeAnnotation, bool> predicate)
		{
			if (attributes == null)
				return null;
			bool? result = null;
			foreach (var ta in EnumerateTypeAnnotations (attributes)) {
				if (!ta.AppliesToTopLevelType)
					continue;
				if (!predicate (ta))
					continue;
				if (IsNotNullAnnotation (ta.Annotation))
					return true;
				if (IsNullableAnnotation (ta.Annotation))
					result = false;
			}
			return result;
		}

		static IEnumerable<TypeAnnotation> EnumerateTypeAnnotations (AttributeCollection attributes)
		{
			foreach (var v in attributes.OfType<RuntimeVisibleTypeAnnotationsAttribute> ())
				foreach (var a in v.Annotations)
					yield return a;
			foreach (var i in attributes.OfType<RuntimeInvisibleTypeAnnotationsAttribute> ())
				foreach (var a in i.Annotations)
					yield return a;
		}

		static bool IsNullableAnnotation (Annotation annotation)
		{
			switch (annotation.Type) {
			case "Landroid/annotation/Nullable;":
			case "Landroid/support/annotation/Nullable;":
			case "Landroidx/annotation/Nullable;":
			case "Landroidx/annotation/RecentlyNullable;":
			case "Lcom/android/annotations/Nullable;":
			case "Ledu/umd/cs/findbugs/annotations/Nullable;":
			case "Ljakarta/annotation/Nullable;":
			case "Ljavax/annotation/Nullable;":
			case "Lorg/checkerframework/checker/nullness/compatqual/NullableDecl;":
			case "Lorg/checkerframework/checker/nullness/qual/Nullable;":
			case "Lorg/eclipse/jdt/annotation/Nullable;":
			case "Lorg/jetbrains/annotations/Nullable;":
			case "Lorg/jspecify/annotations/Nullable;":
				return true;
			}
			return false;
		}

		static string GetReturnDescriptor (string descriptor)
		{
			var i = descriptor.LastIndexOf (')');
			return i < 0 ? descriptor : descriptor.Substring (i + 1);
		}

		static bool IsReferenceTypeDescriptor (string descriptor)
		{
			if (string.IsNullOrEmpty (descriptor))
				return false;
			var c = descriptor [0];
			return c == 'L' || c == '[';
		}

		// JSpecify gives unannotated type-variable *usages* parametric
		// nullness, so e.g. `<T> T get()` must not gain `not-null="true"`
		// in a null-marked scope. A JVMS signature for a type-variable
		// use begins with 'T' (followed by the variable name and ';').
		// Returns false for `null` so callers can pass the raw signature
		// when no Signature attribute is present.
		static bool IsTopLevelTypeVariableSignature (string? signature)
		{
			return !string.IsNullOrEmpty (signature) && signature [0] == 'T';
		}

		static bool IsNotNullAnnotation (Annotation annotation)
		{
			// Android ones plus the list from here:
			// https://stackoverflow.com/questions/4963300/which-notnull-java-annotation-should-i-use
			// https://github.com/JetBrains/kotlin/blob/03360c0108797b2a98b6608e2bddfacd5f4e87ce/core/compiler.common.jvm/src/org/jetbrains/kotlin/load/java/JvmAnnotationNames.kt#L64-L91
			switch (annotation.Type) {
				case "Landroid/annotation/NonNull;":
				case "Landroid/support/annotation/NonNull;":
				case "Landroidx/annotation/NonNull;":
				case "Landroidx/annotation/RecentlyNonNull;":
				case "Lcom/android/annotations/NonNull;":
				case "Ledu/umd/cs/findbugs/annotations/NonNull;":
				case "Ljakarta/annotation/Nonnull;":
				case "Ljavax/annotation/Nonnull;":
				case "Ljavax/validation/constraints/NotNull;":
				case "Llombok/NonNull;":
				case "Lorg/checkerframework/checker/nullness/compatqual/NonNullDecl;":
				case "Lorg/checkerframework/checker/nullness/qual/NonNull;":
				case "Lorg/eclipse/jdt/annotation/NonNull;":
				case "Lorg/jetbrains/annotations/NotNull;":
				case "Lorg/jspecify/annotations/NonNull;":
					return true;
			}

			return false;
		}

		IEnumerable<XElement> GetFields ()
		{
			foreach (var field in classFile.Fields.OrderBy (n => n.Name, StringComparer.OrdinalIgnoreCase)) {
				var visibility = GetVisibility (field.AccessFlags);
				if (visibility == "private" || visibility == "")
					continue;
				var type = new XAttribute ("type", SignatureToJavaTypeName (field.Descriptor));
				if (!string.IsNullOrWhiteSpace (field.KotlinType))
					type.SetValue (field.KotlinType);
				yield return new XElement ("field",
						new XAttribute ("deprecated",           GetDeprecatedValue (field.Attributes)),
						new XAttribute ("final",                (field.AccessFlags & FieldAccessFlags.Final) != 0),
						new XAttribute ("name",                 field.Name),
						new XAttribute ("static",               (field.AccessFlags & FieldAccessFlags.Static) != 0),
						new XAttribute ("synthetic",            (field.AccessFlags & FieldAccessFlags.Synthetic) != 0),
						new XAttribute ("transient",            (field.AccessFlags & FieldAccessFlags.Transient) != 0),
						type,
						new XAttribute ("type-generic-aware",   GetGenericType (field)),
						new XAttribute ("jni-signature",        field.Descriptor),
						GetNotNull (field),
						GetValue (field),
						new XAttribute ("visibility",           visibility),
						GetAnnotatedVisibility (field.Attributes),
						new XAttribute ("volatile",             (field.AccessFlags & FieldAccessFlags.Volatile) != 0));
			}
		}

		string GetGenericType (FieldInfo field)
		{
			var signature = field.GetSignature ();
			if (signature == null)
				return SignatureToJavaTypeName (field.Descriptor);
			return SignatureToGenericJavaTypeName (signature);
		}

		static XAttribute? GetValue (FieldInfo field)
		{
			var constantValue = (ConstantValueAttribute?) field.Attributes.FirstOrDefault (a => a.Name == "ConstantValue");
			if (constantValue == null)
				return null;
			var value       = "";
			var constant    = constantValue.Constant;
			switch (constant.Type) {
			case ConstantPoolItemType.Double:
				var doubleItem = (ConstantPoolDoubleItem)constant;
				if (Double.IsNaN (doubleItem.Value))
					value = "(0.0 / 0.0)";
				else if (Double.IsNegativeInfinity (doubleItem.Value))
					value = "(-1.0 / 0.0)";
				else if (Double.IsPositiveInfinity (doubleItem.Value))
					value = "(1.0 / 0.0)";
				else
					value = doubleItem.Value.ToString ("G17", CultureInfo.InvariantCulture);
				break;
			case ConstantPoolItemType.Float:
				var floatItem = (ConstantPoolFloatItem) constant;
				if (Double.IsNaN (floatItem.Value))
					value = "(0.0f / 0.0f)";
				else if (Double.IsNegativeInfinity (floatItem.Value))
					value = "(-1.0f / 0.0f)";
				else if (Double.IsPositiveInfinity (floatItem.Value))
					value = "(1.0f / 0.0f)";
				else
					value = floatItem.Value.ToString ("G9", CultureInfo.InvariantCulture);
				break;
			case ConstantPoolItemType.Long:     value = ((ConstantPoolLongItem) constant).Value.ToString ();    break;
			case ConstantPoolItemType.Integer:
				if (field.Descriptor == "Z")
					value = ((ConstantPoolIntegerItem) constant).Value == 1 ? bool.TrueString.ToLower () : bool.FalseString.ToLower ();
				else
					value = ((ConstantPoolIntegerItem) constant).Value.ToString (); 
				break;
			case ConstantPoolItemType.String:
				value = '"' + EscapeLiteral (((ConstantPoolStringItem) constant).StringData.Value) + '"';
				break;
			default:
				throw new InvalidOperationException ("Unable to get value for: " + constant);
			}
			return new XAttribute ("value", value);
		}

		static string EscapeLiteral (string value)
		{
			bool fixup = false;
			for (int i = 0; i < value.Length; ++i) {
				var c = value [i];
				if (c < 0x20 || c > 0xff || c == '\\' || c == '"') {
					fixup = true;
					break;
				}
			}
			if (fixup) {
				var sb = new StringBuilder ();
				for (int i = 0; i < value.Length; ++i) {
					var c = value [i];
					if (c == '\\') {
						sb.Append (@"\\");
						continue;
					}
					if (c == '"') {
						sb.Append ("\\\"");
						continue;
					}
					if (c < 0x20 || c > 0xff) {
						sb.Append ("\\u").AppendFormat ("{0:x4}", (int)c);
						continue;
					}
					sb.Append (c);
				}
				value = sb.ToString ();
			}
			return value;
		}

		static string GetVisibility (FieldAccessFlags accessFlags)
		{
			if (accessFlags.HasFlag (FieldAccessFlags.Internal))
				return "kotlin-internal";
			if ((accessFlags & FieldAccessFlags.Public) != 0)
				return "public";
			if ((accessFlags & FieldAccessFlags.Protected) != 0)
				return "protected";
			if ((accessFlags & FieldAccessFlags.Private) != 0)
				return "private";
			return "";
		}

		static string GetMethodVisibility (MethodAccessFlags accessFlags)
		{
			if (accessFlags.HasFlag (MethodAccessFlags.Internal))
				return "kotlin-internal";
			if ((accessFlags & MethodAccessFlags.Public) != 0)
				return "public";
			if ((accessFlags & MethodAccessFlags.Protected) != 0)
				return "protected";
			if ((accessFlags & MethodAccessFlags.Private) != 0)
				return "private";
			return "";
		}

		static string GetClassVisibility (ClassAccessFlags accessFlags)
		{
			if ((accessFlags & ClassAccessFlags.Public) != 0)
				return "public";
			if ((accessFlags & ClassAccessFlags.Protected) != 0)
				return "protected";
			if ((accessFlags & ClassAccessFlags.Private) != 0)
				return "private";
			return "";
		}

		IEnumerable<XElement> GetMethods ()
		{
			return classFile.Methods.Where (m => !m.Name.StartsWith ("<", StringComparison.OrdinalIgnoreCase)
						&& (GetMethodVisibility(m.AccessFlags) == "public" || GetMethodVisibility(m.AccessFlags) == "protected" || GetMethodVisibility (m.AccessFlags) == "kotlin-internal"))
				.OrderBy (m => m.Name + m.Descriptor, StringComparer.OrdinalIgnoreCase)
				.Select (m => GetMethod ("method", m.Name, m,
					returns: m.ReturnType.TypeSignature));
		}
	}
}

