using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Prepare
{
	partial class Runtimes
	{
		static Context ctx => Context.Instance;

		public readonly List<Runtime> Items = new List <Runtime> {
			new MonoJitRuntime (
				abiName: AbiNames.TargetJit.AndroidArmV7a,
				enabledCheck: (Context ctx) => ctx.IsTargetJitAbiEnabled (AbiNames.TargetJit.AndroidArmV7a)
			),

			new MonoJitRuntime (
				abiName: AbiNames.TargetJit.AndroidArmV8a,
				enabledCheck: (Context ctx) => ctx.IsTargetJitAbiEnabled (AbiNames.TargetJit.AndroidArmV8a)
			),

			new MonoJitRuntime (
				abiName: AbiNames.TargetJit.AndroidX86,
				enabledCheck: (Context ctx) => ctx.IsTargetJitAbiEnabled (AbiNames.TargetJit.AndroidX86)
			),

			new MonoJitRuntime (
				abiName: AbiNames.TargetJit.AndroidX86_64,
				enabledCheck: (Context ctx) => ctx.IsTargetJitAbiEnabled (AbiNames.TargetJit.AndroidX86_64)
			),

			new MonoHostRuntime (
				name: AbiNames.HostJit.Linux,
				mingw: false,
				enabledCheck: (Context ctx) => ctx.IsHostJitAbiEnabled (AbiNames.HostJit.Linux)
			),

			new MonoHostRuntime (
				name: AbiNames.HostJit.Darwin,
				mingw: false,
				enabledCheck: (Context ctx) => ctx.IsHostJitAbiEnabled (AbiNames.HostJit.Darwin)
			),

			new MonoHostRuntime (
				name: AbiNames.HostJit.Win32,
				mingw: true,
				enabledCheck: (Context ctx) => ctx.IsHostJitAbiEnabled (AbiNames.HostJit.Win32)
			),

			new MonoHostRuntime (
				name: AbiNames.HostJit.Win64,
				mingw: true,
				enabledCheck: (Context ctx) => ctx.IsHostJitAbiEnabled (AbiNames.HostJit.Win64)
			),

			new MonoCrossRuntime (
				name: AbiNames.CrossAot.ArmV7a,
				enabledCheck: (Context ctx) => ctx.IsTargetAotAbiEnabled (AbiNames.TargetAot.ArmV7a)
			),

			new MonoCrossRuntime (
				name: AbiNames.CrossAot.ArmV8a,
				enabledCheck: (Context ctx) => ctx.IsTargetAotAbiEnabled (AbiNames.TargetAot.ArmV8a)
			),

			new MonoCrossRuntime (
				name: AbiNames.CrossAot.X86,
				enabledCheck: (Context ctx) => ctx.IsTargetAotAbiEnabled (AbiNames.TargetAot.X86)
			),

			new MonoCrossRuntime (
				name: AbiNames.CrossAot.X86_64,
				enabledCheck: (Context ctx) => ctx.IsTargetAotAbiEnabled (AbiNames.TargetAot.X86_64)
			),

			new MonoCrossRuntime (
				name: AbiNames.CrossAot.WinArmV7a,
				enabledCheck: (Context ctx) => ctx.IsTargetAotAbiEnabled (AbiNames.TargetAot.WinArmV7a)
			),

			new MonoCrossRuntime (
				name: AbiNames.CrossAot.WinArmV8a,
				enabledCheck: (Context ctx) => ctx.IsTargetAotAbiEnabled (AbiNames.TargetAot.WinArmV8a)
			),

			new MonoCrossRuntime (
				name: AbiNames.CrossAot.WinX86,
				enabledCheck: (Context ctx) => ctx.IsTargetAotAbiEnabled (AbiNames.TargetAot.WinX86)
			),

			new MonoCrossRuntime (
				name: AbiNames.CrossAot.WinX86_64,
				enabledCheck: (Context ctx) => ctx.IsTargetAotAbiEnabled (AbiNames.TargetAot.WinX86_64)
			),

			new LlvmRuntime (
				name: AbiNames.Llvm.Host32Bit,
				enabledCheck: (Context ctx) => IsLlvmRuntimeEnabled (ctx, AbiNames.Llvm.Host32Bit)
			),

			new LlvmRuntime (
				name: AbiNames.Llvm.Host64Bit,
				enabledCheck: (Context ctx) => IsLlvmRuntimeEnabled (ctx, AbiNames.Llvm.Host64Bit)
			),

			new LlvmRuntime (
				name: AbiNames.Llvm.Windows32Bit,
				enabledCheck: (Context ctx) => IsLlvmRuntimeEnabled (ctx, AbiNames.Llvm.Windows32Bit)
			),

			new LlvmRuntime (
				name: AbiNames.Llvm.Windows64Bit,
				enabledCheck: (Context) => IsLlvmRuntimeEnabled (ctx, AbiNames.Llvm.Windows64Bit)
			),
		};

		public readonly List<BclFile> BclFilesToInstall = new List<BclFile> {
			new BclFile ("I18N.dll",                                                    BclFileType.ProfileAssembly),
			new BclFile ("I18N.CJK.dll",                                                BclFileType.ProfileAssembly),
			new BclFile ("I18N.MidEast.dll",                                            BclFileType.ProfileAssembly),
			new BclFile ("I18N.Other.dll",                                              BclFileType.ProfileAssembly),
			new BclFile ("I18N.Rare.dll",                                               BclFileType.ProfileAssembly),
			new BclFile ("I18N.West.dll",                                               BclFileType.ProfileAssembly),
			new BclFile ("Microsoft.CSharp.dll",                                        BclFileType.ProfileAssembly),
			new BclFile ("Mono.Btls.Interface.dll",                                     BclFileType.ProfileAssembly),
			new BclFile ("Mono.CompilerServices.SymbolWriter.dll",                      BclFileType.ProfileAssembly),
			new BclFile ("Mono.CSharp.dll",                                             BclFileType.ProfileAssembly),
			new BclFile ("Mono.Data.Sqlite.dll",                                        BclFileType.ProfileAssembly),
			new BclFile ("Mono.Data.Tds.dll",                                           BclFileType.ProfileAssembly),
			new BclFile ("Mono.Posix.dll",                                              BclFileType.ProfileAssembly),
			new BclFile ("Mono.Security.dll",                                           BclFileType.ProfileAssembly),
			new BclFile ("mscorlib.dll",                                                BclFileType.ProfileAssembly),
			new BclFile ("System.dll",                                                  BclFileType.ProfileAssembly),
			new BclFile ("System.ComponentModel.Composition.dll",                       BclFileType.ProfileAssembly),
			new BclFile ("System.ComponentModel.DataAnnotations.dll",                   BclFileType.ProfileAssembly),
			new BclFile ("System.Core.dll",                                             BclFileType.ProfileAssembly),
			new BclFile ("System.Data.dll",                                             BclFileType.ProfileAssembly),
			new BclFile ("System.Data.Services.Client.dll",                             BclFileType.ProfileAssembly),
			new BclFile ("System.IdentityModel.dll",                                    BclFileType.ProfileAssembly),
			new BclFile ("System.IO.Compression.dll",                                   BclFileType.ProfileAssembly),
			new BclFile ("System.IO.Compression.FileSystem.dll",                        BclFileType.ProfileAssembly),
			new BclFile ("System.Json.dll",                                             BclFileType.ProfileAssembly),
			new BclFile ("System.Net.dll",                                              BclFileType.ProfileAssembly),
			new BclFile ("System.Net.Http.dll",                                         BclFileType.ProfileAssembly),
			new BclFile ("System.Net.Http.WinHttpHandler.dll",                          BclFileType.ProfileAssembly),
			new BclFile ("System.Numerics.dll",                                         BclFileType.ProfileAssembly),
			new BclFile ("System.Numerics.Vectors.dll",                                 BclFileType.ProfileAssembly),
			new BclFile ("System.Reflection.Context.dll",                               BclFileType.ProfileAssembly),
			new BclFile ("System.Runtime.CompilerServices.Unsafe.dll",                  BclFileType.ProfileAssembly, excludeDebugSymbols: true),
			new BclFile ("System.Runtime.Serialization.dll",                            BclFileType.ProfileAssembly),
			new BclFile ("System.Security.dll",                                         BclFileType.ProfileAssembly),
			new BclFile ("System.ServiceModel.dll",                                     BclFileType.ProfileAssembly),
			new BclFile ("System.ServiceModel.Internals.dll",                           BclFileType.ProfileAssembly),
			new BclFile ("System.ServiceModel.Web.dll",                                 BclFileType.ProfileAssembly),
			new BclFile ("System.Transactions.dll",                                     BclFileType.ProfileAssembly),
			new BclFile ("System.Web.Services.dll",                                     BclFileType.ProfileAssembly),
			new BclFile ("System.Windows.dll",                                          BclFileType.ProfileAssembly, excludeDebugSymbols: true),
			new BclFile ("System.Xml.dll",                                              BclFileType.ProfileAssembly),
			new BclFile ("System.Xml.Linq.dll",                                         BclFileType.ProfileAssembly),
			new BclFile ("System.Xml.Serialization.dll",                                BclFileType.ProfileAssembly, excludeDebugSymbols: true),

			new BclFile ("Microsoft.Win32.Primitives.dll",                              BclFileType.FacadeAssembly),
			new BclFile ("Microsoft.Win32.Registry.dll",                                BclFileType.FacadeAssembly),
			new BclFile ("Microsoft.Win32.Registry.AccessControl.dll",                  BclFileType.FacadeAssembly),
			new BclFile ("netstandard.dll",                                             BclFileType.FacadeAssembly),
			new BclFile ("System.AppContext.dll",                                       BclFileType.FacadeAssembly),
			new BclFile ("System.Buffers.dll",                                          BclFileType.FacadeAssembly),
			new BclFile ("System.Collections.dll",                                      BclFileType.FacadeAssembly),
			new BclFile ("System.Collections.Concurrent.dll",                           BclFileType.FacadeAssembly),
			new BclFile ("System.Collections.NonGeneric.dll",                           BclFileType.FacadeAssembly),
			new BclFile ("System.Collections.Specialized.dll",                          BclFileType.FacadeAssembly),
			new BclFile ("System.ComponentModel.dll",                                   BclFileType.FacadeAssembly),
			new BclFile ("System.ComponentModel.Annotations.dll",                       BclFileType.FacadeAssembly),
			new BclFile ("System.ComponentModel.EventBasedAsync.dll",                   BclFileType.FacadeAssembly),
			new BclFile ("System.ComponentModel.Primitives.dll",                        BclFileType.FacadeAssembly),
			new BclFile ("System.ComponentModel.TypeConverter.dll",                     BclFileType.FacadeAssembly),
			new BclFile ("System.Console.dll",                                          BclFileType.FacadeAssembly),
			new BclFile ("System.Data.Common.dll",                                      BclFileType.FacadeAssembly),
			new BclFile ("System.Data.SqlClient.dll",                                   BclFileType.FacadeAssembly),
			new BclFile ("System.Diagnostics.Contracts.dll",                            BclFileType.FacadeAssembly),
			new BclFile ("System.Diagnostics.Debug.dll",                                BclFileType.FacadeAssembly),
			new BclFile ("System.Diagnostics.FileVersionInfo.dll",                      BclFileType.FacadeAssembly),
			new BclFile ("System.Diagnostics.Process.dll",                              BclFileType.FacadeAssembly),
			new BclFile ("System.Diagnostics.StackTrace.dll",                           BclFileType.FacadeAssembly),
			new BclFile ("System.Diagnostics.TextWriterTraceListener.dll",              BclFileType.FacadeAssembly),
			new BclFile ("System.Diagnostics.Tools.dll",                                BclFileType.FacadeAssembly),
			new BclFile ("System.Diagnostics.TraceEvent.dll",                           BclFileType.FacadeAssembly),
			new BclFile ("System.Diagnostics.TraceSource.dll",                          BclFileType.FacadeAssembly),
			new BclFile ("System.Diagnostics.Tracing.dll",                              BclFileType.FacadeAssembly),
			new BclFile ("System.Drawing.Common.dll",                                   BclFileType.FacadeAssembly),
			new BclFile ("System.Drawing.Primitives.dll",                               BclFileType.FacadeAssembly),
			new BclFile ("System.Dynamic.Runtime.dll",                                  BclFileType.FacadeAssembly),
			new BclFile ("System.Globalization.dll",                                    BclFileType.FacadeAssembly),
			new BclFile ("System.Globalization.Calendars.dll",                          BclFileType.FacadeAssembly),
			new BclFile ("System.Globalization.Extensions.dll",                         BclFileType.FacadeAssembly),
			new BclFile ("System.IO.dll",                                               BclFileType.FacadeAssembly),
			new BclFile ("System.IO.Compression.ZipFile.dll",                           BclFileType.FacadeAssembly),
			new BclFile ("System.IO.FileSystem.dll",                                    BclFileType.FacadeAssembly),
			new BclFile ("System.IO.FileSystem.AccessControl.dll",                      BclFileType.FacadeAssembly),
			new BclFile ("System.IO.FileSystem.DriveInfo.dll",                          BclFileType.FacadeAssembly),
			new BclFile ("System.IO.FileSystem.Primitives.dll",                         BclFileType.FacadeAssembly),
			new BclFile ("System.IO.FileSystem.Watcher.dll",                            BclFileType.FacadeAssembly),
			new BclFile ("System.IO.IsolatedStorage.dll",                               BclFileType.FacadeAssembly),
			new BclFile ("System.IO.MemoryMappedFiles.dll",                             BclFileType.FacadeAssembly),
			new BclFile ("System.IO.Pipes.dll",                                         BclFileType.FacadeAssembly),
			new BclFile ("System.IO.UnmanagedMemoryStream.dll",                         BclFileType.FacadeAssembly),
			new BclFile ("System.Linq.dll",                                             BclFileType.FacadeAssembly),
			new BclFile ("System.Linq.Expressions.dll",                                 BclFileType.FacadeAssembly),
			new BclFile ("System.Linq.Parallel.dll",                                    BclFileType.FacadeAssembly),
			new BclFile ("System.Linq.Queryable.dll",                                   BclFileType.FacadeAssembly),
			new BclFile ("System.Memory.dll",                                           BclFileType.FacadeAssembly),
			new BclFile ("System.Net.AuthenticationManager.dll",                        BclFileType.FacadeAssembly),
			new BclFile ("System.Net.Cache.dll",                                        BclFileType.FacadeAssembly),
			new BclFile ("System.Net.HttpListener.dll",                                 BclFileType.FacadeAssembly),
			new BclFile ("System.Net.Mail.dll",                                         BclFileType.FacadeAssembly),
			new BclFile ("System.Net.NameResolution.dll",                               BclFileType.FacadeAssembly),
			new BclFile ("System.Net.NetworkInformation.dll",                           BclFileType.FacadeAssembly),
			new BclFile ("System.Net.Ping.dll",                                         BclFileType.FacadeAssembly),
			new BclFile ("System.Net.Primitives.dll",                                   BclFileType.FacadeAssembly),
			new BclFile ("System.Net.Requests.dll",                                     BclFileType.FacadeAssembly),
			new BclFile ("System.Net.Security.dll",                                     BclFileType.FacadeAssembly),
			new BclFile ("System.Net.ServicePoint.dll",                                 BclFileType.FacadeAssembly),
			new BclFile ("System.Net.Sockets.dll",                                      BclFileType.FacadeAssembly),
			new BclFile ("System.Net.Utilities.dll",                                    BclFileType.FacadeAssembly),
			new BclFile ("System.Net.WebHeaderCollection.dll",                          BclFileType.FacadeAssembly),
			new BclFile ("System.Net.WebSockets.dll",                                   BclFileType.FacadeAssembly),
			new BclFile ("System.Net.WebSockets.Client.dll",                            BclFileType.FacadeAssembly),
			new BclFile ("System.ObjectModel.dll",                                      BclFileType.FacadeAssembly),
			new BclFile ("System.Reflection.dll",                                       BclFileType.FacadeAssembly),
			new BclFile ("System.Reflection.DispatchProxy.dll",                         BclFileType.FacadeAssembly),
			new BclFile ("System.Reflection.Emit.dll",                                  BclFileType.FacadeAssembly),
			new BclFile ("System.Reflection.Emit.ILGeneration.dll",                     BclFileType.FacadeAssembly),
			new BclFile ("System.Reflection.Emit.Lightweight.dll",                      BclFileType.FacadeAssembly),
			new BclFile ("System.Reflection.Extensions.dll",                            BclFileType.FacadeAssembly),
			new BclFile ("System.Reflection.Primitives.dll",                            BclFileType.FacadeAssembly),
			new BclFile ("System.Reflection.TypeExtensions.dll",                        BclFileType.FacadeAssembly),
			new BclFile ("System.Resources.Reader.dll",                                 BclFileType.FacadeAssembly),
			new BclFile ("System.Resources.ReaderWriter.dll",                           BclFileType.FacadeAssembly),
			new BclFile ("System.Resources.ResourceManager.dll",                        BclFileType.FacadeAssembly),
			new BclFile ("System.Resources.Writer.dll",                                 BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.dll",                                          BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.CompilerServices.VisualC.dll",                 BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.Extensions.dll",                               BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.Handles.dll",                                  BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.InteropServices.dll",                          BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.InteropServices.RuntimeInformation.dll",       BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.InteropServices.WindowsRuntime.dll",           BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.Loader.dll",                                   BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.Numerics.dll",                                 BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.Serialization.Formatters.dll",                 BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.Serialization.Json.dll",                       BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.Serialization.Primitives.dll",                 BclFileType.FacadeAssembly),
			new BclFile ("System.Runtime.Serialization.Xml.dll",                        BclFileType.FacadeAssembly),
			new BclFile ("System.Security.AccessControl.dll",                           BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Claims.dll",                                  BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.Algorithms.dll",                 BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.Cng.dll",                        BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.Csp.dll",                        BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.DeriveBytes.dll",                BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.Encoding.dll",                   BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.Encryption.dll",                 BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.Encryption.Aes.dll",             BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.Encryption.ECDiffieHellman.dll", BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.Encryption.ECDsa.dll",           BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.Hashing.dll",                    BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.Hashing.Algorithms.dll",         BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.OpenSsl.dll",                    BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.Pkcs.dll",                       BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.Primitives.dll",                 BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.ProtectedData.dll",              BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.RandomNumberGenerator.dll",      BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.RSA.dll",                        BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Cryptography.X509Certificates.dll",           BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Principal.dll",                               BclFileType.FacadeAssembly),
			new BclFile ("System.Security.Principal.Windows.dll",                       BclFileType.FacadeAssembly),
			new BclFile ("System.Security.SecureString.dll",                            BclFileType.FacadeAssembly),
			new BclFile ("System.ServiceModel.Duplex.dll",                              BclFileType.FacadeAssembly),
			new BclFile ("System.ServiceModel.Http.dll",                                BclFileType.FacadeAssembly),
			new BclFile ("System.ServiceModel.NetTcp.dll",                              BclFileType.FacadeAssembly),
			new BclFile ("System.ServiceModel.Primitives.dll",                          BclFileType.FacadeAssembly),
			new BclFile ("System.ServiceModel.Security.dll",                            BclFileType.FacadeAssembly),
			new BclFile ("System.ServiceProcess.ServiceController.dll",                 BclFileType.FacadeAssembly),
			new BclFile ("System.Text.Encoding.dll",                                    BclFileType.FacadeAssembly),
			new BclFile ("System.Text.Encoding.CodePages.dll",                          BclFileType.FacadeAssembly),
			new BclFile ("System.Text.Encoding.Extensions.dll",                         BclFileType.FacadeAssembly),
			new BclFile ("System.Text.RegularExpressions.dll",                          BclFileType.FacadeAssembly),
			new BclFile ("System.Threading.dll",                                        BclFileType.FacadeAssembly),
			new BclFile ("System.Threading.AccessControl.dll",                          BclFileType.FacadeAssembly),
			new BclFile ("System.Threading.Overlapped.dll",                             BclFileType.FacadeAssembly),
			new BclFile ("System.Threading.Tasks.dll",                                  BclFileType.FacadeAssembly),
			new BclFile ("System.Threading.Tasks.Extensions.dll",                       BclFileType.FacadeAssembly),
			new BclFile ("System.Threading.Tasks.Parallel.dll",                         BclFileType.FacadeAssembly),
			new BclFile ("System.Threading.Thread.dll",                                 BclFileType.FacadeAssembly),
			new BclFile ("System.Threading.ThreadPool.dll",                             BclFileType.FacadeAssembly),
			new BclFile ("System.Threading.Timer.dll",                                  BclFileType.FacadeAssembly),
			new BclFile ("System.ValueTuple.dll",                                       BclFileType.FacadeAssembly),
			new BclFile ("System.Xml.ReaderWriter.dll",                                 BclFileType.FacadeAssembly),
			new BclFile ("System.Xml.XDocument.dll",                                    BclFileType.FacadeAssembly),
			new BclFile ("System.Xml.XmlDocument.dll",                                  BclFileType.FacadeAssembly),
			new BclFile ("System.Xml.XmlSerializer.dll",                                BclFileType.FacadeAssembly),
			new BclFile ("System.Xml.XPath.dll",                                        BclFileType.FacadeAssembly),
			new BclFile ("System.Xml.XPath.XDocument.dll",                              BclFileType.FacadeAssembly),
			new BclFile ("System.Xml.XPath.XmlDocument.dll",                            BclFileType.FacadeAssembly),
			new BclFile ("System.Xml.Xsl.Primitives.dll",                               BclFileType.FacadeAssembly),
		};

		// These two are populated from BclFilesToInstall in the constructor
		public readonly List<BclFile> DesignerHostBclFilesToInstall;
		public readonly List<BclFile> DesignerWindowsBclFilesToInstall;

		public static readonly List <TestAssembly> TestAssemblies = new List <TestAssembly> {
			new TestAssembly ("BinarySerializationOverVersionsTest.dll",                         TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_corlib_test.dll",                                       TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_corlib_xunit-test.dll",                                 TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_I18N.CJK_test.dll",                                     TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_I18N.MidEast_test.dll",                                 TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_I18N.Other_test.dll",                                   TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_I18N.Rare_test.dll",                                    TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_I18N.West_test.dll",                                    TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_Microsoft.CSharp_xunit-test.dll",                       TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_Mono.CSharp_test.dll",                                  TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_Mono.Data.Sqlite_test.dll",                             TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_Mono.Data.Tds_test.dll",                                TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_Mono.Posix_test.dll",                                   TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_Mono.Security_test.dll",                                TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.ComponentModel.Composition_xunit-test.dll",      TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_System.ComponentModel.DataAnnotations_test.dll",        TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Core_test.dll",                                  TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Core_xunit-test.dll",                            TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_System.Data_test.dll",                                  TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Data_xunit-test.dll",                            TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_System.IO.Compression.FileSystem_test.dll",             TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.IO.Compression_test.dll",                        TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Json_test.dll",                                  TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Json_xunit-test.dll",                            TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_System.Net.Http_test.dll",                              TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Net.Http.FunctionalTests_xunit-test.dll",        TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_System.Net.Http.UnitTests_xunit-test.dll",              TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_System.Numerics_test.dll",                              TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Numerics_xunit-test.dll",                        TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_System.Runtime.CompilerServices.Unsafe_xunit-test.dll", TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_System.Runtime.Serialization_test.dll",                 TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Runtime.Serialization_xunit-test.dll",           TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_System.Security_test.dll",                              TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Security_xunit-test.dll",                        TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_System.ServiceModel.Web_test.dll",                      TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.ServiceModel_test.dll",                          TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Transactions_test.dll",                          TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Web.Services_test.dll",                          TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Xml.Linq_test.dll",                              TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Xml.Linq_xunit-test.dll",                        TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_System.Xml_test.dll",                                   TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System.Xml_xunit-test.dll",                             TestAssemblyType.XUnit),
			new TestAssembly ("monodroid_System_test.dll",                                       TestAssemblyType.NUnit),
			new TestAssembly ("monodroid_System_xunit-test.dll",                                 TestAssemblyType.XUnit),
			new TestAssembly ("System.Reflection.TestModule.dll",                                TestAssemblyType.NUnit, excludeDebugSymbols: true),
			new TestAssembly ("TestLoadAssembly.dll",                                            TestAssemblyType.NUnit),

			// Mono.CSharp testsuite dynamically loads Microsoft.CSharp
			new TestAssembly ("Microsoft.CSharp.dll",                                            TestAssemblyType.Reference),

			// This is referenced by monodroid_corlib_xunit-test.dll
			new TestAssembly ("System.Runtime.CompilerServices.Unsafe.dll",                      TestAssemblyType.Reference, excludeDebugSymbols: true),

			// Satellite assemblies
			new TestAssembly (Path.Combine ("es-ES", "monodroid_corlib_test.resources.dll"),     TestAssemblyType.Satellite),
			new TestAssembly (Path.Combine ("nn-NO", "monodroid_corlib_test.resources.dll"),     TestAssemblyType.Satellite),

			// Other
			new TestAssembly ("nunitlite.dll",                                                   TestAssemblyType.TestRunner),
		};

		public static readonly Dictionary<string, string> FrameworkListVersionOverrides = new Dictionary <string, string> (StringComparer.Ordinal) {
			{ "System.Buffers", "4.0.99.0" },
			{ "System.Memory",  "4.0.99.0" }
		};

		public static readonly string FrameworkListRedist = "MonoAndroid";
		public static readonly string FrameworkListName   = "Xamarin.Android Base Class Libraries";

		public readonly List<MonoUtilityFile> UtilityFilesToInstall = new List<MonoUtilityFile> {
			new MonoUtilityFile ("mono-cil-strip.exe",                     targetName: "cil-strip.exe"),
			new MonoUtilityFile ("illinkanalyzer.exe",                     remap: true),
			new MonoUtilityFile ("mdoc.exe",                               remap: true),
			new MonoUtilityFile ("mono-symbolicate.exe",                   remap: true),
			new MonoUtilityFile ("mkbundle.exe",                           remap: true),
			new MonoUtilityFile ("monodoc.dll"),
			new MonoUtilityFile ("monodoc.dll.config",                     ignoreDebugInfo: true),
			new MonoUtilityFile ("mono-api-html.exe",                      remap: true),
			new MonoUtilityFile ("mono-api-info.exe",                      remap: true),
		};

		/// <summary>
		///   These are source:destination relative paths for source files we are to install. Sources are relative to
		///   the mono submodule directory or absolute, while destinations are relative to <see
		///   cref="Configurables.Paths.InstallMSBuildDir" /> or absolute. Note that the destination must include the
		///   file name.
		/// </summary>
		public readonly List<RuntimeFile> RuntimeFilesToInstall = new List<RuntimeFile> {
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => Path.Combine (GetAndroidInputRootDir (runtime), "share", "mono-2.0", "mono", "eglib", "eglib-config.h"),
				destinationCreator:(Runtime runtime) => Path.Combine (Configurables.Paths.OutputIncludeDir, runtime.PrefixedName, "eglib", "eglib-config.h"),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime)
			),

			new RuntimeFile (
				sourceCreator: (Runtime runtime) => Path.Combine (Configurables.Paths.MonoProfileDir, "Consts.cs"),
				destinationCreator: (Runtime runtime) => Path.Combine (Configurables.Paths.OutputIncludeDir, "Consts.cs"),
				strip: false,
				shared: true
			),

			 // Stripped runtime
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetRuntimeOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetRuntimeOutputDestinationPath (runtime, debug: false),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime),
				type: RuntimeFileType.StrippableBinary
			),

			 // Unstripped runtime
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetRuntimeOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetRuntimeOutputDestinationPath (runtime, debug: true),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime),
				type: RuntimeFileType.StrippableBinary,
				strip: false
			),

			// Stripped libmono-native for Mac, copied from libmono-native-compat
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetMonoNativeOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetMonoNativeOutputDestinationPath (runtime, debug: false),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime) || !IsAbi (runtime, AbiNames.HostJit.Darwin),
				type: RuntimeFileType.StrippableBinary
			),

			// Unstripped libmono-native for Mac, copied from libmono-native-compat
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetMonoNativeOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetMonoNativeOutputDestinationPath (runtime, debug: true),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime) || !IsAbi (runtime, AbiNames.HostJit.Darwin),
				type: RuntimeFileType.StrippableBinary,
				strip: false
			),

			// Stripped libmono-native for everything except: Mac, Win32, Win64, cross runtimes
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetMonoNativeOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetMonoNativeOutputDestinationPath (runtime, debug: false),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime) || IsAbi (runtime, AbiNames.HostJit.Darwin, AbiNames.HostJit.Win32, AbiNames.HostJit.Win64),
				type: RuntimeFileType.StrippableBinary
			),

			// Untripped libmono-native for everything except: Mac, Win32, Win64, cross runtimes
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetMonoNativeOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetMonoNativeOutputDestinationPath (runtime, debug: true),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime) || IsAbi (runtime, AbiNames.HostJit.Darwin, AbiNames.HostJit.Win32, AbiNames.HostJit.Win64),
				type: RuntimeFileType.StrippableBinary,
				strip: false
			),

			// Unstripped host mono binary
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => Path.Combine (GetAndroidInputRootDir (runtime), "bin", "mono"),
				destinationCreator: (Runtime runtime) => Path.Combine (runtime.Name, "mono"),
				shouldSkip: (Runtime runtime) => !IsRuntimeType<MonoHostRuntime> (runtime) || IsAbi (runtime, AbiNames.HostJit.Win32, AbiNames.HostJit.Win64),
				type: RuntimeFileType.StrippableBinary,
				strip: false
			),

			// Stripped cross runtime
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetCrossRuntimeOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetCrossRuntimeOutputDestinationPath (runtime, debug: false),
				shouldSkip: (Runtime runtime) => !IsRuntimeType<MonoCrossRuntime> (runtime),
				type: RuntimeFileType.StrippableBinary
			),

			// Untripped cross runtime
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetCrossRuntimeOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetCrossRuntimeOutputDestinationPath (runtime, debug: true),
				shouldSkip: (Runtime runtime) => !IsRuntimeType<MonoCrossRuntime> (runtime),
				type: RuntimeFileType.StrippableBinary,
				strip: false
			),

			// Stripped profiler
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetProfilerOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetProfilerOutputDestinationPath (runtime, debug: false),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime) || String.IsNullOrEmpty (runtime.As<MonoRuntime>().OutputProfilerFilename),
				type: RuntimeFileType.StrippableBinary
			),

			// Unstripped profiler
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetProfilerOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetProfilerOutputDestinationPath (runtime, debug: true),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime) || String.IsNullOrEmpty (runtime.As<MonoRuntime>().OutputProfilerFilename),
				type: RuntimeFileType.StrippableBinary,
				strip: false
			),

			// Stripped AOT profiler
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetAotProfilerOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetAotProfilerOutputDestinationPath (runtime, debug: false),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime) || String.IsNullOrEmpty (runtime.As<MonoRuntime>().OutputAotProfilerFilename),
				type: RuntimeFileType.StrippableBinary
			),

			// Unstripped AOT profiler
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetAotProfilerOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetAotProfilerOutputDestinationPath (runtime, debug: true),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime) || String.IsNullOrEmpty (runtime.As<MonoRuntime>().OutputAotProfilerFilename),
				type: RuntimeFileType.StrippableBinary,
				strip: false
			),

			// Stripped BTLS
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetMonoBtlsOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetMonoBtlsOutputDestinationPath (runtime, debug: false),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime) || String.IsNullOrEmpty (runtime.As<MonoRuntime>().OutputMonoBtlsFilename),
				type: RuntimeFileType.StrippableBinary
			),

			// Unstripped BTLS
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetMonoBtlsOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetMonoBtlsOutputDestinationPath (runtime, debug: true),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime) || String.IsNullOrEmpty (runtime.As<MonoRuntime>().OutputMonoBtlsFilename),
				type: RuntimeFileType.StrippableBinary,
				strip: false
			),

			// Stripped MonoPosixHelper
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetMonoPosixHelperOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetMonoPosixHelperOutputDestinationPath (runtime, debug: false),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime) || String.IsNullOrEmpty (runtime.As<MonoRuntime>().OutputMonoPosixHelperFilename),
				type: RuntimeFileType.StrippableBinary
			),

			// Unstripped MonoPosixHelper
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => GetMonoPosixHelperOutputSourcePath (runtime),
				destinationCreator: (Runtime runtime) => GetMonoPosixHelperOutputDestinationPath (runtime, debug: true),
				shouldSkip: (Runtime runtime) => !IsHostOrTargetRuntime (runtime) || String.IsNullOrEmpty (runtime.As<MonoRuntime>().OutputMonoPosixHelperFilename),
				type: RuntimeFileType.StrippableBinary,
				strip: false
			),

			// LLVM opt
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => Path.Combine (GetLlvmOutputSourcePath(runtime), $"opt{runtime.As<LlvmRuntime>().ExeSuffix}"),
				destinationCreator: (Runtime runtime) => Path.Combine (GetLlvmOutputDestinationPath (runtime), $"opt{runtime.As<LlvmRuntime>().ExeSuffix}"),
				shouldSkip: (Runtime runtime) => !IsRuntimeType<LlvmRuntime> (runtime) || !runtime.As<LlvmRuntime>().InstallBinaries || (Context.IsWindows && !IsWindowsRuntime (runtime)),
				type: RuntimeFileType.StrippableBinary
			),

			// LLVM llc
			new RuntimeFile (
				sourceCreator: (Runtime runtime) => Path.Combine (GetLlvmOutputSourcePath(runtime), $"llc{runtime.As<LlvmRuntime>().ExeSuffix}"),
				destinationCreator: (Runtime runtime) => Path.Combine (GetLlvmOutputDestinationPath (runtime), $"llc{runtime.As<LlvmRuntime>().ExeSuffix}"),
				shouldSkip: (Runtime runtime) => !IsRuntimeType<LlvmRuntime> (runtime) || !runtime.As<LlvmRuntime>().InstallBinaries || (Context.IsWindows && !IsWindowsRuntime (runtime)),
				type: RuntimeFileType.StrippableBinary
			)
		};

		// If some assemblies don't exist in the Designer BCL set, list them here
		static readonly Dictionary<string, (BclFileType Type, BclFileTarget Target)> DesignerIgnoreFiles = new Dictionary<string, (BclFileType Type, BclFileTarget Target)> {
			{ "Mono.Btls.Interface.dll", (Type: BclFileType.ProfileAssembly, Target: BclFileTarget.DesignerWindows) },
		};

		static readonly List<BundleItem> UnixBundleItems = new List<BundleItem> {
			new BundleItem (
				sourcePath: Path.Combine (Configurables.Paths.InstallMSBuildDir, "libzip.dll"),
				shouldInclude: (Context ctx) => ctx.WindowsJitAbisEnabled
			),

			new BundleItem (
				sourcePath: Path.Combine (Configurables.Paths.InstallMSBuildDir, "x64", "libzip.dll"),
				shouldInclude: (Context ctx) => ctx.WindowsJitAbisEnabled
			),
		};

		static readonly List<BundleItem> MacOSBundleItems = new List<BundleItem> {
			new BundleItem (
				sourcePath: Path.Combine (Configurables.Paths.InstallMSBuildDir, "libzip.5.0.dylib")
			),
		};

		public List<BundleItem> BundleItems {
			get {
				if (bundleItems != null)
					return bundleItems;

				bundleItems = new List<BundleItem> ();
				bundleItems.AddRange (BclFilesToInstall);
				bundleItems.AddRange (DesignerHostBclFilesToInstall);
				bundleItems.AddRange (DesignerWindowsBclFilesToInstall);
				bundleItems.AddRange (TestAssemblies);
				bundleItems.AddRange (RuntimeFilesToInstall);
				bundleItems.AddRange (UtilityFilesToInstall);
				bundleItems.Add (Configurables.Paths.FrameworkListInstallPath);
				bundleItems.Add (Configurables.Paths.BCLTestsArchivePath);

				AddUnixBundleItems (bundleItems);
				AddMacOSBundleItems (bundleItems);

				return bundleItems;
			}
		}

		partial void AddUnixBundleItems (List<BundleItem> bundleItems);
		partial void AddMacOSBundleItems (List<BundleItem> bundleItems);

		/// <summary>
		///   List of directories we'll be installing to. All the directories will be removed recursively before
		///   installation starts. This is to ensure that no artifacts from previous builds remain.
		/// </summary>
		public readonly List<string> OutputDirectories = new List<string> {
			Configurables.Paths.BCLTestsDestDir,
			Configurables.Paths.InstallMSBuildDir,
			Configurables.Paths.InstallBCLFrameworkDir,
			Configurables.Paths.OutputIncludeDir,
		};

		static string GetMonoUtilitySourcePath (string utilityName)
		{
			return Path.Combine (Configurables.Paths.MonoProfileToolsDir, utilityName);
		}

		static string GetLlvmOutputSourcePath (Runtime runtime)
		{
			var llvmRuntime = EnsureRuntimeType<LlvmRuntime> (runtime, "LLVM");
			return Path.Combine (GetLlvmInputDir (runtime), "bin");
		}

		static string GetLlvmOutputDestinationPath (Runtime runtime)
		{
			var llvmRuntime = EnsureRuntimeType<LlvmRuntime> (runtime, "LLVM");
			return llvmRuntime.InstallPath;
		}

		static string GetMonoPosixHelperOutputSourcePath (Runtime runtime)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"{monoRuntime.OutputMonoPosixHelperFilename}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetMonoPosixHelperOutputDestinationPath (Runtime runtime, bool debug)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetRuntimeOutputDir (runtime), $"{monoRuntime.OutputMonoPosixHelperFilename}{GetDebugInfix (debug)}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetMonoBtlsOutputSourcePath (Runtime runtime)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"{monoRuntime.OutputMonoBtlsFilename}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetMonoBtlsOutputDestinationPath (Runtime runtime, bool debug)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetRuntimeOutputDir (runtime), $"{monoRuntime.OutputMonoBtlsFilename}{GetDebugInfix (debug)}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetAotProfilerOutputSourcePath (Runtime runtime)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"{monoRuntime.OutputAotProfilerFilename}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetAotProfilerOutputDestinationPath (Runtime runtime, bool debug)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetRuntimeOutputDir (runtime), $"{monoRuntime.OutputAotProfilerFilename}{GetDebugInfix (debug)}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetProfilerOutputSourcePath (Runtime runtime)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"{monoRuntime.OutputProfilerFilename}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetProfilerOutputDestinationPath (Runtime runtime, bool debug)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetRuntimeOutputDir (runtime), $"{monoRuntime.OutputProfilerFilename}{GetDebugInfix (debug)}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetCrossRuntimeOutputSourcePath (Runtime runtime)
		{
			var crossRuntime = EnsureRuntimeType<MonoCrossRuntime> (runtime, "cross compilation");
			return Path.Combine (GetAndroidInputRootDir (runtime), "bin", $"{crossRuntime.ExePrefix}mono-sgen{crossRuntime.ExeSuffix}");
		}

		static string GetCrossRuntimeOutputDestinationPath (Runtime runtime, bool debug)
		{
			var crossRuntime = EnsureRuntimeType<MonoCrossRuntime> (runtime, "cross compilation");
			string runtimeName = $"{crossRuntime.CrossMonoName}{GetDebugInfix (debug)}{crossRuntime.ExeSuffix}";
			if (String.IsNullOrEmpty (crossRuntime.InstallPath))
				return runtimeName;

			return Path.Combine (crossRuntime.InstallPath, runtimeName);
		}

		static string GetRuntimeOutputSourcePath (Runtime runtime)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"{monoRuntime.OutputRuntimeFilename}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetRuntimeOutputDestinationPath (Runtime runtime, bool debug)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetRuntimeOutputDir (runtime), $"{monoRuntime.OutputRuntimeFilename}{GetDebugInfix (debug)}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetMonoNativeOutputSourcePath (Runtime runtime)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			if (IsAbi (runtime, AbiNames.HostJit.Darwin))
				return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"libmono-native-compat{monoRuntime.NativeLibraryExtension}");

			return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"libmono-native{monoRuntime.NativeLibraryExtension}");
		}

		static string GetMonoNativeOutputDestinationPath (Runtime runtime, bool debug)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetRuntimeOutputDir (runtime), $"libmono-native{GetDebugInfix (debug)}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetDebugInfix (bool debug)
		{
			return debug ? Configurables.Defaults.DebugBinaryInfix : String.Empty;
		}

		static bool IsHostOrTargetRuntime (Runtime runtime)
		{
			return IsRuntimeType<MonoJitRuntime> (runtime) || IsRuntimeType<MonoHostRuntime> (runtime);
		}

		static T EnsureRuntimeType<T> (Runtime runtime, string typeName) where T: Runtime
		{
			var ret = runtime.As<T> ();
			if (ret == null)
				throw new InvalidOperationException ($"Runtime {runtime.Name} is not a {typeName} runtime");

			return ret;
		}

		static bool IsRuntimeType <T> (Runtime runtime) where T: Runtime
		{
			return runtime.As<T>() != null;
		}

		static bool IsWindowsRuntime (Runtime runtime)
		{
			return String.Compare (runtime.ExeSuffix, Configurables.Defaults.WindowsExecutableSuffix, StringComparison.Ordinal) == 0;
		}

		static bool IsAbi (Runtime runtime, string abiName, params string[] furtherAbiNames)
		{
			if (ExpectedAbi (abiName))
				return true;

			if (furtherAbiNames == null)
				return false;

			foreach (string a in furtherAbiNames) {
				if (ExpectedAbi (a))
					return true;
			}

			return false;

			bool ExpectedAbi (string abi)
			{
				if (String.IsNullOrEmpty (abi))
					return false;

				return String.Compare (abi, runtime.Name ?? String.Empty, StringComparison.Ordinal) == 0;
			}
		}

		static string GetLlvmInputDir (Runtime runtime)
		{
			return GetLlvmInputRootDir (runtime);
		}

		static string GetLlvmInputRootDir (Runtime runtime)
		{
			return Path.Combine (Configurables.Paths.MonoSDKSRelativeOutputDir, $"llvm-{runtime.PrefixedName}");
		}

		static string GetAndroidInputLibDir (Runtime runtime)
		{
			return Path.Combine (GetAndroidInputRootDir (runtime), "lib");
		}

		static string GetAndroidInputRootDir (Runtime runtime)
		{
			return Path.Combine (Configurables.Paths.MonoSDKSRelativeOutputDir, $"android-{runtime.PrefixedName}-{Configurables.Defaults.MonoSdksConfiguration}");
		}

		static string GetRuntimeOutputDir (Runtime runtime)
		{
			return Path.Combine (Configurables.Paths.RuntimeInstallRelativeLibDir, runtime.PrefixedName);
		}

		static bool IsLlvmRuntimeEnabled (Context ctx, string llvmAbi)
		{
			bool enabled = false;
			bool windows = ctx.IsLlvmWindowsAbi (llvmAbi);
			bool is64Bit = ctx.Is64BitLlvmAbi (llvmAbi);

			HashSet<string> targets;
			if (windows)
				targets = is64Bit ? AbiNames.All64BitWindowsAotAbis : AbiNames.All32BitWindowsAotAbis;
			else
				targets = is64Bit ? AbiNames.All64BitHostAotAbis : AbiNames.All32BitHostAotAbis;

			foreach (string target in targets) {
				if (Context.Instance.IsTargetAotAbiEnabled (target)) {
					enabled = true;
					break;
				}
			}

			return enabled && (!is64Bit || Context.Instance.OS.Is64Bit);
		}

		public Runtimes ()
		{
			Context c = ctx;
			foreach (Runtime runtime in Items) {
				runtime.Init (c);
			}

			DesignerHostBclFilesToInstall = new List<BclFile> ();
			DesignerWindowsBclFilesToInstall = new List<BclFile> ();

			PopulateDesignerBclFiles (DesignerHostBclFilesToInstall, DesignerWindowsBclFilesToInstall);
		}

		partial void PopulateDesignerBclFiles (List<BclFile> designerHostBclFilesToInstall, List<BclFile> designerWindowsBclFilesToInstall);

		List<BclFile> BclToDesigner (BclFileTarget ignoreForTarget)
		{
			return BclFilesToInstall.Where (bf => ShouldIncludeDesignerBcl (bf)).Select (bf => new BclFile (bf.Name, bf.Type, excludeDebugSymbols: true, version: bf.Version, target: ignoreForTarget)).ToList ();

			bool ShouldIncludeDesignerBcl (BclFile bf)
			{
				if (DesignerIgnoreFiles == null || !DesignerIgnoreFiles.TryGetValue (bf.Name, out (BclFileType Type, BclFileTarget Target) bft)) {
					return true;
				}

				if (bf.Type != bft.Type || bft.Target != ignoreForTarget)
					return true;

				Log.Instance.DebugLine ($"BCL file {bf.Name} will NOT be included in the installed Designer BCL files ({ignoreForTarget})");
				return false;
			}
		}

		List<BundleItem> bundleItems;
	}
}
