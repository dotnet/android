using System;
using System.Reflection;

[assembly: AssemblyCopyright("Copyright © Microsoft 2020")]
[assembly: AssemblyCompany("Microsoft")]

[assembly: AssemblyVersion (ThisAssembly.Git.SemVer.Major + "." + ThisAssembly.Git.SemVer.Minor + "." + ThisAssembly.Git.BaseVersion.Patch + "." + ThisAssembly.Git.Commits)]

[assembly: AssemblyFileVersion (ThisAssembly.Git.SemVer.Major + "." + ThisAssembly.Git.SemVer.Minor + "." + ThisAssembly.Git.BaseVersion.Patch + "." + ThisAssembly.Git.Commits)]

[assembly: AssemblyInformationalVersion (
	ThisAssembly.Git.SemVer.Major + "." +
	ThisAssembly.Git.SemVer.Minor + "." +
	ThisAssembly.Git.BaseVersion.Patch + "." +
	ThisAssembly.Git.Commits + "-" +
	ThisAssembly.Git.Branch + "+" +
	ThisAssembly.Git.Commit)]
