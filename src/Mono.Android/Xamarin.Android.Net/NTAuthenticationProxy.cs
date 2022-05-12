using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Runtime;
using System.Security.Authentication.ExtendedProtection;

namespace Xamarin.Android.Net
{
	internal sealed class NTAuthenticationProxy
	{
		const string AssemblyName = "System.Net.Http";
		const string TypeName = "System.Net.NTAuthentication";
		const string ContextFlagsPalTypeName = "System.Net.ContextFlagsPal";

		const string ConstructorDescription = "#ctor(System.Boolean,System.String,System.Net.NetworkCredential,System.String,System.Net.ContextFlagsPal,System.Security.Authentication.ExtendedProtection.ChannelBinding)";
		const string IsCompletedPropertyName = "IsCompleted";
		const string GetOutgoingBlobMethodName = "GetOutgoingBlob";
		const string CloseContextMethodName = "CloseContext";

		const BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		static Lazy<Type> s_NTAuthenticationType = new (() => FindType (TypeName, AssemblyName));
		static Lazy<ConstructorInfo> s_NTAuthenticationConstructorInfo = new (() => GetNTAuthenticationConstructor ());
		static Lazy<PropertyInfo> s_IsCompletedPropertyInfo = new (() => GetProperty (IsCompletedPropertyName));
		static Lazy<MethodInfo> s_GetOutgoingBlobMethodInfo = new (() => GetMethod (GetOutgoingBlobMethodName));
		static Lazy<MethodInfo> s_CloseContextMethodInfo = new (() => GetMethod (CloseContextMethodName));

		static Type FindType (string typeName, string assemblyName)
			=> Type.GetType ($"{typeName}, {assemblyName}", throwOnError: true)!;

		static ConstructorInfo GetNTAuthenticationConstructor ()
		{
			var contextFlagsPalType = FindType (ContextFlagsPalTypeName, AssemblyName);
			var paramTypes = new[] {
				typeof (bool),
				typeof (string),
				typeof (NetworkCredential),
				typeof (string),
				contextFlagsPalType,
				typeof (ChannelBinding)
			};

			return s_NTAuthenticationType.Value.GetConstructor (InstanceBindingFlags, paramTypes)
				?? throw new MissingMemberException (TypeName, ConstructorInfo.ConstructorName);
		}

		static PropertyInfo GetProperty (string name)
			=> s_NTAuthenticationType.Value.GetProperty (name, InstanceBindingFlags)
				?? throw new MissingMemberException (TypeName, name);

		static MethodInfo GetMethod (string name)
			=> s_NTAuthenticationType.Value.GetMethod (name, InstanceBindingFlags)
				?? throw new MissingMemberException (TypeName, name);

		object _instance;

		[DynamicDependency (ConstructorDescription, TypeName, AssemblyName)]
		internal NTAuthenticationProxy (
			bool isServer,
			string package,
			NetworkCredential credential,
			string? spn,
			int requestedContextFlags,
			ChannelBinding? channelBinding)
		{
			var constructorParams = new object?[] { isServer, package, credential, spn, requestedContextFlags, channelBinding };
			_instance = s_NTAuthenticationConstructorInfo.Value.Invoke (constructorParams);
		}

		public bool IsCompleted
			=> GetIsCompleted ();

		[DynamicDependency ($"get_{IsCompletedPropertyName}", TypeName, AssemblyName)]
		bool GetIsCompleted ()
			=> (bool)s_IsCompletedPropertyInfo.Value.GetValue (_instance);

		[DynamicDependency (GetOutgoingBlobMethodName, TypeName, AssemblyName)]
		public string? GetOutgoingBlob (string? incomingBlob)
			=> (string?)s_GetOutgoingBlobMethodInfo.Value.Invoke (_instance, new object?[] { incomingBlob });

		[DynamicDependency (CloseContextMethodName, TypeName, AssemblyName)]
		public void CloseContext ()
			=> s_CloseContextMethodInfo.Value.Invoke (_instance, null);
	}
}
