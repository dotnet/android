using System;

namespace Xamarin.ProjectTools
{
	public static class KnownPackages
	{
		public static Package XamarinAndroidXWear = new Package {
			Id = "Xamarin.AndroidX.Wear",
			Version = "1.2.0.5"
		};
		public static Package XamarinForms = new Package {
			Id = "Xamarin.Forms",
			Version = "5.0.0.2622",
		};
		public static Package XamarinFormsMaps = new Package {
			Id = "Xamarin.Forms.Maps",
			Version = "5.0.0.2622",
		};
		public static Package AndroidXConstraintLayout = new Package {
			Id = "Xamarin.AndroidX.ConstraintLayout",
			Version = "2.1.4.9",
		};
		public static Package AndroidXAppCompat = new Package {
			Id = "Xamarin.AndroidX.AppCompat",
			Version = "1.6.1.6",
		};
		public static Package AndroidXBrowser = new Package {
			Id = "Xamarin.AndroidX.Browser",
			Version = "1.5.0.3",
		};
		public static Package AndroidXLegacySupportV4 = new Package {
			Id = "Xamarin.AndroidX.Legacy.Support.V4",
			Version = "1.0.0.22",
		};
		public static Package AndroidXAppCompatResources = new Package {
			Id = "Xamarin.AndroidX.AppCompat.AppCompatResources",
			Version = "1.6.1.7",
		};
		public static Package AndroidXLifecycleLiveData = new Package {
			Id = "Xamarin.AndroidX.Lifecycle.LiveData",
			Version = "2.6.2.3",
		};
		public static Package AndroidXWorkRuntime = new Package {
			Id = "Xamarin.AndroidX.Work.Runtime",
			Version = "2.9.0",
		};
		public static Package XamarinGoogleAndroidMaterial = new Package {
			Id = "Xamarin.Google.Android.Material",
			Version = "1.10.0.2",
		};
		public static Package CocosSharp_PCL_Shared_1_5_0_0 = new Package {
			Id = "CocosSharp.PCL.Shared",
			Version = "1.5.0.0",
			TargetFramework ="MonoAndroid10",
			References =  {
				new BuildItem.Reference ("box2d") {
					MetadataValues = "HintPath=..\\packages\\CocosSharp.PCL.Shared.1.5.0.0\\lib\\MonoAndroid10\\box2d.dll"
				},
				new BuildItem.Reference ("CocosSharp") {
					MetadataValues = "HintPath=..\\packages\\CocosSharp.PCL.Shared.1.5.0.0\\lib\\MonoAndroid10\\CocosSharp.dll"
				},
				new BuildItem.Reference ("MonoGame.Framework") {
					MetadataValues = "HintPath=..\\packages\\CocosSharp.PCL.Shared.1.5.0.0\\lib\\MonoAndroid10\\MonoGame.Framework.dll"
				},
			}
		};
		public static Package MonoGame_Framework_Android_3_4_0_459 = new Package {
			Id = "MonoGame.Framework.Android",
			Version = "3.4.0.459",
			TargetFramework = "MonoAndroid10",
			References = {
				new BuildItem.Reference ("MonoGame.Framework") {
					MetadataValues = "HintPath=..\\packages\\MonoGame.Framework.Android.3.4.0.459\\lib\\MonoAndroid\\MonoGame.Framework.dll"
				},
			}
		};
		public static Package FSharp_Core_Latest = new Package {
			Id = "FSharp.Core",
			Version = "4.7.1",
			TargetFramework = "netstandard2.0",
			References = {
				new BuildItem.Reference ("mscorlib"),
				new BuildItem.Reference ("FSharp.Core") {
					MetadataValues = "HintPath=..\\packages\\FSharp.Core.4.7.1\\lib\\netstandard2.0\\FSharp.Core.dll"
				},
			}
		};
		public static Package Xamarin_Android_FSharp_ResourceProvider = new Package {
			Id = "Xamarin.Android.FSharp.ResourceProvider",
			Version = "1.0.1",
			TargetFramework = "monoandroid81",
			References = {
				new BuildItem.Reference ("Xamarin.Android.FSharp.ResourceProvider.Runtime") {
					MetadataValues = "HintPath=..\\packages\\Xamarin.Android.FSharp.ResourceProvider.1.0.1\\lib\\monoandroid81\\Xamarin.Android.FSharp.ResourceProvider.Runtime.dll"
				},
			}
		};
		public static Package SQLitePCLRaw_Core = new Package {
			Id = "SQLitePCLRaw.core",
			Version = "1.1.8",
			TargetFramework = "monoandroid71",
			References = {
				new BuildItem.Reference("SQLitePCL") {
					MetadataValues = "HintPath=..\\packages\\SQLitePCLRaw.core.1.1.8\\lib\\MonoAndroid\\SQLitePCLRaw.core.dll"
				}
			}
		};
		public static Package Microsoft_Azure_EventHubs = new Package {
			Id = "Microsoft.Azure.EventHubs",
			Version = "2.2.1",
			TargetFramework = "netstandard2.0",
			References = {
				new BuildItem.Reference("Microsoft.Azure.EventHubs") {
					MetadataValues = "HintPath=..\\packages\\Microsoft.Azure.EventHubs.2.2.1\\lib\\netstandard2.0\\Microsoft.Azure.EventHubs.dll"
				}
			}
		};
		public static Package PCLCrypto_Alpha = new Package {
			Id = "PCLCrypto",
			Version = "2.1.17-alpha-g5b1e8dff8c",
			TargetFramework = "monoandroid23",
			References = {
				new BuildItem.Reference("PCLCrypto") {
					MetadataValues = "HintPath=..\\packages\\PCLCrypto.2.1.17-alpha-g5b1e8dff8c\\lib\\monoandroid23\\PCLCrypto.dll"
				}
			}
		};
		public static Package Xamarin_Build_Download = new Package {
			Id = "Xamarin.Build.Download",
			Version = "0.11.4",
		};
		public static Package NuGet_Build_Packaging = new Package {
			Id = "NuGet.Build.Packaging",
			Version = "0.2.2",
		};
		public static Package Xamarin_GooglePlayServices_Base = new Package {
			Id = "Xamarin.GooglePlayServices.Base",
			Version = "117.6.0.2",
		};
		public static Package Xamarin_GooglePlayServices_Basement = new Package {
			Id = "Xamarin.GooglePlayServices.Basement",
			Version = "117.6.0.3",
		};
		public static Package Xamarin_GooglePlayServices_Tasks = new Package {
			Id = "Xamarin.GooglePlayServices.Tasks",
			Version = "117.2.1.2",
		};
		public static Package Xamarin_GooglePlayServices_Maps = new Package {
			Id = "Xamarin.GooglePlayServices.Maps",
			Version = "117.0.1.2",
		};
		public static Package Xamarin_Kotlin_StdLib_Common = new Package {
			Id = "Xamarin.Kotlin.Stdlib.Common",
			Version = "1.6.20.1"
		};
		public static Package Xamarin_Kotlin_Reflect = new Package {
			Id = "Xamarin.Kotlin.Reflect",
			Version = "1.9.10.2"
		};
		public static Package Acr_UserDialogs = new Package {
			Id = "Acr.UserDialogs",
			Version = "8.0.1",
		};
		public static Package CircleImageView = new Package {
			Id = "Refractored.Controls.CircleImageView",
			Version = "1.0.1",
			TargetFramework = "MonoAndroid10",
			References = {
				new BuildItem.Reference ("Refractored.Controls.CircleImageView") {
					MetadataValues = "HintPath=..\\packages\\Refractored.Controls.CircleImageView.1.0.1\\lib\\MonoAndroid10\\Refractored.Controls.CircleImageView.dll"
				}
			},
		};
		public static Package Microsoft_Extensions_Http = new Package {
			Id = "Microsoft.Extensions.Http",
			Version = "2.2.0",
			TargetFramework = "netstandard2.0",
			References = {
				new BuildItem.Reference ("Microsoft.Extensions.Http") {
					MetadataValues = "HintPath=..\\packages\\Microsoft.Extensions.Http.2.2.0\\lib\\netstandard2.0\\Microsoft.Extensions.Http.dll"
				}
			},
		};
		public static Package Akavache = new Package {
			Id = "akavache",
			Version = "6.0.30",
			TargetFramework = "netstandard2.0",
		};
		/// <summary>
		/// A NuGet package that has an EmbeddedResource in PdfViewBinding.dll:
		/// __AndroidLibraryProjects__.zip\library_project_imports\res\.gitignore
		/// </summary>
		public static Package Xamarin_PdfView_Android = new Package {
			Id = "Xamarin.PdfView.Android",
			Version = "1.0.4",
			References = {
				new BuildItem.Reference ("Xamarin.PdfView.Android") {
					MetadataValues = "HintPath=..\\packages\\Xamarin.PdfView.Android.1.0.4\\lib\\PdfViewBinding.dll"
				}
			},
		};
		public static Package ZXing_Net_Mobile = new Package {
			Id = "ZXing.Net.Mobile",
			Version = "3.0.0-beta5", // version with AndroidX
			TargetFramework = "MonoAndroid10",
		};
		public static Package Xamarin_Legacy_OpenTK = new Package {
			Id = "Xamarin.Legacy.OpenTK",
			Version = "0.0.1-alpha",
			TargetFramework = "MonoAndroid10",
		};
		public static Package Xamarin_Legacy_NUnitLite = new Package {
			Id = "Xamarin.Legacy.NUnitLite",
			Version = "0.0.1-alpha",
			TargetFramework = "MonoAndroid10",
		};
		public static Package Xamarin_Jetbrains_Annotations = new Package {
			Id = "Xamarin.Jetbrains.Annotations",
			Version = "24.1.0.1",
		};
		public static Package Mono_AotProfiler_Android  = new Package {
			Id = "Mono.AotProfiler.Android",
			Version = "7.0.0-preview1",
		};
		public static Package SkiaSharp = new Package () {
			Id = "SkiaSharp",
			Version = "2.88.3",
		};
		public static Package SkiaSharp_Views = new Package () {
			Id = "SkiaSharp.Views",
			Version = "2.88.3",
		};
		public static Package Microsoft_Intune_Maui_Essentials_android = new Package {
			Id = "Microsoft.Intune.Maui.Essentials.android",
			Version = "10.0.0-beta2",
		};
	}
}

