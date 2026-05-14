using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using K4os.Compression.LZ4;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.AssemblyStore;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Category ("Node-2")]
	public class TrimmableTypeMapBuildTests : BaseTest {
		const uint CompressedAssemblyMagic = 0x5A4C4158; // 'XALZ', little-endian

		[Test]
		public void Build_WithTrimmableTypeMap_Succeeds ([Values] bool isRelease, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			DirectoryAssert.Exists (intermediateDir);
		}

		[Test]
		public void Build_WithTrimmableTypeMap_IncrementalBuild ([Values] bool isRelease, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			DirectoryAssert.Exists (intermediateDir);

			Assert.IsTrue (builder.Build (proj), "Second build should have succeeded.");

			Assert.IsTrue (
				builder.Output.IsTargetSkipped ("_GenerateJavaStubs"),
				"_GenerateJavaStubs should be skipped on incremental build.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_DoesNotHitCopyIfChangedMismatch ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			Assert.IsFalse (
				StringAssertEx.ContainsText (builder.LastBuildOutput, "source and destination count mismatch"),
				$"{builder.BuildLogFile} should not fail with XACIC7004.");
			Assert.IsFalse (
				StringAssertEx.ContainsText (builder.LastBuildOutput, "Internal error: architecture"),
				$"{builder.BuildLogFile} should keep trimmable typemap assemblies aligned across ABIs.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_AssemblyStoreMappingsStayInRange ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var environmentFiles = Directory.GetFiles (builder.Output.GetIntermediaryPath ("android"), "environment.*.ll");
			Assert.IsNotEmpty (environmentFiles, "Expected generated environment.<abi>.ll files.");

			foreach (var environmentFile in environmentFiles) {
				var abi = Path.GetFileNameWithoutExtension (environmentFile).Substring ("environment.".Length);
				var manifestFile = builder.Output.GetIntermediaryPath (Path.Combine ("app_shared_libraries", abi, "assembly-store.so.manifest"));

				if (!File.Exists (manifestFile)) {
					continue;
				}

				var environmentText = File.ReadAllText (environmentFile);
				var runtimeDataMatch = Regex.Match (environmentText, @"assembly_store_bundled_assemblies.*\[(\d+)\s+x");
				Assert.IsTrue (runtimeDataMatch.Success, $"{environmentFile} should declare assembly_store_bundled_assemblies.");

				var runtimeDataCount = int.Parse (runtimeDataMatch.Groups [1].Value);
				var maxMappingIndex = File.ReadLines (manifestFile)
					.Select (line => Regex.Match (line, @"\bmi:(\d+)\b"))
					.Where (match => match.Success)
					.Select (match => int.Parse (match.Groups [1].Value))
					.Max ();

				Assert.That (
					runtimeDataCount,
					Is.GreaterThan (maxMappingIndex),
					$"{Path.GetFileName (environmentFile)} should allocate enough runtime slots for {Path.GetFileName (manifestFile)}.");
			}
		}

		[Test]
		public void ReleaseCoreClrTrimmableTypeMap_DoesNotKeepMoreTypemapPeersThanLlvmIr ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var llvmIr = BuildTypemapComparisonApk ("llvm-ir");
			var trimmable = BuildTypemapComparisonApk ("trimmable");

			WriteComparisonDiagnostics (llvmIr, trimmable);

			Assert.IsEmpty (
				trimmable.ManagedTypemapEntries.Except (llvmIr.ManagedTypemapEntries, StringComparer.Ordinal).OrderBy (x => x, StringComparer.Ordinal).ToArray (),
				"Trimmable typemap should not keep additional managed typemap-eligible types or methods compared to llvm-ir.");
			Assert.IsEmpty (
				trimmable.JavaTypemapEntries.Except (llvmIr.JavaTypemapEntries, StringComparer.Ordinal).OrderBy (x => x, StringComparer.Ordinal).ToArray (),
				"Trimmable typemap should not keep additional Java typemap-eligible classes or methods compared to llvm-ir.");
		}

		[Test]
		public void TrimmableTypeMap_PreserveLists_ArePackagedInSdk ()
		{
			foreach (var file in new [] {
				"Trimmable.CoreCLR.xml",
				"System.Private.CoreLib.xml",
			}) {
				var path = Path.Combine (TestEnvironment.DotNetPreviewAndroidSdkDirectory, "PreserveLists", file);
				FileAssert.Exists (path, $"{path} should exist in the SDK pack.");
			}
		}

		[Test]
		public void TrimmableTypeMap_RuntimeArtifacts_ArePackagedInSdk ()
		{
			var toolsDir = TestEnvironment.AndroidMSBuildDirectory;

			foreach (var file in new [] {
				"java_runtime.jar",
				"java_runtime.dex",
				"java_runtime_fastdev.jar",
				"java_runtime_fastdev.dex",
				"java_runtime_trimmable.jar",
				"java_runtime_trimmable.dex",
				"java_runtime_clr.jar",
				"java_runtime_clr.dex",
				"java_runtime_fastdev_clr.jar",
				"java_runtime_fastdev_clr.dex",
			}) {
				FileAssert.Exists (Path.Combine (toolsDir, file), $"{file} should exist in the SDK pack.");
			}

		}

		ApkComparisonProfile BuildTypemapComparisonApk (string typemapImplementation)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				PackageName = "com.xamarin.typemapcomparison",
				ProjectName = "TypemapComparison",
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidSupportedAbis", "arm64-v8a");
			proj.SetProperty ("AndroidPackageFormat", "apk");
			proj.SetProperty (KnownProperties.AndroidLinkTool, "r8");
			proj.SetProperty ("TrimMode", "full");
			proj.SetProperty ("_AndroidTypeMapImplementation", typemapImplementation);

			using var builder = CreateApkBuilder (Path.Combine ("temp", $"TypemapComparison_{typemapImplementation}_{Guid.NewGuid ():N}"));
			Assert.IsTrue (builder.Build (proj), $"{typemapImplementation} build should have succeeded.");

			if (typemapImplementation != "trimmable") {
				FileAssert.Exists (builder.Output.GetIntermediaryPath (Path.Combine ("android", "typemaps.arm64-v8a.ll")), "llvm-ir build should generate the native typemap.");
				FileAssert.Exists (builder.Output.GetIntermediaryPath (Path.Combine ("android", "typemaps.arm64-v8a.o")), "llvm-ir build should compile the native typemap.");
			}

			var apkDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath);
			var apkPath = Directory.GetFiles (apkDirectory, "*-Signed.apk", SearchOption.AllDirectories).Single ();
			var acwMapPath = builder.Output.GetIntermediaryPath ("acw-map.txt");
			var javaSourceDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android", "src"));
			var typeMapDirectory = builder.Output.GetIntermediaryPath ("typemap");
			var linkedAssemblyDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "linked"));

			var profile = ReadApkProfile (typemapImplementation, apkPath, acwMapPath, javaSourceDirectory, typeMapDirectory, linkedAssemblyDirectory);
			if (typemapImplementation == "trimmable") {
				Assert.IsTrue (profile.ManagedAssemblyNames.Contains ("_Microsoft.Android.TypeMaps.dll"), "trimmable build should package the root managed typemap assembly.");
			} else {
				Assert.IsFalse (profile.ManagedAssemblyNames.Contains ("_Microsoft.Android.TypeMaps.dll"), "llvm-ir build should not package the trimmable root managed typemap assembly.");
			}
			return profile;
		}

		ApkComparisonProfile ReadApkProfile (string name, string apkPath, string acwMapPath, string javaSourceDirectory, string typeMapDirectory, string linkedAssemblyDirectory)
		{
			var profile = new ApkComparisonProfile {
				Name = name,
				ApkPath = apkPath,
				ApkSize = new FileInfo (apkPath).Length,
			};

			LoadAcwMap (acwMapPath, profile);
			ReadGeneratedJavaProfile (javaSourceDirectory, profile);
			ReadTypeMapAssemblyProfile (profile, "generated", typeMapDirectory);
			ReadTypeMapAssemblyProfile (profile, "linked", linkedAssemblyDirectory);
			ReadAssemblyStoreProfile (profile);
			ReadDexProfile (profile);

			return profile;
		}

		void ReadGeneratedJavaProfile (string javaSourceDirectory, ApkComparisonProfile profile)
		{
			if (!Directory.Exists (javaSourceDirectory)) {
				return;
			}

			foreach (var file in Directory.EnumerateFiles (javaSourceDirectory, "*.java", SearchOption.AllDirectories)) {
				profile.GeneratedJavaSourceCount++;
				var text = File.ReadAllText (file);
				if (text.IndexOf ("__md_methods", StringComparison.Ordinal) >= 0) {
					profile.GeneratedJavaWithMdMethodsCount++;
				}
				if (text.IndexOf ("Runtime.register (", StringComparison.Ordinal) >= 0) {
					profile.GeneratedJavaWithRuntimeRegisterCount++;
				}
				if (text.IndexOf ("Runtime.registerNatives", StringComparison.Ordinal) >= 0) {
					profile.GeneratedJavaWithRegisterNativesCount++;
				}
			}
		}

		void LoadAcwMap (string acwMapPath, ApkComparisonProfile profile)
		{
			FileAssert.Exists (acwMapPath, $"{profile.Name} build should produce acw-map.txt.");

			foreach (var line in File.ReadLines (acwMapPath)) {
				if (line.Length == 0 || line [0] == '#') {
					continue;
				}

				var fields = line.Split (new [] { ';' }, 2);
				if (fields.Length != 2) {
					continue;
				}

				var javaName = fields [1].Trim ();
				if (!IsTypemapHelperJavaType (javaName)) {
					profile.CandidateJavaNames.Add (javaName);
				}

				var managedType = GetManagedTypeFromAcwMapKey (fields [0]);
				if (managedType != null && !IsTypemapHelperManagedType (managedType)) {
					profile.CandidateManagedTypes.Add (managedType);
				}
			}
		}

		string GetManagedTypeFromAcwMapKey (string key)
		{
			var comma = key.IndexOf (',');
			if (comma < 0) {
				return null;
			}

			var typeName = key.Substring (0, comma).Trim ();
			if (typeName.Length == 0 || typeName.IndexOf ('/') >= 0) {
				return null;
			}

			return typeName.Replace ('+', '/');
		}

		void ReadAssemblyStoreProfile (ApkComparisonProfile profile)
		{
			(var explorers, var errorMessage) = AssemblyStoreExplorer.Open (profile.ApkPath);
			Assert.IsNull (errorMessage, $"{profile.ApkPath} should contain readable assembly stores.");
			Assert.IsNotNull (explorers, $"{profile.ApkPath} should contain assembly stores.");

			var explorer = explorers.FirstOrDefault (e => e.TargetArch == AndroidTargetArch.Arm64);
			Assert.IsNotNull (explorer, $"{profile.ApkPath} should contain an arm64-v8a assembly store.");

			profile.AssemblyStoreCount = explorers.Count;
			foreach (var store in explorers) {
				var storeSize = store.Assemblies?.Where (a => !a.Ignore).Sum (a => (long)a.DataSize) ?? 0;
				profile.AssemblyStores.Add ($"{store.TargetArch}: assemblies={store.AssemblyCount}, indexed={store.IndexEntryCount}, size={storeSize}");
				profile.AssemblyStoreSize += storeSize;
			}

			foreach (var item in explorer.Assemblies.Where (a => !a.Ignore && a.Name.EndsWith (".dll", StringComparison.OrdinalIgnoreCase) && !a.Name.EndsWith (".ni.dll", StringComparison.OrdinalIgnoreCase))) {
				profile.ManagedAssemblyNames.Add (item.Name);
				using var stream = explorer.ReadImageData (item);
				if (stream == null) {
					continue;
				}

				using var assemblyStream = GetManagedAssemblyStream (stream, item.Name);
				AssemblyDefinition assembly;
				try {
					assembly = AssemblyDefinition.ReadAssembly (assemblyStream);
				} catch (BadImageFormatException ex) {
					Assert.Fail ($"Assembly store entry '{item.Name}' should contain a readable managed assembly: {ex.Message}. First bytes: {ReadFirstBytes (assemblyStream)}");
					throw;
				}
				using (assembly) {
					profile.ManagedAssemblyCount++;
					if (item.Name.EndsWith (".TypeMap.dll", StringComparison.Ordinal) || item.Name == "_Microsoft.Android.TypeMaps.dll") {
						ReadTypeMapAssemblyProfile (profile, "packaged", assembly, item.Name);
					}
					foreach (var type in assembly.Modules.SelectMany (m => m.Types).SelectMany (FlattenType)) {
						if (IsTypemapHelperManagedType (type.FullName)) {
							continue;
						}

						profile.RawManagedTypeCount++;
						profile.RawManagedMethodCount += type.Methods.Count;

						if (!IsManagedTypemapEligible (type, profile)) {
							continue;
						}

						profile.FilteredManagedTypeCount++;
						profile.FilteredManagedMethodCount += type.Methods.Count;
						profile.ManagedTypemapEntries.Add ($"type {type.FullName}");
						foreach (var method in type.Methods) {
							profile.ManagedTypemapEntries.Add ($"method {type.FullName}::{GetManagedMethodSignature (method)}");
						}
					}
				}
			}
		}

		void ReadTypeMapAssemblyProfile (ApkComparisonProfile profile, string stage, string directory)
		{
			if (!Directory.Exists (directory)) {
				return;
			}

			foreach (var file in Directory.EnumerateFiles (directory, "*.dll", SearchOption.TopDirectoryOnly).Where (IsTypeMapAssemblyPath)) {
				using var assembly = AssemblyDefinition.ReadAssembly (file);
				ReadTypeMapAssemblyProfile (profile, stage, assembly, Path.GetFileName (file));
			}
		}

		bool IsTypeMapAssemblyPath (string file)
		{
			var name = Path.GetFileName (file);
			return name.EndsWith (".TypeMap.dll", StringComparison.Ordinal) || name == "_Microsoft.Android.TypeMaps.dll";
		}

		void ReadTypeMapAssemblyProfile (ApkComparisonProfile profile, string stage, AssemblyDefinition assembly, string assemblyName)
		{
			var metrics = new TypeMapAssemblyMetrics {
				Stage = stage,
				AssemblyName = assemblyName,
			};

			foreach (var attribute in assembly.CustomAttributes) {
				var attributeName = attribute.AttributeType.FullName;
				if (attributeName.StartsWith ("System.Runtime.InteropServices.TypeMapAttribute`1", StringComparison.Ordinal)) {
					ReadTypeMapAttribute (attribute, metrics);
				} else if (attributeName.StartsWith ("System.Runtime.InteropServices.TypeMapAssociationAttribute", StringComparison.Ordinal)) {
					metrics.AssociationAttributeCount++;
				} else if (attributeName.StartsWith ("System.Runtime.InteropServices.TypeMapAssemblyTargetAttribute`1", StringComparison.Ordinal)) {
					metrics.AssemblyTargetAttributeCount++;
				}
			}

			if (metrics.TypeMapAttributeCount != 0 || metrics.AssociationAttributeCount != 0 || metrics.AssemblyTargetAttributeCount != 0) {
				profile.TypeMapAssemblies.Add (metrics);
			}
		}

		void ReadTypeMapAttribute (CustomAttribute attribute, TypeMapAssemblyMetrics metrics)
		{
			metrics.TypeMapAttributeCount++;
			if (attribute.ConstructorArguments.Count == 2) {
				metrics.UnconditionalTypeMapAttributeCount++;
			} else if (attribute.ConstructorArguments.Count == 3) {
				metrics.ConditionalTypeMapAttributeCount++;
			}

			var jniName = attribute.ConstructorArguments.Count > 0 ? attribute.ConstructorArguments [0].Value as string : null;
			var proxyType = attribute.ConstructorArguments.Count > 1 ? attribute.ConstructorArguments [1].Value as string : null;
			var targetType = attribute.ConstructorArguments.Count > 2 ? attribute.ConstructorArguments [2].Value as string : null;
			var key = $"{jniName}\t{proxyType}\t{targetType}";
			metrics.TypeMapAttributeKeys.Add (key);
			if (jniName != null) {
				metrics.IncrementPrefixBucket (jniName);
			}
		}

		bool IsManagedTypemapEligible (TypeDefinition type, ApkComparisonProfile profile)
		{
			if (profile.CandidateManagedTypes.Contains (type.FullName)) {
				return true;
			}

			var isTypemapEligible = false;
			foreach (var attribute in type.CustomAttributes) {
				var attributeName = attribute.AttributeType.FullName;
				if (attributeName != "Android.Runtime.RegisterAttribute" && attributeName != "Java.Interop.JniTypeSignatureAttribute") {
					continue;
				}

				isTypemapEligible = true;
				if (attribute.ConstructorArguments.Count > 0 && attribute.ConstructorArguments [0].Value is string jniName) {
					jniName = NormalizeJniName (jniName);
					if (!IsTypemapHelperJavaType (jniName)) {
						profile.CandidateJavaNames.Add (jniName);
					}
				}
			}

			return isTypemapEligible;
		}

		string GetManagedMethodSignature (MethodDefinition method)
		{
			var parameters = String.Join (",", method.Parameters.Select (p => p.ParameterType.FullName));
			return $"{method.Name}({parameters}):{method.ReturnType.FullName}";
		}

		string NormalizeJniName (string jniName)
		{
			if (jniName.Length >= 2 && jniName [0] == 'L' && jniName [jniName.Length - 1] == ';') {
				return jniName.Substring (1, jniName.Length - 2);
			}

			return jniName;
		}

		string ReadFirstBytes (Stream stream)
		{
			var position = stream.Position;
			stream.Seek (0, SeekOrigin.Begin);
			var bytes = new byte [Math.Min (16, stream.Length)];
			stream.ReadExactly (bytes, 0, bytes.Length);
			stream.Seek (position, SeekOrigin.Begin);
			return BitConverter.ToString (bytes);
		}

		Stream GetManagedAssemblyStream (Stream stream, string name)
		{
			(ulong elfPayloadOffset, ulong elfPayloadSize, var error) = Xamarin.Android.AssemblyStore.Utils.FindELFPayloadSectionOffsetAndSize (stream);
			Assert.IsTrue (
				error == ELFPayloadError.None || error == ELFPayloadError.NotELF,
				$"{name} should be a managed assembly or an ELF image containing one. ELF payload error: {error}");

			if (elfPayloadOffset == 0) {
				stream.Seek (0, SeekOrigin.Begin);
				var copy = new MemoryStream ();
				stream.CopyTo (copy);
				copy.Seek (0, SeekOrigin.Begin);
				return DecompressAssemblyIfNeeded (copy);
			}

			var payload = new MemoryStream ();
			var buffer = new byte [16 * 1024];
			var remaining = elfPayloadSize;
			stream.Seek ((long)elfPayloadOffset, SeekOrigin.Begin);
			while (remaining > 0) {
				var read = stream.Read (buffer, 0, (int)Math.Min ((ulong)buffer.Length, remaining));
				if (read == 0) {
					break;
				}
				payload.Write (buffer, 0, read);
				remaining -= (ulong)read;
			}
			payload.Seek (0, SeekOrigin.Begin);
			return DecompressAssemblyIfNeeded (payload);
		}

		Stream DecompressAssemblyIfNeeded (Stream stream)
		{
			using var reader = new BinaryReader (stream, Encoding.UTF8, leaveOpen: true);
			var magic = reader.ReadUInt32 ();
			if (magic != CompressedAssemblyMagic) {
				stream.Seek (0, SeekOrigin.Begin);
				return stream;
			}

			reader.ReadUInt32 ();
			var decompressedLength = reader.ReadUInt32 ();
			var compressedLength = (int)(stream.Length - stream.Position);
			var compressed = new byte [compressedLength];
			stream.ReadExactly (compressed, 0, compressed.Length);

			var decompressed = new byte [decompressedLength];
			var decoded = LZ4Codec.Decode (compressed, 0, compressed.Length, decompressed, 0, decompressed.Length);
			Assert.AreEqual ((int)decompressedLength, decoded, "Compressed assembly should decompress to the expected size.");
			stream.Dispose ();
			return new MemoryStream (decompressed, 0, decoded, writable: false);
		}

		IEnumerable<TypeDefinition> FlattenType (TypeDefinition type)
		{
			yield return type;
			foreach (var nested in type.NestedTypes) {
				foreach (var nestedType in FlattenType (nested)) {
					yield return nestedType;
				}
			}
		}

		void ReadDexProfile (ApkComparisonProfile profile)
		{
			using var zip = ZipFile.OpenRead (profile.ApkPath);
			foreach (var entry in zip.Entries.Where (e => Regex.IsMatch (e.FullName, @"^classes(\d*)\.dex$", RegexOptions.CultureInvariant))) {
				using var stream = entry.Open ();
				using var memory = new MemoryStream ();
				stream.CopyTo (memory);
				var bytes = memory.ToArray ();

				profile.DexSize += bytes.Length;

				var dex = DexProfileReader.Read (bytes);
				profile.DexFiles.Add ($"{entry.FullName}: size={bytes.Length}, classes={dex.Classes.Count}");
				profile.DexStringIdCount += dex.StringIdCount;
				profile.DexTypeIdCount += dex.TypeIdCount;
				profile.DexProtoIdCount += dex.ProtoIdCount;
				profile.DexFieldIdCount += dex.FieldIdCount;
				profile.DexMethodIdCount += dex.MethodIdCount;
				profile.DexDataSize += dex.DataSize;
				profile.RawJavaClassCount += dex.Classes.Count;
				profile.RawJavaMethodCount += dex.Classes.Sum (c => c.Methods.Count);

				foreach (var javaClass in dex.Classes) {
					profile.JavaClassNames.Add (javaClass.Name);
					if (IsTypemapHelperJavaType (javaClass.Name) || !profile.CandidateJavaNames.Contains (javaClass.Name)) {
						continue;
					}

					profile.FilteredJavaClassCount++;
					profile.FilteredJavaMethodCount += javaClass.Methods.Count;
					profile.JavaTypemapEntries.Add ($"class {javaClass.Name}");
					foreach (var method in javaClass.Methods) {
						profile.JavaTypemapEntries.Add ($"method {javaClass.Name}->{method}");
					}
				}
			}
		}

		bool IsTypemapHelperManagedType (string typeName)
		{
			return typeName.StartsWith ("_TypeMap.", StringComparison.Ordinal) ||
				typeName.StartsWith ("_Microsoft.Android.TypeMaps", StringComparison.Ordinal) ||
				typeName.EndsWith ("/__TypeMapAnchor", StringComparison.Ordinal) ||
				typeName == "Android.Runtime.JavaProxyThrowable" ||
				typeName.IndexOf ("JavaPeerProxy", StringComparison.Ordinal) >= 0 ||
				typeName.IndexOf ("TypeMapProvider", StringComparison.Ordinal) >= 0 ||
				typeName.IndexOf ("TypeMapping", StringComparison.Ordinal) >= 0;
		}

		bool IsTypemapHelperJavaType (string jniName)
		{
			return jniName.StartsWith ("net/dot/android/", StringComparison.Ordinal) ||
				jniName.StartsWith ("mono/android/", StringComparison.Ordinal) ||
				jniName.IndexOf ("JavaPeerProxy", StringComparison.Ordinal) >= 0 ||
				jniName.IndexOf ("TypeMap", StringComparison.Ordinal) >= 0;
		}

		void WriteComparisonDiagnostics (ApkComparisonProfile llvmIr, ApkComparisonProfile trimmable)
		{
			var managedDiff = GetEntryDiff (llvmIr.ManagedTypemapEntries, trimmable.ManagedTypemapEntries);
			var javaDiff = GetEntryDiff (llvmIr.JavaTypemapEntries, trimmable.JavaTypemapEntries);
			var javaClassDiff = GetEntryDiff (llvmIr.JavaClassNames, trimmable.JavaClassNames);

			TestContext.Out.WriteLine ("APK contents comparison: llvm-ir vs trimmable typemap");
			WriteComparisonTable (llvmIr, trimmable, managedDiff, javaDiff);
			WriteSize ("APK", llvmIr.ApkSize, trimmable.ApkSize);
			WriteSize ("assembly stores", llvmIr.AssemblyStoreSize, trimmable.AssemblyStoreSize);
			WriteSize ("classes*.dex", llvmIr.DexSize, trimmable.DexSize);
			WriteProfile (llvmIr);
			WriteProfile (trimmable);
			WriteEntryDiff ("managed typemap entries", managedDiff);
			WriteEntryDiff ("Java typemap entries", javaDiff);
			WriteEntryDiff ("Java classes", javaClassDiff);
		}

		void WriteComparisonTable (ApkComparisonProfile llvmIr, ApkComparisonProfile trimmable, EntryDiff managedDiff, EntryDiff javaDiff)
		{
			TestContext.Out.WriteLine ("| Metric | llvm-ir | trimmable |");
			TestContext.Out.WriteLine ("|---|---:|---:|");
			TestContext.Out.WriteLine ($"| APK size | {FormatNumber (llvmIr.ApkSize)} | {FormatNumber (trimmable.ApkSize)} |");
			TestContext.Out.WriteLine ($"| Assembly-store payload | {FormatNumber (llvmIr.AssemblyStoreSize)} | {FormatNumber (trimmable.AssemblyStoreSize)} |");
			TestContext.Out.WriteLine ($"| classes*.dex | {FormatNumber (llvmIr.DexSize)} | {FormatNumber (trimmable.DexSize)} |");
			TestContext.Out.WriteLine ($"| Registered managed types / methods | {FormatNumber (llvmIr.FilteredManagedTypeCount)} / {FormatNumber (llvmIr.FilteredManagedMethodCount)} | {FormatNumber (trimmable.FilteredManagedTypeCount)} / {FormatNumber (trimmable.FilteredManagedMethodCount)} |");
			TestContext.Out.WriteLine ($"| Managed diff | {FormatNumber (managedDiff.LlvmIrOnly.Length)} llvm-ir-only | {FormatNumber (managedDiff.TrimmableOnly.Length)} trimmable-only |");
			TestContext.Out.WriteLine ($"| Java diff | {FormatNumber (javaDiff.LlvmIrOnly.Length)} llvm-ir-only | {FormatNumber (javaDiff.TrimmableOnly.Length)} trimmable-only |");
		}

		string FormatNumber (long value) => value.ToString ("N0", CultureInfo.InvariantCulture);

		void WriteProfile (ApkComparisonProfile profile)
		{
			TestContext.Out.WriteLine ($"{profile.Name}: apk={profile.ApkSize} bytes, stores={profile.AssemblyStoreCount}, store-bytes={profile.AssemblyStoreSize}, dex-bytes={profile.DexSize}");
			TestContext.Out.WriteLine ($"{profile.Name}: managed assemblies={profile.ManagedAssemblyCount}, raw types={profile.RawManagedTypeCount}, raw methods={profile.RawManagedMethodCount}, filtered types={profile.FilteredManagedTypeCount}, filtered methods={profile.FilteredManagedMethodCount}");
			TestContext.Out.WriteLine ($"{profile.Name}: raw Java classes={profile.RawJavaClassCount}, raw Java methods={profile.RawJavaMethodCount}, filtered classes={profile.FilteredJavaClassCount}, filtered methods={profile.FilteredJavaMethodCount}");
			TestContext.Out.WriteLine ($"{profile.Name}: dex ids: strings={profile.DexStringIdCount}, types={profile.DexTypeIdCount}, protos={profile.DexProtoIdCount}, fields={profile.DexFieldIdCount}, methods={profile.DexMethodIdCount}, data-size={profile.DexDataSize}");
			TestContext.Out.WriteLine ($"{profile.Name}: generated Java sources={profile.GeneratedJavaSourceCount}, __md_methods files={profile.GeneratedJavaWithMdMethodsCount}, Runtime.register files={profile.GeneratedJavaWithRuntimeRegisterCount}, Runtime.registerNatives files={profile.GeneratedJavaWithRegisterNativesCount}");
			TestContext.Out.WriteLine ($"{profile.Name}: assembly stores: {String.Join ("; ", profile.AssemblyStores)}");
			TestContext.Out.WriteLine ($"{profile.Name}: dex files: {String.Join ("; ", profile.DexFiles)}");
			foreach (var metrics in profile.TypeMapAssemblies) {
				TestContext.Out.WriteLine ($"{profile.Name}: typemap {metrics.Stage}/{metrics.AssemblyName}: typemap={metrics.TypeMapAttributeCount}, unique={metrics.UniqueTypeMapAttributeCount}, duplicates={metrics.DuplicateTypeMapAttributeCount}, unconditional={metrics.UnconditionalTypeMapAttributeCount}, conditional={metrics.ConditionalTypeMapAttributeCount}, associations={metrics.AssociationAttributeCount}, assembly-targets={metrics.AssemblyTargetAttributeCount}, prefixes={metrics.FormatPrefixBuckets ()}");
			}
		}

		void WriteSize (string label, long llvmIr, long trimmable)
		{
			var ratio = llvmIr == 0 ? 0 : (double)trimmable / llvmIr;
			TestContext.Out.WriteLine ($"{label}: llvm-ir={llvmIr}, trimmable={trimmable}, delta={trimmable - llvmIr}, ratio={ratio:0.000}");
		}

		EntryDiff GetEntryDiff (ISet<string> llvmIr, ISet<string> trimmable)
		{
			var llvmOnly = llvmIr.Except (trimmable, StringComparer.Ordinal).OrderBy (x => x, StringComparer.Ordinal).ToArray ();
			var trimmableOnly = trimmable.Except (llvmIr, StringComparer.Ordinal).OrderBy (x => x, StringComparer.Ordinal).ToArray ();
			var common = llvmIr.Intersect (trimmable, StringComparer.Ordinal).Count ();

			return new EntryDiff (llvmOnly, trimmableOnly, common);
		}

		void WriteEntryDiff (string label, EntryDiff diff)
		{
			TestContext.Out.WriteLine ($"{label}: llvm-ir only={diff.LlvmIrOnly.Length}, trimmable only={diff.TrimmableOnly.Length}, common={diff.Common}");
			WriteSample ($"{label} llvm-ir only", diff.LlvmIrOnly);
			WriteSample ($"{label} trimmable only", diff.TrimmableOnly);
		}

		void WriteSample (string label, string [] entries)
		{
			if (entries.Length == 0) {
				return;
			}

			TestContext.Out.WriteLine ($"{label}:");
			foreach (var entry in entries.Take (50)) {
				TestContext.Out.WriteLine ($"  {entry}");
			}
			if (entries.Length > 50) {
				TestContext.Out.WriteLine ($"  ... {entries.Length - 50} more");
			}
		}

		class ApkComparisonProfile
		{
			public string Name;
			public string ApkPath;
			public long ApkSize;
			public int AssemblyStoreCount;
			public long AssemblyStoreSize;
			public long DexSize;
			public int ManagedAssemblyCount;
			public int RawManagedTypeCount;
			public int RawManagedMethodCount;
			public int FilteredManagedTypeCount;
			public int FilteredManagedMethodCount;
			public int RawJavaClassCount;
			public int RawJavaMethodCount;
			public int FilteredJavaClassCount;
			public int FilteredJavaMethodCount;
			public int DexStringIdCount;
			public int DexTypeIdCount;
			public int DexProtoIdCount;
			public int DexFieldIdCount;
			public int DexMethodIdCount;
			public int DexDataSize;
			public int GeneratedJavaSourceCount;
			public int GeneratedJavaWithMdMethodsCount;
			public int GeneratedJavaWithRuntimeRegisterCount;
			public int GeneratedJavaWithRegisterNativesCount;
			public readonly List<string> AssemblyStores = new List<string> ();
			public readonly List<string> DexFiles = new List<string> ();
			public readonly HashSet<string> ManagedAssemblyNames = new HashSet<string> (StringComparer.Ordinal);
			public readonly HashSet<string> CandidateManagedTypes = new HashSet<string> (StringComparer.Ordinal);
			public readonly HashSet<string> CandidateJavaNames = new HashSet<string> (StringComparer.Ordinal);
			public readonly HashSet<string> ManagedTypemapEntries = new HashSet<string> (StringComparer.Ordinal);
			public readonly HashSet<string> JavaTypemapEntries = new HashSet<string> (StringComparer.Ordinal);
			public readonly HashSet<string> JavaClassNames = new HashSet<string> (StringComparer.Ordinal);
			public readonly List<TypeMapAssemblyMetrics> TypeMapAssemblies = new List<TypeMapAssemblyMetrics> ();
		}

		class TypeMapAssemblyMetrics
		{
			public string Stage;
			public string AssemblyName;
			public int TypeMapAttributeCount;
			public int UnconditionalTypeMapAttributeCount;
			public int ConditionalTypeMapAttributeCount;
			public int AssociationAttributeCount;
			public int AssemblyTargetAttributeCount;
			public readonly List<string> TypeMapAttributeKeys = new List<string> ();
			readonly SortedDictionary<string, int> prefixBuckets = new SortedDictionary<string, int> (StringComparer.Ordinal);

			public int UniqueTypeMapAttributeCount => TypeMapAttributeKeys.Distinct (StringComparer.Ordinal).Count ();
			public int DuplicateTypeMapAttributeCount => TypeMapAttributeCount - UniqueTypeMapAttributeCount;

			public void IncrementPrefixBucket (string jniName)
			{
				var bucket = GetPrefixBucket (jniName);
				prefixBuckets.TryGetValue (bucket, out int count);
				prefixBuckets [bucket] = count + 1;
			}

			public string FormatPrefixBuckets ()
			{
				return String.Join (", ", prefixBuckets.Select (p => $"{p.Key}={p.Value}"));
			}

			static string GetPrefixBucket (string jniName)
			{
				if (jniName.StartsWith ("mono/android/", StringComparison.Ordinal)) {
					return "mono/android";
				}
				if (jniName.StartsWith ("android/", StringComparison.Ordinal)) {
					return "android";
				}
				if (jniName.StartsWith ("java/", StringComparison.Ordinal)) {
					return "java";
				}
				if (jniName.StartsWith ("com/xamarin/", StringComparison.Ordinal)) {
					return "app";
				}
				return "other";
			}
		}

		class EntryDiff
		{
			public EntryDiff (string [] llvmIrOnly, string [] trimmableOnly, int common)
			{
				LlvmIrOnly = llvmIrOnly;
				TrimmableOnly = trimmableOnly;
				Common = common;
			}

			public string [] LlvmIrOnly { get; }
			public string [] TrimmableOnly { get; }
			public int Common { get; }
		}

		class DexClass
		{
			public string Name;
			public readonly List<string> Methods = new List<string> ();
		}

		class DexProfile
		{
			public readonly List<DexClass> Classes = new List<DexClass> ();
			public int StringIdCount { get; set; }
			public int TypeIdCount { get; set; }
			public int ProtoIdCount { get; set; }
			public int FieldIdCount { get; set; }
			public int MethodIdCount { get; set; }
			public int DataSize { get; set; }
		}

		class DexProfileReader
		{
			readonly byte [] data;

			DexProfileReader (byte [] data)
			{
				this.data = data;
			}

			public static DexProfile Read (byte [] data) => new DexProfileReader (data).ReadProfile ();

			DexProfile ReadProfile ()
			{
				Assert.AreEqual ((byte)'d', data [0], "classes.dex magic should start with dex.");
				Assert.AreEqual ((byte)'e', data [1], "classes.dex magic should start with dex.");
				Assert.AreEqual ((byte)'x', data [2], "classes.dex magic should start with dex.");

				var strings = ReadStrings ();
				var typeIds = ReadTypeIds (strings);
				var protoIds = ReadProtoIds (typeIds);
				var methodIds = ReadMethodIds (strings, protoIds);
				var profile = new DexProfile {
					StringIdCount = strings.Length,
					TypeIdCount = typeIds.Length,
					ProtoIdCount = protoIds.Length,
					FieldIdCount = ReadInt32 (80),
					MethodIdCount = methodIds.Length,
					DataSize = ReadInt32 (104),
				};

				var classDefsSize = ReadInt32 (96);
				var classDefsOffset = ReadInt32 (100);
				for (int i = 0; i < classDefsSize; i++) {
					var classDefOffset = classDefsOffset + i * 32;
					var classIdx = ReadInt32 (classDefOffset);
					var classDataOffset = ReadInt32 (classDefOffset + 24);
					var dexClass = new DexClass {
						Name = DescriptorToJniName (GetItem (typeIds, classIdx, "class type")),
					};

					if (classDataOffset != 0) {
						ReadClassData (classDataOffset, methodIds, dexClass);
					}

					profile.Classes.Add (dexClass);
				}

				return profile;
			}

			string [] ReadStrings ()
			{
				var stringsSize = ReadInt32 (56);
				var stringsOffset = ReadInt32 (60);
				var strings = new string [stringsSize];
				for (int i = 0; i < stringsSize; i++) {
					var offset = ReadInt32 (stringsOffset + i * 4);
					ReadUleb128 (ref offset);
					var start = offset;
					while (true) {
						EnsureAvailable (offset, 1, "string data");
						if (data [offset] == 0) {
							break;
						}
						offset++;
					}
					strings [i] = Encoding.UTF8.GetString (data, start, offset - start);
				}

				return strings;
			}

			string [] ReadTypeIds (string [] strings)
			{
				var typeIdsSize = ReadInt32 (64);
				var typeIdsOffset = ReadInt32 (68);
				var typeIds = new string [typeIdsSize];
				for (int i = 0; i < typeIdsSize; i++) {
					typeIds [i] = GetItem (strings, ReadInt32 (typeIdsOffset + i * 4), "type descriptor string");
				}

				return typeIds;
			}

			string [] ReadProtoIds (string [] typeIds)
			{
				var protoIdsSize = ReadInt32 (72);
				var protoIdsOffset = ReadInt32 (76);
				var protoIds = new string [protoIdsSize];
				for (int i = 0; i < protoIdsSize; i++) {
					var returnType = GetItem (typeIds, ReadInt32 (protoIdsOffset + i * 12 + 4), "proto return type");
					var parametersOffset = ReadInt32 (protoIdsOffset + i * 12 + 8);
					protoIds [i] = $"({ReadTypeList (parametersOffset, typeIds)}){returnType}";
				}

				return protoIds;
			}

			string ReadTypeList (int offset, string [] typeIds)
			{
				if (offset == 0) {
					return "";
				}

				var count = ReadInt32 (offset);
				var builder = new StringBuilder ();
				offset += 4;
				for (int i = 0; i < count; i++) {
					builder.Append (GetItem (typeIds, ReadUInt16 (offset + i * 2), "proto parameter type"));
				}
				return builder.ToString ();
			}

			string [] ReadMethodIds (string [] strings, string [] protoIds)
			{
				var methodIdsSize = ReadInt32 (88);
				var methodIdsOffset = ReadInt32 (92);
				var methodIds = new string [methodIdsSize];
				for (int i = 0; i < methodIdsSize; i++) {
					var protoIdx = ReadUInt16 (methodIdsOffset + i * 8 + 2);
					methodIds [i] = GetItem (strings, ReadInt32 (methodIdsOffset + i * 8 + 4), "method name string") + GetItem (protoIds, protoIdx, "method prototype");
				}

				return methodIds;
			}

			void ReadClassData (int offset, string [] methodIds, DexClass dexClass)
			{
				var staticFieldsSize = ReadUleb128 (ref offset);
				var instanceFieldsSize = ReadUleb128 (ref offset);
				var directMethodsSize = ReadUleb128 (ref offset);
				var virtualMethodsSize = ReadUleb128 (ref offset);

				SkipEncodedFields (staticFieldsSize, ref offset);
				SkipEncodedFields (instanceFieldsSize, ref offset);
				ReadEncodedMethods (directMethodsSize, methodIds, dexClass, ref offset);
				ReadEncodedMethods (virtualMethodsSize, methodIds, dexClass, ref offset);
			}

			void SkipEncodedFields (int count, ref int offset)
			{
				for (int i = 0; i < count; i++) {
					ReadUleb128 (ref offset);
					ReadUleb128 (ref offset);
				}
			}

			void ReadEncodedMethods (int count, string [] methodIds, DexClass dexClass, ref int offset)
			{
				var methodIndex = 0;
				for (int i = 0; i < count; i++) {
					methodIndex += ReadUleb128 (ref offset);
					ReadUleb128 (ref offset);
					ReadUleb128 (ref offset);
					dexClass.Methods.Add (GetItem (methodIds, methodIndex, "encoded method"));
				}
			}

			int ReadInt32 (int offset)
			{
				EnsureAvailable (offset, 4, "uint");
				return data [offset] |
					(data [offset + 1] << 8) |
					(data [offset + 2] << 16) |
					(data [offset + 3] << 24);
			}

			int ReadUInt16 (int offset)
			{
				EnsureAvailable (offset, 2, "ushort");
				return data [offset] |
					(data [offset + 1] << 8);
			}

			int ReadUleb128 (ref int offset)
			{
				var result = 0;
				var shift = 0;
				int value;
				do {
					EnsureAvailable (offset, 1, "ULEB128");
					value = data [offset++];
					result |= (value & 0x7f) << shift;
					shift += 7;
				} while ((value & 0x80) != 0);

				return result;
			}

			T GetItem<T> (T [] items, int index, string description)
			{
				Assert.IsTrue (index >= 0 && index < items.Length, $"Invalid {description} index {index}; table has {items.Length} entries.");
				return items [index];
			}

			void EnsureAvailable (int offset, int count, string description)
			{
				Assert.IsTrue (offset >= 0 && count >= 0 && offset <= data.Length - count, $"DEX {description} read at offset {offset} with size {count} exceeds file size {data.Length}.");
			}

			string DescriptorToJniName (string descriptor)
			{
				if (descriptor.Length >= 2 && descriptor [0] == 'L' && descriptor [descriptor.Length - 1] == ';') {
					return descriptor.Substring (1, descriptor.Length - 2);
				}

				return descriptor;
			}
		}
	}
}
