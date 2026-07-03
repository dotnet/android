#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace Java.Interop {

	/// <include file="../Documentation/Java.Interop/JavaPeerableExtensions.xml" path="/docs/member[@name='T:JavaPeerableExtensions']/*" />
	public static class JavaPeerableExtensions {

		/// <include file="../Documentation/Java.Interop/JavaPeerableExtensions.xml" path="/docs/member[@name='M:GetJniTypeName']/*" />
		public static string? GetJniTypeName (this IJavaPeerable self)
		{
			JniPeerMembers.AssertSelf (self);
			return JniEnvironment.Types.GetJniTypeNameFromInstance (self.PeerReference);
		}

		/// <include file="../Documentation/Java.Interop/JavaPeerableExtensions.xml" path="/docs/member[@name='M:TryJavaCast']/*" />
		public static bool TryJavaCast<
				[DynamicallyAccessedMembers (JavaObject.Constructors)]
				TResult
		> (this IJavaPeerable? self, [NotNullWhen (true)] out TResult? result)
			where TResult : class, IJavaPeerable
		{
			result = JavaAs<TResult> (self);
			return result != null;
		}

		/// <include file="../Documentation/Java.Interop/JavaPeerableExtensions.xml" path="/docs/member[@name='M:JavaAs']/*" />
		public static TResult? JavaAs<
				[DynamicallyAccessedMembers (JavaObject.Constructors)]
				TResult
		> (this IJavaPeerable? self)
			where TResult : class, IJavaPeerable
		{
			if (self == null || !self.PeerReference.IsValid) {
				return null;
			}

			if (self is TResult result) {
				return result;
			}

			var r = self.PeerReference;
			return JniEnvironment.Runtime.ValueManager.CreatePeer (
					ref r, JniObjectReferenceOptions.Copy,
					targetType: typeof (TResult))
				as TResult;
		}
	}
}
