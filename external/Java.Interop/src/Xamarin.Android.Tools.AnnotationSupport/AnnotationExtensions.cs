using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public static class AnnotationExtensions
	{
		public static string FormatMember (this AnnotatedItem a)
		{
			return string.Format ("  [{0}] {1} {2} {3}",
			                      a.Arguments == null ? "F" : a.ParameterIndex >= 0 ? ("P" + a.ParameterIndex) : a.MemberName == "#ctor" ? "C" : "M",
			                      a.MemberType,
			                      a.MemberName,
			                      a.Arguments == null ? null : "(" + string.Join (", ", a.Arguments) + ")");
		}

		#region annotated member retrieval

		public static IEnumerable<AnnotationData> Data (this IEnumerable<AnnotatedItem> anns)
		{
			return anns.SelectMany (a => a.Annotations);
		}

		public static IEnumerable<AnnotatedItem> GetAnnotations (this  AndroidAnnotationsSupport api, ManagedApiQuery query)
		{
			if (query == null)
				throw new ArgumentNullException (nameof (query));
			if (query.TypeName == null)
				throw new ArgumentNullException ("TypeName must not be null");

			IList<AnnotatedItem> l;
			var qt = api.Data.TryGetValue (query.TypeName, out l) ? l : new AnnotatedItem [0];

			if (query.MemberName == null) // type
				return qt;
			
			var qmbr = qt.Where (a => a.ManagedInfo.MemberName == query.MemberName);
			if (query.Arguments == null) // field, property (from managed signature), or method match without arguments.
				return qmbr.Concat (qt.Where (a => IsProperty (a, query.MemberName)));
			
			var qmth = qmbr.Where (a => AreArgumentsEqual (a.ManagedInfo.Arguments, query.Arguments));
			// method (idx < 0) or parameter (idx >= 0)
			return qmth.Where (a => a.ParameterIndex == query.ParameterIndex || a.ParameterIndex < 0 && query.ParameterIndex < 0);
		}

		public static IEnumerable<AnnotatedItem> GetAnnotations (this AndroidAnnotationsSupport api, string managedTypeName)
		{
			return api.GetAnnotations (new ManagedApiQuery { TypeName = managedTypeName });
		}

		public static IEnumerable<AnnotatedItem> GetAnnotations (this AndroidAnnotationsSupport api, string managedTypeName, string managedMemberName)
		{
			return api.GetAnnotations (new ManagedApiQuery { TypeName = managedTypeName, MemberName = managedMemberName });
		}

		public static IEnumerable<AnnotatedItem> GetAnnotations (this AndroidAnnotationsSupport api, string managedTypeName, string managedMethodName, string [] parameterTypes)
		{
			return api.GetAnnotations (new ManagedApiQuery { TypeName = managedTypeName, MemberName = managedMethodName, Arguments = parameterTypes });
		}

		public static IEnumerable<AnnotatedItem> GetAnnotations (this AndroidAnnotationsSupport api, string managedTypeName, string managedMethodName, string [] parameterTypes, int parameterIndex)
		{
			return api.GetAnnotations (new ManagedApiQuery { TypeName = managedTypeName, MemberName = managedMethodName, Arguments = parameterTypes, ParameterIndex = parameterIndex });
		}

		static bool IsProperty (AnnotatedItem item, string propertyName)
		{
			if (item.Arguments == null)
				return false;
			return item.ManagedInfo.MemberName == "get_" + propertyName && item.Arguments.Length == 0
				|| item.ManagedInfo.MemberName == "set_" + propertyName && item.Arguments.Length == 1;
		}

		[Obsolete ("Use GetAnnotations() overload.")]
		public static IEnumerable<AnnotatedItem> GetMethodsAnnotations (this AndroidAnnotationsSupport api, string managedTypeName, string managedMethodName)
		{
			if (managedMethodName == null)
				throw new ArgumentNullException (nameof (managedMethodName));
			return GetAnnotations (api, managedTypeName, managedMethodName);
		}

		[Obsolete ("Use GetAnnotations() overload.")]
		public static IEnumerable<AnnotatedItem> GetFieldAnnotations (this AndroidAnnotationsSupport api, string managedTypeName, string managedPropertyName)
		{
			if (managedPropertyName == null)
				throw new ArgumentNullException (nameof (managedPropertyName));
			return GetAnnotations (api, managedTypeName, managedPropertyName);
		}

		[Obsolete ("Use GetAnnotations() overload.")]
		public static IEnumerable<AnnotatedItem> GetMethodAnnotations (this AndroidAnnotationsSupport api, string managedTypeName, string managedMethodName, string [] managedParameterTypes)
		{
			if (managedMethodName == null)
				throw new ArgumentNullException (nameof (managedMethodName));
			if (managedParameterTypes == null)
				throw new ArgumentNullException (nameof (managedParameterTypes));
			return GetAnnotations (api, managedTypeName, managedMethodName, managedParameterTypes);
		}

		[Obsolete ("Use GetAnnotations() overload.")]
		public static IEnumerable<AnnotatedItem> GetParameterAnnotations (this AndroidAnnotationsSupport api, string managedTypeName, string managedMethodName, string [] managedParameterTypes, int parameterIndex)
		{
			if (managedMethodName == null)
				throw new ArgumentNullException (nameof (managedMethodName));
			if (managedParameterTypes == null)
				throw new ArgumentNullException (nameof (managedParameterTypes));
			return GetAnnotations (api, managedTypeName, managedMethodName, managedParameterTypes, parameterIndex);
		}

		static bool AreArgumentsEqual (TypeName [] definedArguments, IList<string> queriedArguments)
		{
			if (definedArguments.Length != queriedArguments.Count)
				return false;
			return definedArguments.Zip (queriedArguments, (t, s) => t.FullName == s).All (b => b);
		}

		#endregion

		#region Constant definition analysys

		static Func<AnnotationData, bool> is_intdef = a => a.Name == "IntDef";
		static Func<AnnotationData, bool> is_stringdef = a => a.Name == "StringDef";

		public static AnnotationData GetIntDef (this AndroidAnnotationsSupport api, ManagedApiQuery query)
		{
			return api.GetAnnotations (query).Data ().FirstOrDefault (is_intdef);
		}

		[Obsolete ("Use GetIntDef(ManagedApiQuery)")]
		public static AnnotationData GetFieldIntDef (this AndroidAnnotationsSupport api, string managedTypeName, string managedPropertyName)
		{
			if (managedPropertyName == null)
				throw new ArgumentNullException (nameof (managedPropertyName));
			return api.GetAnnotations (managedTypeName, managedPropertyName).Data ().FirstOrDefault (is_intdef);
		}

		// This is nothing but shortcut to GetMethodReturnIntDef(..., "get_" + propName ?? "set_" + propName)
		// Won't work if the getter and the setter have inconsistent parameter/return types.
		[Obsolete ("Use GetIntDef(ManagedApiQuery)")]
		public static AnnotationData GetPropertyIntDef (this AndroidAnnotationsSupport api, string managedTypeName, string managedPropertyName)
		{
			if (managedPropertyName == null)
				throw new ArgumentNullException (nameof (managedPropertyName));
			return api.GetAnnotations (managedTypeName, managedPropertyName).Data ().FirstOrDefault (is_intdef);
		}

		[Obsolete ("Use GetIntDef(ManagedApiQuery)")]
		public static AnnotationData GetMethodReturnIntDef (this AndroidAnnotationsSupport api, string managedTypeName, string managedMethodName, string [] managedParameterTypes)
		{
			if (managedMethodName == null)
				throw new ArgumentNullException (nameof (managedMethodName));
			if (managedParameterTypes == null)
				throw new ArgumentNullException (nameof (managedParameterTypes));
			return api.GetAnnotations (managedTypeName, managedMethodName, managedParameterTypes).Data ().FirstOrDefault (is_intdef);
		}

		[Obsolete ("Use GetIntDef(ManagedApiQuery)")]
		public static AnnotationData GetMethodParameterIntDef (this AndroidAnnotationsSupport api, string managedTypeName, string managedMethodName, string [] managedParameterTypes, int parameterIndex)
		{
			if (managedMethodName == null)
				throw new ArgumentNullException (nameof (managedMethodName));
			if (managedParameterTypes == null)
				throw new ArgumentNullException (nameof (managedParameterTypes));
			return api.GetAnnotations (managedTypeName, managedMethodName, managedParameterTypes, parameterIndex).Data ().FirstOrDefault (is_intdef);
		}

		public static IList<ManagedMemberInfo> AsCompletionCandidates (this AnnotationData a)
		{
			return a == null ? null : a.GetExtension<ConstantDefinitionExtension> ().ManagedConstants;
		}

		public static bool IsAlreadyEnumified (this AnnotationData a)
		{
			var ext = a == null ? null : a.GetExtension<ConstantDefinitionExtension> ();
			return ext != null && ext.IsTargetAlreadyEnumified;
		}

		public static AnnotationData IntDef (this IEnumerable<AnnotationData> anns)
		{
			return anns.FirstOrDefault (is_intdef);
		}

		public static AnnotationData StringDef (this IEnumerable<AnnotationData> anns)
		{
			return anns.FirstOrDefault (is_stringdef);
		}

		#endregion

		#region permission requirements retrieval

		public static IEnumerable<string> GetRequiredPermissions (this IEnumerable<AnnotatedItem> items)
		{
			return items.Select (item => item.GetExtension<RequiresPermissionExtension> ())
				.Where (x => x != null)
				.SelectMany (x => x.Values);
		}

		#endregion
	}
}

