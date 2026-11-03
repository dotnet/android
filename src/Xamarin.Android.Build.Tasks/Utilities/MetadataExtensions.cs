#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public static class MetadataExtensions
	{
		public static string? GetCustomAttributeFullName (this MetadataReader reader, CustomAttribute attribute, TaskLoggingHelper log)
		{
			if (attribute.Constructor.Kind == HandleKind.MemberReference) {
				var ctor = reader.GetMemberReference ((MemberReferenceHandle)attribute.Constructor);
				try {
					if (ctor.Parent.Kind == HandleKind.TypeReference) {
						var type = reader.GetTypeReference ((TypeReferenceHandle)ctor.Parent);
						return reader.GetString (type.Namespace) + "." + reader.GetString (type.Name);
					} else if (ctor.Parent.Kind == HandleKind.TypeSpecification) {
						var type = reader.GetTypeSpecification ((TypeSpecificationHandle)ctor.Parent);
						BlobReader blobReader = reader.GetBlobReader (type.Signature);
						SignatureTypeCode typeCode = blobReader.ReadSignatureTypeCode ();
						if (typeCode != SignatureTypeCode.GenericTypeInstance) {
							log.LogDebugMessage ($"Unsupported TypeSpecification signature: {typeCode}");
							return null;
						}
						blobReader.ReadByte (); // SignatureTypeKind.Class or SignatureTypeKind.ValueType.
						EntityHandle typeHandle = blobReader.ReadTypeHandle ();
						if (typeHandle.Kind == HandleKind.TypeReference) {
							TypeReference typeRef = reader.GetTypeReference ((TypeReferenceHandle)typeHandle);
							return reader.GetString (typeRef.Namespace) + "." + reader.GetString (typeRef.Name);
						} else if (typeHandle.Kind == HandleKind.TypeDefinition) {
							TypeDefinition typeDef = reader.GetTypeDefinition ((TypeDefinitionHandle)typeHandle);
							return reader.GetString (typeDef.Namespace) + "." + reader.GetString (typeDef.Name);
						}
						log.LogDebugMessage ($"Unsupported generic type handle kind: {typeHandle.Kind}");
						return null;
					} else {
						log.LogDebugMessage ($"Unsupported EntityHandle.Kind: {ctor.Parent.Kind}");
						return null;
					}
				}
				catch (InvalidCastException ex) {
					log.LogDebugMessage ($"Unsupported EntityHandle.Kind `{ctor.Parent.Kind}`: {ex}");
					return null;
				}
			} else if (attribute.Constructor.Kind == HandleKind.MethodDefinition) {
				var ctor = reader.GetMethodDefinition ((MethodDefinitionHandle)attribute.Constructor);
				var type = reader.GetTypeDefinition (ctor.GetDeclaringType ());
				return reader.GetString (type.Namespace) + "." + reader.GetString (type.Name);
			}
			return null;
		}

		public static CustomAttributeValue<object?> GetCustomAttributeArguments (this CustomAttribute attribute)
		{
			return attribute.DecodeValue (DummyCustomAttributeProvider.Instance);
		}

		/// <summary>
		/// Get the bytes in an embedded resource as a Stream.
		/// WARNING: It is incorrect to read from this stream after the PEReader has been disposed.
		/// 
		/// See:
		///		https://github.com/dotnet/corefx/issues/23372
		///		https://gist.github.com/nguerrera/6864d2a907cb07d869be5a2afed8d764
		/// </summary>
		/// <safety>
		/// <paramref name="resource"/> must be an embedded resource from <paramref name="peReader"/>.
		/// Its length prefix and payload must describe a valid range in that reader's image.
		/// Keep the reader and its image alive and undisposed throughout this call, including
		/// exceptional exits, and, on success, until the returned stream has been disposed.
		/// If the reader borrows externally owned storage, keep that storage readable and at a
		/// stable address for the same duration, pinned if it is managed memory.
		/// Prevent aliases or other threads from disposing the reader or invalidating that storage
		/// during this time. The returned stream neither retains nor owns the reader.
		/// </safety>
		public static unsafe Stream GetEmbeddedResourceStream (this PEReader peReader, ManifestResource resource)
		{
			checked // arithmetic overflow here could cause AV
			{
				// Locate start and end of PE image in unmanaged memory.
				var block = peReader.GetEntireImage ();
				byte* peImageStart;
				unsafe {
					// SAFETY: GetEntireImage supplies the image bounds; the caller keeps the reader
					// and any borrowed backing storage alive and stable, without concurrent disposal.
					Debug.Assert (block.Pointer != null && block.Length > 0);
					peImageStart = block.Pointer;
				}
				byte* peImageEnd = peImageStart + block.Length;

				// Locate offset to resources within PE image.
				int offsetToResources;
				if (!peReader.PEHeaders.TryGetDirectoryOffset (peReader.PEHeaders.CorHeader?.ResourcesDirectory ?? default, out offsetToResources)) {
					throw new BadImageFormatException ("Failed to get offset to resources in PE file.");
				}
				Debug.Assert (offsetToResources > 0);
				byte* resourceStart = peImageStart + offsetToResources + resource.Offset;

				// Get the length of the the resource from the first 4 bytes.
				if (resourceStart >= peImageEnd - sizeof (int)) {
					throw new BadImageFormatException ("resource offset out of bounds.");
				}

				// SAFETY: The embedded resource belongs to this image, and the preceding check
				// bounds its four-byte prefix; the caller prevents invalidation during this read.
				int resourceLength = unsafe (new BlobReader (resourceStart, sizeof (int)).ReadInt32 ());
				resourceStart += sizeof (int);
				if (resourceLength < 0 || resourceStart >= peImageEnd - resourceLength) {
					throw new BadImageFormatException ("resource offset or length out of bounds.");
				}

				// SAFETY: The checked offset/length bound the read-only stream to this image.
				// The caller keeps its storage alive and stable until the stream is disposed.
				return unsafe (new UnmanagedMemoryStream (resourceStart, resourceLength));
			}
		}
	}
}
