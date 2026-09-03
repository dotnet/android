using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Abstract base class for Xamarin.Android projects in the test framework.
	/// Extends <see cref="DotNetXamarinProject"/> to provide Android-specific functionality and defaults.
	/// </summary>
	/// <remarks>
	/// This class serves as the foundation for concrete Android project types like
	/// <see cref="XamarinAndroidApplicationProject"/> and <see cref="XamarinAndroidLibraryProject"/>.
	/// It sets up Android-specific default properties and language settings.
	/// </remarks>
	/// <seealso cref="DotNetXamarinProject"/>
	/// <seealso cref="XamarinAndroidApplicationProject"/>
	/// <seealso cref="XamarinAndroidLibraryProject"/>
	/// <seealso cref="XamarinAndroidProjectLanguage"/>
	public abstract class XamarinAndroidProject : DotNetXamarinProject
	{
		/// <summary>
		/// Override to provide additional MSBuild XML content inside the auto-generated Directory.Build.targets file.
		/// The returned string is inserted as-is inside the root &lt;Project&gt; element, before the
		/// <c>_ClearResolvedCodeAnalysisRuleSet</c> target.
		/// </summary>
		protected virtual string ExtraDirectoryBuildTargetsContent => "";

		/// <summary>
		/// Initializes a new instance of the XamarinAndroidProject class with the specified configuration names.
		/// Sets the default language to C# and output type to Library.
		/// </summary>
		/// <param name="debugConfigurationName">The name for the debug configuration (default: "Debug").</param>
		/// <param name="releaseConfigurationName">The name for the release configuration (default: "Release").</param>
		/// <seealso cref="XamarinAndroidProjectLanguage"/>
		/// <seealso cref="KnownProperties.OutputType"/>
		protected XamarinAndroidProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release")
			: base (debugConfigurationName, releaseConfigurationName)
		{
			Language = XamarinAndroidProjectLanguage.CSharp;
			SetProperty (KnownProperties.OutputType, "Library");

			// Prevent CI Guardian SDL analysis from breaking CoreCompile incrementality.
			// Guardian's MergeGuardianDotnetAnalyzersRuleSets target regenerates a merged
			// ruleset file on every build (new timestamp), which is in CoreCompile's Inputs
			// via $(ResolvedCodeAnalysisRuleSet). Clearing this property ensures CoreCompile
			// is properly incremental when source files haven't changed.
			Imports.Add (new Import (() => "Directory.Build.targets") {
				TextContent = () => $"""
					<Project>
						{ExtraDirectoryBuildTargetsContent}
						<Target Name="_ClearResolvedCodeAnalysisRuleSet" BeforeTargets="CoreCompile" AfterTargets="ResolveCodeAnalysisRuleSet;MergeGuardianDotnetAnalyzersRuleSets">
							<PropertyGroup>
								<ResolvedCodeAnalysisRuleSet />
							</PropertyGroup>
						</Target>
					</Project>
				""",
			});
		}

		/// <summary>
		/// Gets the language settings specific to Xamarin.Android projects.
		/// </summary>
		/// <returns>The Android-specific language configuration.</returns>
		/// <seealso cref="XamarinAndroidProjectLanguage"/>
		/// <seealso cref="Language"/>
		public XamarinAndroidProjectLanguage XamarinAndroidLanguage {
			get { return (XamarinAndroidProjectLanguage) Language; }
		}
	}
}
