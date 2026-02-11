using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Android.Build.TypeMap;

/// <summary>
/// Builds a <see cref="TypeMapAssemblyData"/> from scanned <see cref="JavaPeerInfo"/> records.
/// All decision logic (deduplication, ACW detection, callback resolution, proxy naming) lives here.
/// The output model is a plain data structure that the emitter writes directly into a PE assembly.
/// </summary>
sealed class ModelBuilder
{
	/// <summary>
	/// Builds a TypeMap assembly model for the given peers.
	/// </summary>
	/// <param name="peers">Scanned Java peer types (typically from a single input assembly).</param>
	/// <param name="outputPath">Output .dll path — used to derive assembly/module names if not specified.</param>
	/// <param name="assemblyName">Explicit assembly name. If null, derived from <paramref name="outputPath"/>.</param>
	public TypeMapAssemblyData Build (IReadOnlyList<JavaPeerInfo> peers, string outputPath, string? assemblyName = null)
	{
		if (peers is null) {
			throw new ArgumentNullException (nameof (peers));
		}
		if (outputPath is null) {
			throw new ArgumentNullException (nameof (outputPath));
		}

		assemblyName ??= Path.GetFileNameWithoutExtension (outputPath);
		string moduleName = Path.GetFileName (outputPath);

		var model = new TypeMapAssemblyData {
			AssemblyName = assemblyName,
			ModuleName = moduleName,
		};

		var seenJniNames = new HashSet<string> (StringComparer.Ordinal);

		foreach (var peer in peers) {
			if (!seenJniNames.Add (peer.JavaName)) {
				continue;
			}

			bool hasProxy = peer.ActivationCtor != null || peer.InvokerTypeName != null;
			bool isAcw = !peer.DoNotGenerateAcw && !peer.IsInterface && peer.MarshalMethods.Count > 0;

			JavaPeerProxyData? proxy = null;
			if (hasProxy) {
				proxy = BuildProxyType (peer, isAcw);
				model.ProxyTypes.Add (proxy);
			}

			model.Entries.Add (BuildEntry (peer, proxy, assemblyName));
		}

		return model;
	}

	static JavaPeerProxyData BuildProxyType (JavaPeerInfo peer, bool isAcw)
	{
		var proxyTypeName = peer.JavaName.Replace ('/', '_').Replace ('$', '_') + "_Proxy";

		var proxy = new JavaPeerProxyData {
			TypeName = proxyTypeName,
			TargetType = new TypeRefData {
				ManagedTypeName = peer.ManagedTypeName,
				AssemblyName = peer.AssemblyName,
			},
			HasActivation = peer.ActivationCtor != null,
			IsAcw = isAcw,
		};

		if (peer.InvokerTypeName != null) {
			proxy.InvokerType = new TypeRefData {
				ManagedTypeName = peer.InvokerTypeName,
				AssemblyName = peer.AssemblyName,
			};
		}

		if (isAcw) {
			BuildUcoMethods (peer, proxy);
			BuildUcoConstructors (peer, proxy);
			BuildNativeRegistrations (proxy);
		}

		return proxy;
	}

	static void BuildUcoMethods (JavaPeerInfo peer, JavaPeerProxyData proxy)
	{
		int ucoIndex = 0;
		for (int i = 0; i < peer.MarshalMethods.Count; i++) {
			var mm = peer.MarshalMethods [i];
			if (mm.IsConstructor) {
				continue;
			}

			proxy.UcoMethods.Add (new UcoMethodData {
				WrapperName = $"n_{mm.JniName}_uco_{ucoIndex}",
				CallbackMethodName = mm.NativeCallbackName,
				CallbackType = new TypeRefData {
					ManagedTypeName = !string.IsNullOrEmpty (mm.DeclaringTypeName) ? mm.DeclaringTypeName : peer.ManagedTypeName,
					AssemblyName = !string.IsNullOrEmpty (mm.DeclaringAssemblyName) ? mm.DeclaringAssemblyName : peer.AssemblyName,
				},
				JniSignature = mm.JniSignature,
			});
			ucoIndex++;
		}
	}

	static void BuildUcoConstructors (JavaPeerInfo peer, JavaPeerProxyData proxy)
	{
		if (peer.ActivationCtor == null || peer.JavaConstructors.Count == 0) {
			return;
		}

		foreach (var ctor in peer.JavaConstructors) {
			proxy.UcoConstructors.Add (new UcoConstructorData {
				WrapperName = $"nctor_{ctor.ConstructorIndex}_uco",
				TargetType = new TypeRefData {
					ManagedTypeName = peer.ManagedTypeName,
					AssemblyName = peer.AssemblyName,
				},
			});
		}
	}

	static void BuildNativeRegistrations (JavaPeerProxyData proxy)
	{
		foreach (var uco in proxy.UcoMethods) {
			// The JNI method name registered is the n_* callback name (e.g., "n_onCreate")
			// but we need the Java-side native method name which matches the callback name
			proxy.NativeRegistrations.Add (new NativeRegistrationData {
				JniMethodName = uco.CallbackMethodName,
				JniSignature = uco.JniSignature,
				WrapperMethodName = uco.WrapperName,
			});
		}

		foreach (var uco in proxy.UcoConstructors) {
			// Constructor wrapper name is "nctor_N_uco", JNI name is "nctor_N"
			string jniName = uco.WrapperName;
			int ucoSuffix = jniName.LastIndexOf ("_uco", StringComparison.Ordinal);
			if (ucoSuffix >= 0) {
				jniName = jniName.Substring (0, ucoSuffix);
			}

			proxy.NativeRegistrations.Add (new NativeRegistrationData {
				JniMethodName = jniName,
				// Constructor UCO wrappers have a fixed (IntPtr, IntPtr) signature — the JNI
				// signature for registration is the Java constructor's JNI signature.
				// For now, use "()V" as placeholder — the actual ctor signature is resolved at emit time.
				JniSignature = "()V",
				WrapperMethodName = uco.WrapperName,
			});
		}
	}

	static TypeMapAttributeData BuildEntry (JavaPeerInfo peer, JavaPeerProxyData? proxy, string outputAssemblyName)
	{
		string typeRef;
		if (proxy != null) {
			typeRef = $"{proxy.Namespace}.{proxy.TypeName}, {outputAssemblyName}";
		} else {
			typeRef = $"{peer.ManagedTypeName}, {peer.AssemblyName}";
		}

		return new TypeMapAttributeData {
			JniName = peer.JavaName,
			TypeReference = typeRef,
		};
	}
}
