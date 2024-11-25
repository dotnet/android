using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	public partial class BuildTest : BaseTest
	{
		static readonly object [] DotNetBuildSource = new object [] {
			new object [] {
				/* runtimeIdentifiers */ "android-arm",
				/* isRelease */          false,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm",
				/* isRelease */          false,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm64",
				/* isRelease */          false,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-x86",
				/* isRelease */          false,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-x64",
				/* isRelease */          false,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm",
				/* isRelease */          true,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm",
				/* isRelease */          true,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm",
				/* isRelease */          true,
				/* aot */                true,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm",
				/* isRelease */          true,
				/* aot */                true,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm64",
				/* isRelease */          true,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm;android-arm64;android-x86;android-x64",
				/* isRelease */          false,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm;android-arm64;android-x86;android-x64",
				/* isRelease */          false,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm;android-arm64;android-x86",
				/* isRelease */          true,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm;android-arm64;android-x86;android-x64",
				/* isRelease */          true,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm;android-arm64;android-x86;android-x64",
				/* isRelease */          true,
				/* aot */                false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm;android-arm64;android-x86;android-x64",
				/* isRelease */          true,
				/* aot */                true,
			},
		};

#pragma warning disable 414
		static object [] RuntimeChecks () => new object [] {
			new object[] {
				/* supportedAbi */     "armeabi-v7a",
				/* debugSymbols */     true ,
				/* optimize */         true ,
				/* embedassebmlies */  true ,
				/* expectedResult */   "release",
			},
			new object[] {
				/* supportedAbi */     "armeabi-v7a",
				/* debugSymbols */     true ,
				/* optimize */         true ,
				/* embedassebmlies */  false ,
				/* expectedResult */   TestEnvironment.CommercialBuildAvailable ? "debug" : "release",
			},
			new object[] {
				/* supportedAbi */     "armeabi-v7a",
				/* debugSymbols */     true ,
				/* optimize */         false ,
				/* embedassebmlies */  true ,
				/* expectedResult */   "debug",
			},
			new object[] {
				/* supportedAbi */     "armeabi-v7a",
				/* debugSymbols */     true ,
				/* optimize */         false ,
				/* embedassebmlies */  false ,
				/* expectedResult */   "debug",
			},
			new object[] {
				/* supportedAbi */     "armeabi-v7a",
				/* debugSymbols */     false ,
				/* optimize */         null ,
				/* embedassebmlies */  null ,
				/* expectedResult */   TestEnvironment.CommercialBuildAvailable ? "debug" : "release",
			},
		};
#pragma warning restore 414
	}
}
