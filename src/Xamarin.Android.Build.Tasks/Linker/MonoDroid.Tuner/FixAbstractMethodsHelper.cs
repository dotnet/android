using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;

using Resources = Xamarin.Android.Tasks.Properties.Resources;

namespace MonoDroid.Tuner
{
	static class FixAbstractMethodsHelper
	{
		internal static bool FixAbstractMethods (AssemblyDefinition assembly, IMetadataResolver cache, ref MethodDefinition? abstractMethodErrorCtor, Func<AssemblyDefinition?> getMonoAndroidAssembly, Action<string> logMessage)
		{
			bool changed = false;
			foreach (var type in assembly.MainModule.Types) {
				if (MightNeedFix (type, cache))
					changed |= FixAbstractMethods (type, cache, ref abstractMethodErrorCtor, getMonoAndroidAssembly, logMessage);
			}
			return changed;
		}

		internal static void CheckAppDomainUsage (AssemblyDefinition assembly, Action<string> warn, HashSet<string> warnedAssemblies)
		{
			if (!warnedAssemblies.Add (assembly.Name.Name))
				return;
			if (!assembly.MainModule.HasTypeReference ("System.AppDomain"))
				return;

			foreach (var mr in assembly.MainModule.GetMemberReferences ()) {
				if (mr.ToString ().Contains ("System.AppDomain System.AppDomain::CreateDomain")) {
					warn (string.Format (CultureInfo.CurrentCulture, Resources.XA2000, assembly));
					break;
				}
			}
		}

		static bool MightNeedFix (TypeDefinition type, IMetadataResolver cache)
		{
			return !type.IsAbstract && type.IsSubclassOf ("Java.Lang.Object", cache);
		}

		static bool CompareTypes (TypeReference iType, TypeReference tType, IMetadataResolver cache)
		{
			if (iType.IsGenericParameter)
				return true;

			if (iType.IsArray) {
				if (!tType.IsArray)
					return false;
				return CompareTypes (iType.GetElementType (), tType.GetElementType (), cache);
			}

			if (iType.IsByReference) {
				if (!tType.IsByReference)
					return false;
				return CompareTypes (iType.GetElementType (), tType.GetElementType (), cache);
			}

			if (iType.Name != tType.Name)
				return false;

			if (iType.Namespace != tType.Namespace)
				return false;

			var iTypeDef = cache.Resolve (iType);
			if (iTypeDef == null)
				return false;

			var tTypeDef = cache.Resolve (tType);
			if (tTypeDef == null)
				return false;

			if (iTypeDef.Module.FileName != tTypeDef.Module.FileName)
				return false;

			if (iType is GenericInstanceType iGType && tType is GenericInstanceType tGType) {
				if (iGType.GenericArguments.Count != tGType.GenericArguments.Count)
					return false;
				for (int i = 0; i < iGType.GenericArguments.Count; i++) {
					if (iGType.GenericArguments [i].IsGenericParameter)
						continue;
					if (!CompareTypes (iGType.GenericArguments [i], tGType.GenericArguments [i], cache))
						return false;
				}
			}

			return true;
		}

		static bool IsInOverrides (MethodDefinition iMethod, MethodDefinition tMethod, IMetadataResolver cache)
		{
			if (!tMethod.HasOverrides)
				return false;

			foreach (var o in tMethod.Overrides)
				if (o != null && iMethod.Name == o.Name && iMethod == cache.Resolve (o))
					return true;

			return false;
		}

