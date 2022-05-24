using System;

namespace Xamarin.ProjectTools
{
	public class AndroidItem
	{
		public class AndroidResource : BuildItem
		{
			public AndroidResource (string include)
				: this (() => include)
			{
			}
			public AndroidResource (Func<string> include)
				: base (AndroidBuildActions.AndroidResource, include)
			{
			}
		}
		public class AndroidAsset : BuildItem
		{
			public AndroidAsset (string include)
				: this (() => include)
			{
			}
			public AndroidAsset (Func<string> include)
				: base (AndroidBuildActions.AndroidAsset, include)
			{
			}
		}
		public class AndroidEnvironment : BuildItem
		{
			public AndroidEnvironment (string include)
				: this (() => include)
			{
			}
			public AndroidEnvironment (Func<string> include)
				: base (AndroidBuildActions.AndroidEnvironment, include)
			{
			}
		}
		public class AndroidLibrary : BuildItem
		{
			public AndroidLibrary (string include)
				: this (() => include)
			{
			}
			public AndroidLibrary (Func<string> include)
				: base (AndroidBuildActions.AndroidLibrary, include)
			{
			}
		}
		public class AndroidLintConfig : BuildItem 
		{
			public AndroidLintConfig (string include)
				: this (() => include)
			{
			}
			public AndroidLintConfig (Func<string> include)
				: base (AndroidBuildActions.AndroidLintConfig, include)
			{
			}
		}
		public class _AndroidRemapMembers : BuildItem
		{
			public _AndroidRemapMembers (string include)
				: this (() => include)
			{
			}
			public _AndroidRemapMembers (Func<string> include)
				: base (AndroidBuildActions._AndroidRemapMembers, include)
			{
			}
		}
		public class EmbeddedJar : BuildItem
		{
			public EmbeddedJar (string include)
				: this (() => include)
			{
			}
			public EmbeddedJar (Func<string> include)
				: base (AndroidBuildActions.EmbeddedJar, include)
			{
			}
		}
		public class EmbeddedReferenceJar : BuildItem
		{
			public EmbeddedReferenceJar (string include)
				: this (() => include)
			{
			}
			public EmbeddedReferenceJar (Func<string> include)
				: base (AndroidBuildActions.EmbeddedReferenceJar, include)
			{
			}
		}
		public class ProguardConfiguration : BuildItem
		{
			public ProguardConfiguration (string include)
				: this (() => include)
			{
			}
			public ProguardConfiguration (Func<string> include)
				: base (AndroidBuildActions.ProguardConfiguration, include)
			{
			}
		}
		public class TransformFile : BuildItem
		{
			public TransformFile (string include)
				: this (() => include)
			{
			}
			public TransformFile (Func<string> include)
				: base (AndroidBuildActions.TransformFile, include)
			{
			}
		}
		public class LibraryProjectZip : BuildItem
		{
			public LibraryProjectZip (string include)
				: this (() => include)
			{
			}
			public LibraryProjectZip (Func<string> include)
				: base (AndroidBuildActions.LibraryProjectZip, include)
			{
			}
		}
		public class EmbeddedNativeLibrary : BuildItem {
			public EmbeddedNativeLibrary (string include)
				: this (() => include)
			{
			}
			public EmbeddedNativeLibrary (Func<string> include)
				: base (AndroidBuildActions.EmbeddedNativeLibrary, include)
			{
			}
		}
		public class AndroidNativeLibrary : BuildItem {
			public AndroidNativeLibrary (string include)
				: this (() => include)
			{
			}
			public AndroidNativeLibrary (Func<string> include)
				: base (AndroidBuildActions.AndroidNativeLibrary, include)
			{
			}
		}
		public class AndroidJavaSource : BuildItem {
			public AndroidJavaSource (string include)
				: this (() => include)
			{
			}
			public AndroidJavaSource (Func<string> include)
				: base (AndroidBuildActions.AndroidJavaSource, include)
			{
			}
		}
		public class InputJar : BuildItem
		{
			public InputJar (string include)
				: this (() => include)
			{
			}
			public InputJar (Func<string> include)
				: base (AndroidBuildActions.InputJar, include)
			{
			}
		}
		public class AndroidAarLibrary : BuildItem {
			public AndroidAarLibrary (string include)
				: this (() => include)
			{
			}
			public AndroidAarLibrary (Func<string> include)
				: base (AndroidBuildActions.AndroidAarLibrary, include)
			{
			}
		}
	}
}
