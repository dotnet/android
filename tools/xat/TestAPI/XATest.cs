using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Android.Prepare;

namespace Xamarin.Android.Tests
{
	abstract class XATest : AppObject
	{
		public sealed class FailedCommand : AppObject
		{
			public string Phase        { get; }
			public TestCommand Command { get; }

			public FailedCommand (string phase, TestCommand command)
			{
				Phase = EnsurePropertyValue (nameof (phase), phase);
				Command = command;
			}
		}

		protected const string BuildPhaseName = "build";
		protected const string RunPhaseName = "execute";
		protected const string CleanupPhaseName = "cleanup";
		protected const string GlobalInitPhaseName = "global-init";
		protected const string GlobalShutdownPhaseName = "global-shutdown";

		/// <summary>
		///   Name of the test "kind" (e.g. "APK", "NUnit" etc)
		/// </summary>
		public abstract string KindName                        { get; }

		/// <summary>
		///   Name of separate test "family" within one kind (<see cref="KindName"/>). This can be identical to <see
		///   cref="KindName"/> if there's just a single "family" of tests (e.g. for APK tests) or set separately for
		///   each related set of tests (e.g. Java.Interop tests within the "Host Unit" kind)
		/// </summary>
		public abstract string TestFamilyName                  { get; }

		/// <summary>
		///   Display name for the test, must not be empty and must be unique across all the tests.
		/// </summary>
		public string Name                                     { get; }

		/// <summary>
		///   Test ID, created from <see cref="Name"/> by replacing all non-alphanumeric characters with
		///   underscore characters. After the conversion, the ID must be unique across all the tests
		/// </summary>
		public string ID                                       { get; }

		/// <summary>
		///   Each test uses some sort of input/container file (a .csproj, a .dll etc) but the exact meaning and
		///   treatment of the file depends on the actual test engine/kind/runner etc. However, since all of the tests
		///   have some sort of file, this base class has this generic, required, property to keep it. Derived classes
		///   should expose it to the world using a property name specific to the test kind (e.g. Assembly for unit
		///   tests, Project for APK tests etc)
		/// </summary>
		protected string TestFilePath                          { get; }

		/// <summary>
		///   Path to a widely understood "project file" that can be used to build the test suite. It can be a
		///   <c>.csproj</c> .NET project or a native <c>Makefile</c>.  The exact kind of the project file is not known
		///   here, it's up to XATest derivatives to correctly use it.
		/// </summary>
		public string TestProjectFilePath                      { get; }

		/// <summary>
		///   Lists commands to build the test
		/// </summary>
		public List<TestCommand> BuildCommands                 { get; } = new List<TestCommand> ();

		/// <summary>
		///   Lists commands to run the test(s)
		/// </summary>
		public List<TestCommand> RunCommands                   { get; } = new List<TestCommand> ();

		/// <summary>
		///   Lists commands to clean up after test run is complete
		/// </summary>
		public List<TestCommand> CleanupCommands               { get; } = new List<TestCommand> ();

		/// <summary>
		///   A set of environment variables that will be placed in the test process' environment.
		/// </summary>
		public Dictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string> (StringComparer.Ordinal);

		public List<string> IncludeCategories                  { get; } = new List<string> ();
		public List<string> ExcludeCategories                  { get; } = new List<string> ();
		public List<string> IncludeTests                       { get; } = new List<string> ();
		public List<string> ExcludeTests                       { get; } = new List<string> ();
		public List<string> TestNames                          { get; } = new List<string> ();
		public List<Exception> Exceptions                      { get; } = new List<Exception> ();
		public List<FailedCommand> FailedCommands              { get; } = new List<FailedCommand> ();

		protected XATest (string name, string testFilePath, string testProjectFilePath)
		{
			Name = EnsureParameterValue (nameof (name), name);
			TestFilePath = Utilities.EnsureFullPath (EnsureParameterValue (nameof (testFilePath), testFilePath));
			TestProjectFilePath = Utilities.EnsureFullPath (EnsureParameterValue (nameof (testProjectFilePath), testProjectFilePath));
			ID = Utilities.MakeID (name);
		}

#pragma warning disable 1998
		public virtual async Task<bool> RunGlobalInitCommands ()
		{
			return true;
		}

		public virtual async Task<bool> RunGlobalShutdownCommands ()
		{
			return true;
		}
#pragma warning restore 1998

		public virtual async Task<bool> Build ()
		{
			return await ExecuteCommands (BuildCommands, BuildPhaseName);
		}

		public virtual async Task<bool> Run ()
		{
			return await ExecuteCommands (RunCommands, RunPhaseName);
		}

		public virtual async Task<bool> Cleanup ()
		{
			return await ExecuteCommands (CleanupCommands, CleanupPhaseName);
		}

		protected async Task<bool> ExecuteCommands (List<TestCommand> commands, string phaseName)
		{
			if (commands.Count == 0) {
				return true;
			}

			if (!BeforeExecuteCommands (commands, phaseName)) {
				Log.ErrorLine ($"Test '{Name}' {phaseName}: failed to configure command execution.");
				return false;
			}

			bool haveFailures = false;
			foreach (TestCommand cmd in commands) {
				if (!await ExecuteCommand (cmd, phaseName)) {
					haveFailures = true;
					bool hardFailure = false;
					if (cmd.FailureMode != CommandFailureMode.Quiet) {
						CommandFailureMode failureMode = default;
						var failedCommands = new List<TestCommand> ();

						if (cmd.RunsNestedCommands && cmd.FailedCommands.Count > 0) {
							failedCommands.AddRange (cmd.FailedCommands);

							// If any of the failed commands has its failure mode set to Error, we need to obey it
							foreach (TestCommand failedCommand in failedCommands) {
								if (failedCommand.FailureMode == CommandFailureMode.Error) {
									failureMode = CommandFailureMode.Error;
									break;
								}
							}
						} else {
							failedCommands.Add (cmd);
							failureMode = cmd.FailureMode;
						}

						var failureMessages = new List<string> ();
						foreach (TestCommand failedCommand in failedCommands) {
							failureMessages.Add ($"Test '{Name}', phase '{phaseName}': command '{failedCommand.Name}' ({failedCommand.Description}) failed.");
							FailedCommands.Add (new FailedCommand (phaseName, failedCommand));
						}

						if (failureMode == CommandFailureMode.Error) {
							PrintMessages (failureMessages, message => Log.ErrorLine (message));
						} else if (failureMode == CommandFailureMode.WarnAndContinue) {
							PrintMessages (failureMessages, message => Log.WarningLine (message));
							hardFailure = true;
						} else {
							throw new InvalidOperationException ($"Unknown failure mode {failureMode}");
						}
					}

					if (!hardFailure)
						return false;
				}
			}

			return !haveFailures;

			void PrintMessages (List<string> messages, Action<string> printer)
			{
				foreach (string message in messages) {
					printer (message);
				}
			}
		}

		protected virtual async Task<bool> ExecuteCommand (TestCommand command, string phaseName)
		{
			if (!await command.Run (this)) {
				return false;
			}

			return true;
		}

		protected virtual bool BeforeExecuteCommands (List<TestCommand> commands, string phaseName)
		{
			return true;
		}

		protected bool IsGlobalPhase (string phaseName)
		{
			return
				String.Compare (phaseName, GlobalInitPhaseName, StringComparison.Ordinal) == 0 ||
				String.Compare (phaseName, GlobalShutdownPhaseName, StringComparison.Ordinal) == 0;
		}
	}
}