		static bool HaveSameSignature (TypeReference iface, MethodDefinition iMethod, MethodDefinition tMethod, IMetadataResolver cache)
		{
			if (IsInOverrides (iMethod, tMethod, cache))
				return true;

			if (iMethod.Name != tMethod.Name)
				return false;

			if (!CompareTypes (iMethod.MethodReturnType.ReturnType, tMethod.MethodReturnType.ReturnType, cache))
				return false;

			if (iMethod.Parameters.Count != tMethod.Parameters.Count || iMethod.GenericParameters.Count != tMethod.GenericParameters.Count)
				return false;

			if (iMethod.HasParameters) {
				List<ParameterDefinition> m1p = new List<ParameterDefinition> (iMethod.Parameters);
				List<ParameterDefinition> m2p = new List<ParameterDefinition> (tMethod.Parameters);

				for (int i = 0; i < m1p.Count; i++) {
					if (!CompareTypes (m1p [i].ParameterType, m2p [i].ParameterType, cache))
						return false;
				}
			}

			if (iMethod.HasGenericParameters) {
				List<GenericParameter> m1p = new List<GenericParameter> (iMethod.GenericParameters);
				List<GenericParameter> m2p = new List<GenericParameter> (tMethod.GenericParameters);

				for (int i = 0; i < m1p.Count; i++)
					if (!CompareTypes (m1p [i], m2p [i], cache))
						return false;
			}

			return true;
		}

		static bool FixAbstractMethods (TypeDefinition type, IMetadataResolver cache, ref MethodDefinition? abstractMethodErrorCtor, Func<AssemblyDefinition?> getMonoAndroidAssembly, Action<string> logMessage)
		{
			if (!type.HasInterfaces)
				return false;

			bool rv = false;
			List<MethodDefinition> typeMethods = new List<MethodDefinition> (type.Methods);
			foreach (var baseType in type.GetBaseTypes (cache))
				typeMethods.AddRange (baseType.Methods);

			foreach (var ifaceInfo in type.Interfaces) {
				var iface    = ifaceInfo.InterfaceType;
				var ifaceDef = cache.Resolve (iface);
				if (ifaceDef == null) {
					logMessage ($"Unable to unresolve interface: {iface.FullName}");
					continue;
				}
				if (ifaceDef.HasGenericParameters)
					continue;

				foreach (var iMethod in ifaceDef.Methods.Where (m => m.IsAbstract)) {
					bool exists = false;

					foreach (var tMethod in typeMethods) {
						if (HaveSameSignature (iface, iMethod, tMethod, cache)) {
							exists = true;
							break;
						}
					}

					if (!exists) {
						abstractMethodErrorCtor ??= GetAbstractMethodErrorConstructor (getMonoAndroidAssembly);
						AddNewExceptionMethod (type, iMethod, abstractMethodErrorCtor, logMessage);
						rv = true;
					}
				}
			}

			return rv;
		}

		static TypeReference TryImportType (TypeDefinition declaringType, TypeReference type)
		{
			if (type.IsGenericParameter)
				return type;

			return declaringType.Module.ImportReference (type);
		}

		static void AddNewExceptionMethod (TypeDefinition type, MethodDefinition method, MethodDefinition abstractMethodErrorCtor, Action<string> logMessage)
		{
			var newMethod = new MethodDefinition (method.Name, (method.Attributes | MethodAttributes.Final) & ~MethodAttributes.Abstract, TryImportType (type, method.ReturnType));

			foreach (var paramater in method.Parameters)
				newMethod.Parameters.Add (new ParameterDefinition (paramater.Name, paramater.Attributes, TryImportType (type, paramater.ParameterType)));

			var ilP = newMethod.Body.GetILProcessor ();

			ilP.Append (ilP.Create (Mono.Cecil.Cil.OpCodes.Newobj, type.Module.ImportReference (abstractMethodErrorCtor)));
			ilP.Append (ilP.Create (Mono.Cecil.Cil.OpCodes.Throw));

			type.Methods.Add (newMethod);

			logMessage ($"Added method: {method} to type: {type.FullName} scope: {type.Scope}");
		}

		static MethodDefinition GetAbstractMethodErrorConstructor (Func<AssemblyDefinition?> getMonoAndroidAssembly)
		{
			var assembly = getMonoAndroidAssembly ();
			if (assembly != null) {
				var errorException = assembly.MainModule.GetType ("Java.Lang.AbstractMethodError");
				if (errorException != null) {
					foreach (var method in errorException.Methods) {
						if (method.Name == ".ctor" && !method.HasParameters) {
							return method;
						}
					}
				}
			}

			throw new Exception ("Unable to find Java.Lang.AbstractMethodError constructor in Mono.Android assembly");
		}
	}
}
