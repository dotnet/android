using System;
using System.Collections.Generic;

namespace Xamarin.ProjectTools
{
	public static class TransientBuildFailure
	{
		static readonly string [] permanentHttpFailures = [
			"status code 401",
			"status code 403",
			"status code 404",
			"response code: 401",
			"response code: 403",
			"response code: 404",
			"response status code does not indicate success: 401",
			"response status code does not indicate success: 403",
			"response status code does not indicate success: 404",
		];

		public static bool TryGetDependencyResolutionReason (IEnumerable<string> buildOutput, out string reason)
		{
			if (buildOutput == null)
				throw new ArgumentNullException (nameof (buildOutput));

			bool hasDependencyResolutionFailure = false;
			bool hasAgpPluginResolutionFailure = false;
			bool usesDotNetPublicMaven = false;
			bool hasPermanentHttpFailure = false;
			bool inMavenArtifactFailure = false;
			bool hasPermanentJarProbeFailure = false;
			bool hasAarDiagnostic = false;
			bool hasPermanentAarFailure = false;
			string? transientReason = null;

			foreach (string line in buildOutput) {
				if (Contains (line, "Cannot download Maven artifact")) {
					hasPermanentHttpFailure |= HasPermanentMavenArtifactFailure (
						hasPermanentJarProbeFailure,
						hasAarDiagnostic,
						hasPermanentAarFailure
					);
					inMavenArtifactFailure = true;
					hasPermanentJarProbeFailure = false;
					hasAarDiagnostic = false;
					hasPermanentAarFailure = false;
				}

				hasDependencyResolutionFailure |=
					Contains (line, "Could not resolve") ||
					Contains (line, "Could not download") ||
					Contains (line, "Could not get resource") ||
					Contains (line, "Could not GET") ||
					Contains (line, "Could not HEAD") ||
					Contains (line, "Cannot download Maven artifact") ||
					Contains (line, "Failed to install the following SDK components") ||
					Contains (line, "services.gradle.org/distributions/");
				hasAgpPluginResolutionFailure |=
					Contains (line, "Plugin [id: 'com.android.application'") ||
					Contains (line, "com.android.application.gradle.plugin");
				usesDotNetPublicMaven |= Contains (line, TestEnvironment.DotNetPublicMaven);

				bool hasPermanentHttpFailureInLine = ContainsAny (line, permanentHttpFailures);
				// Maven restore probes JAR before AAR, so a rejected JAR probe is not permanent
				// when an AAR diagnostic follows for the same artifact.
				if (inMavenArtifactFailure && IsMavenArtifactDiagnostic (line, ".jar:")) {
					hasPermanentJarProbeFailure |= hasPermanentHttpFailureInLine;
				} else if (inMavenArtifactFailure && IsMavenArtifactDiagnostic (line, ".aar:")) {
					hasAarDiagnostic = true;
					hasPermanentAarFailure |= hasPermanentHttpFailureInLine;
				} else {
					hasPermanentHttpFailure |= hasPermanentHttpFailureInLine;
				}

				if (transientReason == null) {
					if (Contains (line, "Connection reset")) {
						transientReason = "connection reset";
					} else if (
						Contains (line, "nodename nor servname provided, or not known") ||
						Contains (line, "Name or service not known") ||
						Contains (line, "No such host is known") ||
						Contains (line, "Temporary failure in name resolution")
					) {
						transientReason = "DNS resolution failure";
					} else if (
						Contains (line, "Operation timed out") ||
						Contains (line, "The operation has timed out") ||
						Contains (line, "Read timed out") ||
						Contains (line, "Connect timed out") ||
						Contains (line, "Connection timed out")
					) {
						transientReason = "network timeout";
					} else if (Contains (line, "An error occurred while sending the request")) {
						transientReason = "request send failure";
					}
				}
			}

			hasPermanentHttpFailure |= HasPermanentMavenArtifactFailure (
				hasPermanentJarProbeFailure,
				hasAarDiagnostic,
				hasPermanentAarFailure
			);

			if (hasPermanentHttpFailure) {
				reason = "";
				return false;
			}

			if (hasDependencyResolutionFailure && transientReason != null) {
				reason = transientReason;
				return true;
			}

			if (hasAgpPluginResolutionFailure && usesDotNetPublicMaven) {
				reason = "AGP plugin resolution from dotnet-public-maven";
				return true;
			}

			reason = "";
			return false;
		}

		static bool HasPermanentMavenArtifactFailure (bool hasPermanentJarProbeFailure, bool hasAarDiagnostic, bool hasPermanentAarFailure)
		{
			return hasPermanentAarFailure || (hasPermanentJarProbeFailure && !hasAarDiagnostic);
		}

		static bool IsMavenArtifactDiagnostic (string line, string extension)
		{
			return Contains (line, "XA4236: -") && Contains (line, extension);
		}

		static bool Contains (string value, string text)
		{
			return value.IndexOf (text, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		static bool ContainsAny (string value, string [] values)
		{
			foreach (string candidate in values) {
				if (Contains (value, candidate))
					return true;
			}

			return false;
		}
	}
}
