#!/bin/bash -e

# Run this script as part of a "mono bunp", after a "mono archive"
# is available for use.
#
# See also the `_DownloadArchive` target in `src/mono-runtimes/mono-runtimes.targets`


function PrintItemGroup()
{
	local item="$1"
	local dir="$2"

	echo "  <ItemGroup>"

	for f in `find "$dir" -depth 1 -name \*.dll | sed 's,.dll$,,g' | grep -v 'nunitlite' | sort -f` ; do
		local n=`basename "$f"` ;
		echo "    <$item Include=\"$n.dll\" />" ;
	done

	echo "  </ItemGroup>"
}

function PrintTestItemGroup()
{
	local dir="$1"

	echo "  <ItemGroup>"

	for f in `find "$dir" -depth 1 -name \*.dll | sed 's,.dll$,,g' | sort -f` ; do
		local n=`basename "$f"` ;
		echo "    <MonoTestAssembly Include=\"$n.dll\">" ;
		if [[ "$n" == *xunit-test ]]; then
			echo "      <TestType>xunit</TestType>";
		fi
		echo "    </MonoTestAssembly>"
		for s in `find "$dir" -depth 2 -name "$n.resources.dll" | sed 's,.dll$,,g' | sort -f` ; do
			local p="${s:${#dir}}"
			p="${p:1}"
			echo "    <MonoTestSatelliteAssembly Include=\"$p.dll\" />"
		done
	done

	echo "  </ItemGroup>"
}

cat <<EOF
<?xml version="1.0" encoding="utf-8"?>
<!-- This is a GENERATED FILE -->
<!-- See build-tools/scripts/gen-ProfileAssemblies.sh -->
<Project DefaultTargets="Build" ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
EOF

PrintItemGroup "MonoFacadeAssembly"   "external/mono/sdks/out/android-bcl/monodroid/Facades"
PrintItemGroup "MonoProfileAssembly"  "external/mono/sdks/out/android-bcl/monodroid"
PrintTestItemGroup                    "external/mono/sdks/out/android-bcl/monodroid/tests"

cat <<EOF
  <!-- Manual fixups -->
  <ItemGroup>
    <!-- Mono.CSharp testsuite dynamically loads Microsoft.CSharp -->
    <MonoTestAssembly Include="Microsoft.CSharp.dll">
      <TestType>reference</TestType>
    </MonoTestAssembly>
    <!-- This is referenced by monodroid_corlib_xunit-test.dll -->
    <MonoTestAssembly Include="System.Runtime.CompilerServices.Unsafe.dll">
      <TestType>reference</TestType>
    </MonoTestAssembly>
    <MonoTestRunner Include="nunitlite.dll" />
  </ItemGroup>
</Project>
EOF
