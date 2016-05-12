using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Xamarin.ProjectTools
{
	public class SolutionBuilder : Builder
	{
		public IList<XamarinProject> Projects { get; }
		public string SolutionPath { get; set; }
		public bool BuildSucceeded { get; set; }

		public SolutionBuilder () : base()
		{
			Projects = new List<XamarinProject> ();
		}

		public void Save ()
		{
			foreach (var p in Projects) {
				using (var pb = new ProjectBuilder (Path.Combine (Path.GetDirectoryName (SolutionPath), p.ProjectName))) {
					pb.Save (p);
				}
			}
			// write a sln.
			var sb = new StringBuilder ();
			sb.AppendFormat ("Microsoft Visual Studio Solution File, Format Version {0}\n", "12.00");
			sb.AppendFormat ("# Visual Studio {0}\n", "2012");
			foreach (var p in Projects) {
				sb.AppendFormat ("Project(\"{{{0}}}\") = \"{1}\", \"{2}\", \"{{{3}}}\"\n", p.ProjectTypeGuid, p.ProjectName, 
					Path.Combine(p.ProjectName,p.ProjectFilePath), p.ProjectGuid);
				sb.Append ("EndProject\n");
			}
			sb.Append ("Global\n");
			sb.Append ("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution\n");
			sb.Append ("\t\tDebug|AnyCPU = Debug|AnyCPU\n");
			sb.Append ("\t\tRelease|AnyCPU = Release|AnyCPU\n");
			sb.Append ("\tEndGlobalSection\n");
			sb.Append ("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution\n");
			foreach (var p in Projects) {
				sb.AppendFormat ("{{{0}}}.Debug|AnyCPU.ActiveCfg = Debug|AnyCPU\n", p.ProjectGuid);
				sb.AppendFormat ("{{{0}}}.Debug|AnyCPU.Build.0 = Debug|AnyCPU\n", p.ProjectGuid);
				sb.AppendFormat ("{{{0}}}.Release|AnyCPU.ActiveCfg = Release|AnyCPU\n", p.ProjectGuid);
				sb.AppendFormat ("{{{0}}}.Release|AnyCPU.Build.0 = Release|AnyCPU\n", p.ProjectGuid);
			}
			sb.Append ("\tEndGlobalSection\n");
			sb.Append ("EndGlobal\n");
			File.WriteAllText (SolutionPath, sb.ToString ());
		}

		public bool Build ()
		{
			Save ();
			BuildSucceeded = BuildInternal (SolutionPath, "Build");
			return BuildSucceeded;
		}

		public bool ReBuild ()
		{
			Save ();
			BuildSucceeded = BuildInternal (SolutionPath, "ReBuild");
			return BuildSucceeded;
		}

		public bool Clean ()
		{
			Save ();
			BuildSucceeded = BuildInternal (SolutionPath, "Clean");
			return BuildSucceeded;
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing)
				if (BuildSucceeded)
					Directory.Delete (Path.GetDirectoryName (SolutionPath), recursive: true);
		}
	}
}

