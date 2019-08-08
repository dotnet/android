using System;
using System.Collections.Generic;
using System.Linq;

using Mono.Cecil;

using Java.Interop.Tools.Cecil;

using Mono.Linker;
using Mono.Linker.Steps;
using Mono.Tuner;

namespace MonoDroid.Tuner
{
	public class FixAbstractMethodsStep : BaseStep
	{
		protected override void ProcessAssembly (AssemblyDefinition assembly)
		{
			if (!Annotations.HasAction (assembly))
				Annotations.SetAction (assembly, AssemblyAction.Skip);

			if (Profile.IsSdkAssembly (assembly) || Profile.IsProductAssembly (assembly))
				return;

			bool changed = false;
			foreach (var type in assembly.MainModule.Types) {
				if (MightNeedFix (type))
					changed |= FixAbstractMethods (type);
			}

			if (changed) {
				Context.SafeReadSymbols (assembly);
				AssemblyAction action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;
				if (action == AssemblyAction.Skip || action == AssemblyAction.Copy || action == AssemblyAction.Delete)
					Annotations.SetAction (assembly, AssemblyAction.Save);
				var td = AbstractMethodErrorConstructor.DeclaringType.Resolve ();
				Annotations.Mark (td);
				Annotations.SetPreserve (td, TypePreserve.Nothing);
				Annotations.AddPreservedMethod (td, AbstractMethodErrorConstructor.Resolve ());
			}
		}

		bool MightNeedFix (TypeDefinition type)
		{
			return !type.IsAbstract && type.IsSubclassOf ("Java.Lang.Object");
		}

		static bool CompareTypes (TypeReference iType, TypeReference tType)
		{
			if (iType.IsGenericParameter)
				return true;

			if (iType.IsArray) {
				if (!tType.IsArray)
					return false;
				return CompareTypes (iType.GetElementType (), tType.GetElementType ());
			}

			if (iType.IsByReference) {
				if (!tType.IsByReference)
					return false;
				return CompareTypes (iType.GetElementType (), tType.GetElementType ());
			}

			if (iType.Name != tType.Name)
				return false;

			if (iType.Namespace != tType.Namespace)
				return false;

			TypeDefinition iTypeDef = iType.Resolve ();
			if (iTypeDef == null)
				return false;

			TypeDefinition tTypeDef = tType.Resolve ();
			if (tTypeDef == null)
				return false;

			if (iTypeDef.Module.FullyQualifiedName != tTypeDef.Module.FullyQualifiedName)
				return false;

			if (iType is Mono.Cecil.GenericInstanceType && tType is Mono.Cecil.GenericInstanceType) {
				GenericInstanceType iGType = iType as GenericInstanceType;
				GenericInstanceType tGType = tType as GenericInstanceType;

				if (iGType.GenericArguments.Count != tGType.GenericArguments.Count)
					return false;
				for (int i = 0; i < iGType.GenericArguments.Count; i++) {
					if (iGType.GenericArguments [i].IsGenericParameter)
						continue;
					if (!CompareTypes (iGType.GenericArguments [i], tGType.GenericArguments [i]))
						return false;
				}
			}

			return true;
		}

		bool IsInOverrides (MethodDefinition iMethod, MethodDefinition tMethod)
		{
			if (!tMethod.HasOverrides)
				return false;

			foreach (var o in tMethod.Overrides)
				if (o != null && iMethod == o.Resolve ())
					return true;

			return false;
		}

