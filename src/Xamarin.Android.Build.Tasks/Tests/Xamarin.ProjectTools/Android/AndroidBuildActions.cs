using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	public static class AndroidBuildActions
	{
		public const string AndroidResource = "AndroidResource";
		public const string AndroidAsset = "AndroidAsset";
		public const string AndroidAarLibrary = "AndroidAarLibrary";
		public const string AndroidEnvironment = "AndroidEnvironment";
		public const string AndroidInterfaceDescription = "AndroidInterfaceDescription";
		public const string AndroidJavaSource = "AndroidJavaSource";
		public const string AndroidJavaLibrary = "AndroidJavaLibrary";
		/// <summary>
		/// Only supported in .NET 5+
		/// </summary>
		public const string AndroidLibrary = "AndroidLibrary";
		public const string AndroidLintConfig = "AndroidLintConfig";
		public const string AndroidNativeLibrary = "AndroidNativeLibrary";
		public const string _AndroidRemapMembers = "_AndroidRemapMembers";
		public const string ProguardConfiguration = "ProguardConfiguration";
		public const string TransformFile = "TransformFile";
		public const string InputJar = "InputJar";
		public const string ReferenceJar = "ReferenceJar";
		public const string EmbeddedJar = "EmbeddedJar";
		public const string EmbeddedNativeLibrary = "EmbeddedNativeLibrary";
		public const string EmbeddedReferenceJar = "EmbeddedReferenceJar";
		public const string LibraryProjectZip = "LibraryProjectZip";
		public const string LibraryProjectProperties = "LibraryProjectProperties";
	}
}
