// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools
{
	/// <summary>
	/// Constants for environment variable names used by Android SDK tooling.
	/// See https://developer.android.com/tools/variables#envar
	/// </summary>
	internal static class EnvironmentVariableNames
	{
		/// <summary>
		/// The preferred variable for the Android SDK root directory.
		/// </summary>
		public const string AndroidHome = "ANDROID_HOME";

		/// <summary>
		/// Deprecated — use <see cref="AndroidHome"/> instead.
		/// Retained for reading existing environment configurations.
		/// </summary>
		[System.Obsolete ("ANDROID_SDK_ROOT is deprecated. Use ANDROID_HOME instead.")]
		public const string AndroidSdkRoot = "ANDROID_SDK_ROOT";

		/// <summary>
		/// The JDK installation directory.
		/// </summary>
		public const string JavaHome = "JAVA_HOME";

		/// <summary>
		/// Internal/override JDK path. Takes precedence over JAVA_HOME when set.
		/// </summary>
		public const string JiJavaHome = "JI_JAVA_HOME";

		/// <summary>
		/// Executable search paths.
		/// </summary>
		public const string Path = "PATH";

		/// <summary>
		/// Executable file extensions (Windows).
		/// </summary>
		public const string PathExt = "PATHEXT";
	}
}
