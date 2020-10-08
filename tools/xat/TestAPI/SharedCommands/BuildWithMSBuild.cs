using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Xamarin.Android.Prepare;

namespace Xamarin.Android.Tests.Shared
{
	class BuildWithMSBuild : SharedTestCommand
	{
		public string Target { get; set; } = String.Empty;

		public BuildWithMSBuild ()
			: base (nameof (BuildWithMSBuild), "Build a test suite using MSBuild/xabuild")
		{}

		protected override async Task<bool> Execute (XATest suite)
		{
			var msbuild = new MSBuildRunner (Context, msbuildPath: Context.MSBuildBinary);

			var arguments = new List<string> {
				"/r"
			};

			if (Target.Length > 0) {
				arguments.Add ($"/t:{Target}");
			}

			CustomizeBuildArguments (arguments);
			Log.InfoLine ($"Building suite suite '{suite.Name}'");
			string logTag = $"build_test_{suite.ID}";
			return await msbuild.Run (
				suite.TestProjectFilePath,
				logTag,
				arguments: arguments,
				binlogName: logTag,
				workingDirectory: Path.GetDirectoryName (suite.TestProjectFilePath)
			);
		}

		protected virtual void CustomizeBuildArguments (List<string> arguments)
		{}
	}
}
