using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Contains constant values for Android-specific MSBuild item types (build actions).
	/// These constants provide a centralized way to reference Android build actions
	/// used in Xamarin.Android projects for various Android-specific file types.
	/// </summary>
	/// <remarks>
	/// These build actions are specific to Xamarin.Android and determine how Android
	/// assets, resources, libraries, and other Android-specific files are processed
	/// during the build. Used in conjunction with <see cref="BuildActions"/> for
	/// complete build item type coverage.
	/// </remarks>
	/// <seealso cref="BuildActions"/>
	/// <seealso cref="BuildItem"/>
	/// <seealso cref="AndroidItem"/>
	public static class AndroidBuildActions
	{
		/// <summary>
		/// Build action for Android resource files (layout, drawable, values, etc.).
		/// </summary>
		public const string AndroidResource = "AndroidResource";

		/// <summary>
		/// Build action for Android asset files stored in the assets folder.
		/// </summary>
		public const string AndroidAsset = "AndroidAsset";

		/// <summary>
		/// Build action for Android AAR library files.
		/// </summary>
		public const string AndroidAarLibrary = "AndroidAarLibrary";

		/// <summary>
		/// Build action for Android environment variable files.
		/// </summary>
		public const string AndroidEnvironment = "AndroidEnvironment";

		/// <summary>
		/// Build action for Android interface description language files.
		/// </summary>
		public const string AndroidInterfaceDescription = "AndroidInterfaceDescription";

		/// <summary>
		/// Build action for Java source files to be compiled as part of the Android project.
		/// </summary>
		public const string AndroidJavaSource = "AndroidJavaSource";

		/// <summary>
		/// Build action for Java library JAR files.
		/// </summary>
		public const string AndroidJavaLibrary = "AndroidJavaLibrary";

		/// <summary>
		/// Build action for Android library project references.
		/// </summary>
		public const string AndroidLibrary = "AndroidLibrary";

		/// <summary>
		/// Build action for Maven-based Android library dependencies.
		/// </summary>
		public const string AndroidMavenLibrary = "AndroidMavenLibrary";

		/// <summary>
		/// Build action for Android Lint configuration files.
		/// </summary>
		public const string AndroidLintConfig = "AndroidLintConfig";

		/// <summary>
		/// Build action for native library files (.so) for Android.
		/// </summary>
		public const string AndroidNativeLibrary = "AndroidNativeLibrary";

		/// <summary>
		/// Represents a native JNI library which should not be preloaded at application startup.
		/// </summary>
		public const string AndroidNativeLibraryNoJniPreload = "AndroidNativeLibraryNoJniPreload";

		/// <summary>
		/// Internal build action for Android member remapping metadata.
		/// </summary>
		public const string _AndroidRemapMembers = "_AndroidRemapMembers";

		/// <summary>
		/// Build action for ProGuard configuration files.
		/// </summary>
		public const string ProguardConfiguration = "ProguardConfiguration";

		/// <summary>
		/// Build action for XML transform files.
		/// </summary>
		public const string TransformFile = "TransformFile";

		/// <summary>
		/// Build action for input JAR files in Java binding projects.
		/// </summary>
		public const string InputJar = "InputJar";

		/// <summary>
		/// Build action for reference JAR files in Java binding projects.
		/// </summary>
		public const string ReferenceJar = "ReferenceJar";

		/// <summary>
		/// Build action for embedded JAR files in Java binding projects.
		/// </summary>
		public const string EmbeddedJar = "EmbeddedJar";

		/// <summary>
		/// Build action for embedded native library files.
		/// </summary>
		public const string EmbeddedNativeLibrary = "EmbeddedNativeLibrary";

		/// <summary>
		/// Build action for embedded reference JAR files in Java binding projects.
		/// </summary>
		public const string EmbeddedReferenceJar = "EmbeddedReferenceJar";

		/// <summary>
		/// Build action for Android library project ZIP files.
		/// </summary>
		public const string LibraryProjectZip = "LibraryProjectZip";

		/// <summary>
		/// Build action for Android library project properties files.
		/// </summary>
		public const string LibraryProjectProperties = "LibraryProjectProperties";
	}
}
