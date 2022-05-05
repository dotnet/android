#if NET

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;


namespace Java.Interop {

	using JniMethodMap = Dictionary<(string Name, string Signature), MethodInfo>;

	public class JreTypeManager : JniRuntime.JniTypeManager {

		public override void RegisterNativeMembers (JniType nativeClass, Type type, ReadOnlySpan<char> methods)
		{
			if (base.TryRegisterNativeMembers (nativeClass, type, methods)) {
				return;
			}

			var toRegister  = new JniMethodMap ();

			AddInterfaceMethods (toRegister, type);
			AddClassMethods (toRegister, type);

			// ignore methods…

			var builder         = Runtime.MarshalMemberBuilder;
			var registrations   = new List<JniNativeMethodRegistration>(toRegister.Count);

			while (!methods.IsEmpty) {
				int newLineIndex = methods.IndexOf ('\n');
				if (newLineIndex < 0) {
					break;
				}
				var line = methods.Slice (0, newLineIndex);
				methods = methods.Slice (newLineIndex+1);
				GetNameAndSignature (line, out var name, out var signature);

				if (!toRegister.TryGetValue ((name, signature), out var method)) {
					continue;
				}
				var marshaler   = builder.CreateMarshalToManagedExpression (method).Compile ();
				if (marshaler == null) {
					throw new NotSupportedException ($"Could not create JNI marshal method for {method!.DeclaringType?.FullName}.{method!.Name}({signature})");
				}
				registrations.Add (new JniNativeMethodRegistration (name, signature, marshaler!));
			}

			nativeClass.RegisterNativeMethods (registrations.ToArray ());
		}

		static void AddInterfaceMethods (JniMethodMap toRegister, Type type)
		{
			foreach (var iface in type.GetInterfaces ()) {
				var ifaceSignature = iface.GetCustomAttribute<JniTypeSignatureAttribute> ();
				if (ifaceSignature == null || string.IsNullOrEmpty (ifaceSignature.SimpleReference)) {
					continue;
				}
				var map = type.GetInterfaceMap (iface);
				for (int i = 0; i < map.InterfaceMethods.Length; ++i) {
					AddJniMethod (toRegister, map.InterfaceMethods [i], map.TargetMethods [i]);
				}
			}
		}

		static void AddJniMethod (JniMethodMap toRegister, MethodInfo declaringMethod, MethodInfo? targetMethod = null)
		{
			var signature = declaringMethod.GetCustomAttribute<JniMethodSignatureAttribute>(inherit: true);
			if (signature == null || string.IsNullOrEmpty (signature.MemberName)) {
				return;
			}
			toRegister [("n_" + signature.MemberName, signature.MemberSignature)]   = targetMethod ?? declaringMethod;
		}

		static void AddClassMethods (JniMethodMap toRegister, Type type)
		{
			const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			foreach (var method in type.GetMethods (Flags)) {
				AddJniMethod (toRegister, method);
			}
			foreach (var property in type.GetProperties (Flags)) {
				var get = property.GetGetMethod ();
				if (get != null) {
					AddJniMethod (toRegister, get);
				}
				var set = property.GetSetMethod ();
				if (set != null) {
					AddJniMethod (toRegister, set);
				}
			}
		}

		static void GetNameAndSignature (ReadOnlySpan<char> line, out string name, out string signature)
		{
			int colon   = line.IndexOf (':');
			name        = new string (line.Slice (0, colon));
			line        = line.Slice (colon+1);
			colon       = line.IndexOf (':');
			signature   = new string (line.Slice (0, colon));
		}
	}
}

#endif  // NET
