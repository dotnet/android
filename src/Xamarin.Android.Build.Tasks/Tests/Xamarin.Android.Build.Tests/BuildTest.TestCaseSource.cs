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
				/* expectedResult */   CommercialBuildAvailable ? "debug" : "release",
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
				/* expectedResult */   CommercialBuildAvailable ? "debug" : "release",
			},
		};

		static object [] SequencePointChecks () => new object [] {
			new object[] {
				/* isRelease */          true,
				/* monoSymbolArchive */  true ,
				/* aotAssemblies */      false,
				/* debugSymbols */       true,
				/* expectedRuntime */    "release",
				/* usesAssemblyBlobs */  false,
			},
			new object[] {
				/* isRelease */          true,
				/* monoSymbolArchive */  true ,
				/* aotAssemblies */      true,
				/* debugSymbols */       true,
				/* expectedRuntime */    "release",
				/* usesAssemblyBlobs */  false,
			},
			new object[] {
				/* isRelease */          true,
				/* monoSymbolArchive */  false ,
				/* aotAssemblies */      false,
				/* debugSymbols */       true,
				/* expectedRuntime */    "release",
				/* usesAssemblyBlobs */  false,
			},
			new object[] {
				/* isRelease */          true,
				/* monoSymbolArchive */  false ,
				/* aotAssemblies */      false,
				/* debugSymbols */       true,
				/* expectedRuntime */    "release",
				/* usesAssemblyBlobs */  true,
			},
			new object[] {
				/* isRelease */          true,
				/* monoSymbolArchive */  false ,
				/* aotAssemblies */      true,
				/* debugSymbols */       false,
				/* expectedRuntime */    "release",
				/* usesAssemblyBlobs */  false,
			},
			new object[] {
				/* isRelease */          true,
				/* monoSymbolArchive */  true ,
				/* aotAssemblies */      true,
				/* debugSymbols */       false,
				/* expectedRuntime */    "release",
				/* usesAssemblyBlobs */  false,
			},
			new object[] {
				/* isRelease */          true,
				/* monoSymbolArchive */  true ,
				/* aotAssemblies */      true,
				/* debugSymbols */       false,
				/* expectedRuntime */    "release",
				/* usesAssemblyBlobs */  true,
			},
		};
#pragma warning restore 414
	}
}
