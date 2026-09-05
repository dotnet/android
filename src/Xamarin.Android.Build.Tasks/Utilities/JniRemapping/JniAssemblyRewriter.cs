#nullable enable

using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks.JniRemapping
{
	sealed class JniRewriteResult
	{
		public byte [] Image { get; }
		public int ReplacementCount { get; }
		public bool StrongNameSignatureCleared { get; }

		public JniRewriteResult (byte [] image, int replacementCount, bool strongNameSignatureCleared)
		{
			Image = image;
			ReplacementCount = replacementCount;
			StrongNameSignatureCleared = strongNameSignatureCleared;
		}
	}

	/// <summary>
	/// Rewrites JNI names embedded in <c>Android.Runtime.RegisterAttribute</c>,
	/// the <c>Java.Interop.Jni*SignatureAttribute</c> family, and generated
	/// JniPeerMembers/RegisterNatives <c>ldstr</c> strings according to an R8 mapping.
	///
	/// The rewrite runs in two passes. The first scans the source into an exact plan; the second
	/// reconstructs the whole assembly with <c>MetadataBuilder</c>, cloning every table row in its
	/// original order (so entity tokens keep their values) while rebuilding the heaps. That lifts
	/// the length restrictions of an in-place heap patch and lets two use sites that shared one
	/// deduplicated heap entry receive different values.
	/// </summary>
	static class JniAssemblyRewriter
	{
		public static JniRewriteResult Rewrite (byte [] sourceImage, R8Mapping mapping, TaskLoggingHelper log)
		{
			using var peReader = new PEReader (ImmutableArray.Create (sourceImage));
			if (!peReader.HasMetadata) {
				throw new JniRewriteException ("The file contains no managed metadata.");
			}

			MetadataReader reader = peReader.GetMetadataReader ();
			JniRewritePlan plan = new JniRewritePlanner (peReader, reader, mapping, log).CreatePlan ();
			if (plan.ReplacementCount == 0) {
				return new JniRewriteResult (sourceImage, 0, strongNameSignatureCleared: false);
			}

			FieldRvaTable fieldRvaTable = FieldRvaTable.Read (peReader, reader);
			AssemblyRebuildResult rebuilt = new AssemblyRebuilder (peReader, reader, plan, fieldRvaTable).Build ();
			return new JniRewriteResult (rebuilt.Image, plan.ReplacementCount, rebuilt.StrongNameSignatureCleared);
		}

		public static void ScanRewrittenAssembly (byte [] sourceImage, R8Mapping mapping, TaskLoggingHelper log)
		{
			using var peReader = new PEReader (ImmutableArray.Create (sourceImage));
			if (!peReader.HasMetadata) {
				throw new JniRewriteException ("The file contains no managed metadata.");
			}

			MetadataReader reader = peReader.GetMetadataReader ();
			ScanRewrittenAssembly (peReader, reader, mapping, log);
		}

		public static void ScanRewrittenAssembly (PEReader peReader, MetadataReader reader, R8Mapping mapping, TaskLoggingHelper log)
			=> new JniRewritePlanner (peReader, reader, mapping.CreateReverseMapping (), log).CreatePlan ();

		public static void ScanAssembly (PEReader peReader, MetadataReader reader, R8Mapping mapping, TaskLoggingHelper log)
			=> new JniRewritePlanner (peReader, reader, mapping, log).CreatePlan ();
	}
}
