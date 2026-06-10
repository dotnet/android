#nullable enable
//
// ExecutionConfiguration.cs
//
// Author:
//       Greg Munn <greg.munn@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc

using System;
using Mono.AndroidTools;

namespace Xamarin.AndroidTools.Debugging
{
	/// <summary>
	/// Defines a configuration for running the application with the debugger
	/// </summary>
	public sealed class ExecutionConfiguration
	{
		public ExecutionConfiguration (string packageName, AmIntentCommand runCommand)
		{
			if (string.IsNullOrEmpty (packageName))
				throw new ArgumentException (nameof (packageName));

			this.PackageName = packageName;
			this.RunCommand = runCommand;
			this.Debugger = new DebuggerOptions ();
		}

		public ExecutionConfiguration (string packageName, string? runCommand)
		{
			if (string.IsNullOrEmpty (packageName))
				throw new ArgumentException (nameof (packageName));

			this.PackageName = packageName;
			this.Debugger = new DebuggerOptions ();

			if (!string.IsNullOrEmpty (runCommand)) {
				this.RunCommand = AmIntentCommandParser.Parse (runCommand, packageName);
			}
		}

		/// <summary>
		/// Gets the name of the package that is being debugged. This is needed to set FastDev property files for
		/// devices that fail to the debug properties corectly
		/// </summary>
		public string PackageName { get; private set; }

		/// <summary>
		/// Gets the command that will be used to run the application
		/// </summary>
		public AmIntentCommand? RunCommand { get; private set; }

		/// <summary>
		/// Gets the debugger options that will instruct the runtime how to connect to the debugger
		/// </summary>
		public DebuggerOptions Debugger { get; private set; }

		/// <summary>
		/// Gets or sets a value indicating whether Java debugging is allowed. Defaults to true, but will be available to be toggled off via -p:_AndroidAllowJavaDebugging=false.
		/// </summary>
		public bool AllowJavaDebugging { get; set; } = true;

		/// <summary>
		/// Gets or sets the adb command to execute prior to executing the RunCommand
		/// </summary>
		public string? BeforeRunCommand { get; set; }

		/// <summary>
		/// Gets or sets the adb command to execute after the debugging session has ended
		/// </summary>
		public string? AfterRunCommand { get; set; }

		/// <summary>
		/// Gets or sets an action to write a string to the application output log
		/// </summary>
		/// <remarks>
		/// We want to be able to output the results of the run / before run / after run commands to a log that is
		/// visisble to the user. This property should provide a method to do so.
		/// </remarks>
		public Action<string>? LogWiter { get; set; }
	}
}