		bool HaveSameSignature (TypeReference iface, MethodDefinition iMethod, MethodDefinition tMethod)
		{
			if (IsInOverrides (iMethod, tMethod))
				return true;

			if (iMethod.Name != tMethod.Name && (iMethod.DeclaringType == null || (iMethod.DeclaringType.DeclaringType == null ? (string.Format ("{0}.{1}", iface.FullName, iMethod.Name) != tMethod.Name) : (string.Format ("{0}.{1}.{2}", iMethod.DeclaringType.DeclaringType, iface.Name, iMethod.Name) != tMethod.Name))))
				return false;

			if (!CompareTypes (iMethod.MethodReturnType.ReturnType, tMethod.MethodReturnType.ReturnType))
				return false;

			if (iMethod.Parameters.Count != tMethod.Parameters.Count || iMethod.GenericParameters.Count != tMethod.GenericParameters.Count)
				return false;

			if (iMethod.HasParameters) {
				List<ParameterDefinition> m1p = new List<ParameterDefinition> (iMethod.Parameters);
				List<ParameterDefinition> m2p = new List<ParameterDefinition> (tMethod.Parameters);

				for (int i = 0; i < m1p.Count; i++) {
					if (!CompareTypes (m1p [i].ParameterType,  m2p [i].ParameterType))
						return false;
				}
			}

			if (iMethod.HasGenericParameters) {
				List<GenericParameter> m1p = new List<GenericParameter> (iMethod.GenericParameters);
				List<GenericParameter> m2p = new List<GenericParameter> (tMethod.GenericParameters);

				for (int i = 0; i < m1p.Count; i++)
					if (!CompareTypes (m1p [i], m2p [i]))
						return false;
			}

			return true;
		}

		bool FixAbstractMethods (TypeDefinition type)
		{
			if (!type.HasInterfaces)
				return false;

			bool rv = false;
			List<MethodDefinition> typeMethods = new List<MethodDefinition> (type.Methods);
			foreach (var baseType in type.GetBaseTypes ())
				typeMethods.AddRange (baseType.Methods);

			foreach (var ifaceInfo in type.Interfaces) {
				var iface    = ifaceInfo.InterfaceType;
				var ifaceDef = iface.Resolve ();
				if (ifaceDef == null) {
					Context.LogMessage ("Unable to unresolve interface: {0}", iface.FullName);
					continue;
				}
				if (ifaceDef.HasGenericParameters)
					continue;

				foreach (var iMethod in ifaceDef.Methods.Where (m => m.IsAbstract)) {
					bool exists = false;

					foreach (var tMethod in typeMethods) {
						if (HaveSameSignature (iface, iMethod, tMethod)) {
							exists = true;
							break;
						}
					}

					if (!exists) {
						AddNewExceptionMethod (type, iMethod);
						rv = true;
					}
				}
			}

			return rv;
		}

		TypeReference TryImportType (TypeDefinition declaringType, TypeReference type)
		{
			if (type.IsGenericParameter)
				return type;

			return declaringType.Module.Import (type);
		}

		void AddNewExceptionMethod (TypeDefinition type, MethodDefinition method)
		{
			var newMethod = new MethodDefinition (method.Name, (method.Attributes | MethodAttributes.Final) & ~MethodAttributes.Abstract, TryImportType (type, method.ReturnType));

			foreach (var paramater in method.Parameters)
				newMethod.Parameters.Add (new ParameterDefinition (paramater.Name, paramater.Attributes, TryImportType (type, paramater.ParameterType)));

			var ilP = newMethod.Body.GetILProcessor ();

			ilP.Append (ilP.Create (Mono.Cecil.Cil.OpCodes.Newobj, type.Module.Import (AbstractMethodErrorConstructor)));
			ilP.Append (ilP.Create (Mono.Cecil.Cil.OpCodes.Throw));

			type.Methods.Add (newMethod);

			Context.LogMessage ("Added method: {0} to type: {1} scope: {2}", method, type.FullName, type.Scope);
		}

		MethodReference abstractMethodErrorConstructor;

		MethodReference AbstractMethodErrorConstructor {
			get {
				if (abstractMethodErrorConstructor != null)
					return abstractMethodErrorConstructor;

				foreach (var assembly in Context.GetAssemblies ()) {
					if (assembly.Name.Name != "Mono.Android")
						continue;

					var errorException = assembly.MainModule.GetType ("Java.Lang.AbstractMethodError");
					if (errorException == null)
						break;

					foreach (var method in errorException.Methods)
						if (method.Name == ".ctor" && !method.HasParameters) {
							abstractMethodErrorConstructor = method;
							break;
						}
				}

				if (abstractMethodErrorConstructor == null)
					throw new Exception ("Unable to find Java.Lang.AbstractMethodError constructor in Mono.Android assembly");

				return abstractMethodErrorConstructor;
			}
		}
	}
}
