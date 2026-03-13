using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// An NUnit attribute that wraps test execution to catch transient network/SSL errors
	/// and convert them to <see cref="Assert.Inconclusive(string)"/> results instead of failures.
	/// Apply at the assembly level to handle all tests globally.
	/// </summary>
	/// <example>
	/// <code>[assembly: Xamarin.ProjectTools.HandleTransientNetworkError]</code>
	/// </example>
	[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
	public sealed class HandleTransientNetworkErrorAttribute : NUnitAttribute, IWrapSetUpTearDown
	{
		public TestCommand Wrap (TestCommand command)
		{
			return new TransientNetworkErrorCommand (command);
		}
	}

	class TransientNetworkErrorCommand : DelegatingTestCommand
	{
		public TransientNetworkErrorCommand (TestCommand innerCommand) : base (innerCommand) { }

		public override TestResult Execute (TestExecutionContext context)
		{
			try {
				return innerCommand.Execute (context);
			} catch (Exception ex) when (TransientNetworkErrorDetector.IsTransientNetworkError (ex)) {
				context.CurrentResult.SetResult (ResultState.Inconclusive,
					$"Test skipped due to transient network error: {ex.Message}");
				return context.CurrentResult;
			}
		}
	}

	/// <summary>
	/// Provides detection logic for transient network errors that should not cause test failures.
	/// </summary>
	public static class TransientNetworkErrorDetector
	{
		/// <summary>
		/// Determines whether the given exception represents a transient network error
		/// that should be treated as inconclusive rather than a test failure.
		/// </summary>
		public static bool IsTransientNetworkError (Exception ex)
		{
			if (ex is HttpRequestException httpEx) {
				return IsTransientHttpError (httpEx);
			}

			// Flatten AggregateException to check all inner exceptions
			if (ex is AggregateException aggEx) {
				foreach (var inner in aggEx.Flatten ().InnerExceptions) {
					if (IsTransientNetworkError (inner)) {
						return true;
					}
				}
				return false;
			}

			// Walk the inner exception chain looking for transient network errors
			if (ex.InnerException != null) {
				return IsTransientNetworkError (ex.InnerException);
			}

			return false;
		}

		static bool IsTransientHttpError (HttpRequestException ex)
		{
			// Check for common transient HTTP status codes
			if (ex.StatusCode is HttpStatusCode statusCode) {
				return statusCode == HttpStatusCode.RequestTimeout ||
					statusCode == HttpStatusCode.GatewayTimeout ||
					statusCode == HttpStatusCode.ServiceUnavailable ||
					statusCode == HttpStatusCode.BadGateway;
			}

			// Check for socket/DNS errors (e.g., "nodename nor servname provided, or not known")
			if (ex.InnerException is SocketException) {
				return true;
			}

			// Check for SSL/TLS and I/O errors (e.g., "Received an unexpected EOF or 0 bytes from the transport stream")
			if (ex.InnerException is IOException) {
				return true;
			}

			return false;
		}
	}
}
