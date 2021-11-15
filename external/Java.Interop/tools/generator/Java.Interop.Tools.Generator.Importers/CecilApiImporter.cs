using System;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace MonoDroid.Generation
{
	class CecilApiImporter
	{
		public static ClassGen CreateClass (TypeDefinition t, CodeGenerationOptions opt)
		{
			var klass = new ClassGen (CreateGenBaseSupport (t, opt)) {
				IsAbstract = t.IsAbstract,
				IsFinal = t.IsSealed,
				IsShallow = opt.UseShallowReferencedTypes,
			};

			foreach (var ifaceImpl in t.Interfaces) {
				var iface = ifaceImpl.InterfaceType;
				var def = ifaceImpl.InterfaceType.Resolve ();

				if (def != null && def.IsNotPublic)
					continue;

				klass.AddImplementedInterface (iface.FullNameCorrected ());
			}

			Action populate = () => {
				var implements_charsequence = t.Interfaces.Any (it => it.InterfaceType.FullName == "Java.Lang.CharSequence");

				foreach (var m in t.Methods) {
					if (m.IsPrivate || m.IsAssembly || GetRegisterAttribute (m.CustomAttributes) == null)
						continue;
					if (implements_charsequence && t.Methods.Any (mm => mm.Name == m.Name + "Formatted"))
						continue;
					if (m.IsConstructor)
						klass.Ctors.Add (CreateCtor (klass, m));
					else
						klass.AddMethod (CreateMethod (klass, m));
				}

				foreach (var f in t.Fields)
					if (!f.IsPrivate && GetRegisterAttribute (f.CustomAttributes) == null)
						klass.AddField (CreateField (f));
			};

			if (klass.IsShallow)
				klass.PopulateAction = populate;
			else
				populate ();

			TypeReference nominal_base_type;

			for (nominal_base_type = t.BaseType; nominal_base_type != null && (nominal_base_type.HasGenericParameters || nominal_base_type.IsGenericInstance); nominal_base_type = nominal_base_type.Resolve ().BaseType)
				; // iterate up to non-generic type, at worst System.Object.

			klass.BaseType = nominal_base_type?.FullNameCorrected ();

			return klass;
		}

		public static Ctor CreateCtor (GenBase declaringType, MethodDefinition m)
		{
			var reg_attr = GetRegisterAttribute (m.CustomAttributes);

			var ctor = new Ctor (declaringType) {
				AssemblyName = m.DeclaringType.Module.Assembly.FullName,
				Deprecated = m.Deprecated (),
				GenericArguments = m.GenericArguments (),
				IsAcw = reg_attr != null,
				// not a beautiful way to check static type, yes :|
				IsNonStaticNestedType = m.DeclaringType.IsNested && !(m.DeclaringType.IsAbstract && m.DeclaringType.IsSealed),
				Name = m.Name,
				Visibility = m.Visibility ()
			};

			// If 'elem' is a constructor for a non-static nested type, then
			// the type of the containing class must be inserted as the first
			// argument
			if (ctor.IsNonStaticNestedType)
				ctor.Parameters.AddFirst (CreateParameter (m.DeclaringType.DeclaringType.FullName, ctor.DeclaringType.JavaName));

			foreach (var p in m.GetParameters (reg_attr))
				ctor.Parameters.Add (p);

			return ctor;
		}

		public static Field CreateField (FieldDefinition f)
		{
			var obs_attr = GetObsoleteAttribute (f.CustomAttributes);
			var reg_attr = GetRegisterAttribute (f.CustomAttributes);

			var field = new Field {
				DeprecatedComment = GetObsoleteComment (obs_attr),
				IsAcw = reg_attr != null,
				IsDeprecated = obs_attr != null,
				IsEnumified = GetGeneratedEnumAttribute (f.CustomAttributes) != null,
				IsFinal = f.Constant != null,
				IsStatic = f.IsStatic,
				JavaName = reg_attr != null ? ((string) reg_attr.ConstructorArguments [0].Value).Replace ('/', '.') : f.Name,
				Name = f.Name,
				NotNull = f.GetTypeNullability () == Nullability.NotNull,
				TypeName = f.FieldType.FullNameCorrected ().StripArity (),
				Value = f.Constant == null ? null : f.FieldType.FullName == "System.String" ? '"' + f.Constant.ToString () + '"' : f.Constant.ToString (),
				Visibility = f.IsPublic ? "public" : f.IsFamilyOrAssembly ? "protected internal" : f.IsFamily ? "protected" : f.IsAssembly ? "internal" : "private"
			};

			field.SetterParameter = CreateParameter (f.FieldType.Resolve ()?.FullName ?? f.FieldType.FullName, null);
			field.SetterParameter.Name = "value";

			return field;
		}

		public static GenBaseSupport CreateGenBaseSupport (TypeDefinition t, CodeGenerationOptions opt)
		{
			var obs_attr = GetObsoleteAttribute (t.CustomAttributes);
			var reg_attr = GetRegisterAttribute (t.CustomAttributes);

			var jn = reg_attr != null ? ((string) reg_attr.ConstructorArguments [0].Value).Replace ('/', '.') : t.FullNameCorrected ();
			var idx = jn.LastIndexOf ('.');

			var support = new GenBaseSupport {				
				IsAcw = reg_attr != null,
				IsDeprecated = obs_attr != null,
				IsGeneratable = false,
				IsGeneric = t.HasGenericParameters,
				IsObfuscated = false, // obfuscated types have no chance to be already bound in managed types.
				Name = t.Name,
				Namespace = t.Namespace,
				PackageName = idx < 0 ? string.Empty : jn.Substring (0, idx),
				TypeParameters = GenericParameterDefinitionList.FromMetadata (t.GenericParameters),
				Visibility = t.IsPublic || t.IsNestedPublic ? "public" : "protected internal"
			};

			support.JavaSimpleName = TypeNameUtilities.FilterPrimitiveFullName (t.FullNameCorrected ());

			if (support.JavaSimpleName == null) {
				support.JavaSimpleName = idx < 0 ? jn : jn.Substring (idx + 1);
				support.FullName = t.FullNameCorrected ();
			} else {
				var sym = opt.SymbolTable.Lookup (support.JavaSimpleName);
				support.FullName = sym != null ? sym.FullName : t.FullNameCorrected ();
			}

			support.JavaSimpleName = support.JavaSimpleName.Replace ('$', '.');

			if (support.IsDeprecated)
				support.DeprecatedComment = GetObsoleteComment (obs_attr) ?? "This class is obsoleted in this android platform";

			return support;
		}

		public static InterfaceGen CreateInterface (TypeDefinition t, CodeGenerationOptions opt)
		{
			var iface = new InterfaceGen (CreateGenBaseSupport (t, opt)) {
				IsShallow = opt.UseShallowReferencedTypes,
			};

			foreach (var ifaceImpl in t.Interfaces)
				iface.AddImplementedInterface (ifaceImpl.InterfaceType.FullNameCorrected ());

			Action populate = () => {
				foreach (var m in t.Methods) {
					if (m.IsPrivate || m.IsAssembly || GetRegisterAttribute (m.CustomAttributes) == null)
						continue;

					iface.AddMethod (CreateMethod (iface, m));
				}
			};

			if (iface.IsShallow)
				iface.PopulateAction = populate;
			else
				populate ();

			iface.MayHaveManagedGenericArguments = !iface.IsAcw;

			return iface;
		}

		public static Method CreateMethod (GenBase declaringType, MethodDefinition m)
		{
			var reg_attr = GetRegisterAttribute (m.CustomAttributes);

			var method = new Method (declaringType) {
				AssemblyName = m.DeclaringType.Module.Assembly.FullName,
				Deprecated = m.Deprecated (),
				GenerateAsyncWrapper = false,
				GenericArguments = m.GenericArguments (),
				IsAbstract = m.IsAbstract,
				IsAcw = reg_attr != null,
				IsFinal = m.IsFinal,
				IsInterfaceDefaultMethod = IsDefaultInterfaceMethod (declaringType, m),
				IsReturnEnumified = GetGeneratedEnumAttribute (m.MethodReturnType.CustomAttributes) != null,
				IsStatic = m.IsStatic,
				IsVirtual = m.IsVirtual,
				JavaName = reg_attr != null ? ((string) reg_attr.ConstructorArguments [0].Value) : m.Name,
				ManagedReturn = m.ReturnType.FullNameCorrected ().StripArity ().FilterPrimitive (),
				Return = m.ReturnType.FullNameCorrected ().StripArity ().FilterPrimitive (),
				ReturnNotNull = m.GetReturnTypeNullability () == Nullability.NotNull,
				Visibility = m.Visibility ()
			};

			foreach (var p in m.GetParameters (reg_attr))
				method.Parameters.Add (p);

			if (reg_attr != null) {
				var jnisig = (string) (reg_attr.ConstructorArguments.Count > 1 ? reg_attr.ConstructorArguments [1].Value : reg_attr.Properties.First (p => p.Name == "JniSignature").Argument.Value);
				var rt = JavaNativeTypeManager.ReturnTypeFromSignature (jnisig);
				if (rt?.Type != null)
					method.Return = rt.Type;
			}

			method.FillReturnType ();

			// Strip "Formatted" from ICharSequence-based method.
			var name_base = method.IsReturnCharSequence ? m.Name.Substring (0, m.Name.Length - "Formatted".Length) : m.Name;

			method.Name = m.IsGetter ? (m.Name.StartsWith ("get_Is", StringComparison.Ordinal) && m.Name.Length > 6 && char.IsUpper (m.Name [6]) ? string.Empty : "Get") + name_base.Substring (4) : m.IsSetter ? (m.Name.StartsWith ("set_Is", StringComparison.Ordinal) && m.Name.Length > 6 && char.IsUpper (m.Name [6]) ? string.Empty : "Set") + name_base.Substring (4) : name_base;

			return method;
		}

		public static Parameter CreateParameter (ParameterDefinition p, string jnitype, string rawtype, bool isNotNull)
		{
			// FIXME: safe to use CLR type name? assuming yes as we often use it in metadatamap.
			// FIXME: IsSender?
			var isEnumType = GetGeneratedEnumAttribute (p.CustomAttributes) != null;
			return new Parameter (TypeNameUtilities.MangleName (p.Name), jnitype ?? p.ParameterType.FullNameCorrected ().StripArity (), null, isEnumType, rawtype, isNotNull);
		}

		public static Parameter CreateParameter (string managedType, string javaType)
		{
			return new Parameter ("__self", javaType ?? managedType, managedType, false);
		}

		static CustomAttribute GetJavaDefaultInterfaceMethodAttribute (Collection<CustomAttribute> attributes) =>
			attributes.FirstOrDefault (a => a.AttributeType.FullName == "Java.Interop.JavaInterfaceDefaultMethodAttribute");

		static CustomAttribute GetGeneratedEnumAttribute (Collection<CustomAttribute> attributes) =>
			attributes.FirstOrDefault (a => a.AttributeType.FullName == "Android.Runtime.GeneratedEnumAttribute");

		static CustomAttribute GetObsoleteAttribute (Collection<CustomAttribute> attributes) =>
			attributes.FirstOrDefault (a => a.AttributeType.FullNameCorrected () == "System.ObsoleteAttribute");

		static string GetObsoleteComment (CustomAttribute attribute) =>
			attribute?.ConstructorArguments.Any () == true ? (string) attribute.ConstructorArguments [0].Value : null;

		static CustomAttribute GetRegisterAttribute (Collection<CustomAttribute> attributes) =>
			attributes.FirstOrDefault (a => a.AttributeType.FullNameCorrected () == "Android.Runtime.RegisterAttribute");

		static bool IsDefaultInterfaceMethod (GenBase declaringType, MethodDefinition method)
		{
			if (!(declaringType is InterfaceGen))
				return false;

			if (GetJavaDefaultInterfaceMethodAttribute (method.CustomAttributes) != null)
				return true;

			return !method.IsAbstract && !method.IsStatic;
		}
	}
}
