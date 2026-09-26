[CmdletBinding()]
param (
	[Parameter(Mandatory)]
	[string] $ManifestPath,

	[Parameter(Mandatory)]
	[string] $PackageName,

	[Parameter(Mandatory)]
	[string] $Instrumentation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$manifestFullPath = [IO.Path]::GetFullPath($ManifestPath)
if (-not (Test-Path -LiteralPath $manifestFullPath -PathType Leaf)) {
	throw "Android manifest '$manifestFullPath' does not exist."
}

[xml] $document = Get-Content -LiteralPath $manifestFullPath -Raw
$manifest = $document.DocumentElement
if (-not $manifest -or $manifest.LocalName -ne 'manifest') {
	throw "Android manifest '$manifestFullPath' has no manifest root element."
}

$androidNamespace = 'http://schemas.android.com/apk/res/android'
$namespaceManager = [Xml.XmlNamespaceManager]::new($document.NameTable)
$namespaceManager.AddNamespace('android', $androidNamespace)

foreach ($permission in @(
	'android.permission.ACCESS_LOCAL_NETWORK'
	'android.permission.NEARBY_WIFI_DEVICES'
)) {
	$existing = $manifest.SelectSingleNode("uses-permission[@android:name='$permission']", $namespaceManager)
	if (-not $existing) {
		$element = $document.CreateElement('uses-permission')
		$element.SetAttribute('name', $androidNamespace, $permission) | Out-Null
		$manifest.PrependChild($element) | Out-Null
	}
}

$instrumentationElement = $manifest.SelectSingleNode("instrumentation[@android:name='$Instrumentation']", $namespaceManager)
if (-not $instrumentationElement) {
	$instrumentationElement = $document.CreateElement('instrumentation')
	$instrumentationElement.SetAttribute('name', $androidNamespace, $Instrumentation) | Out-Null
	$instrumentationElement.SetAttribute('targetPackage', $androidNamespace, $PackageName) | Out-Null
	$manifest.AppendChild($instrumentationElement) | Out-Null
} else {
	$targetPackage = $instrumentationElement.GetAttribute('targetPackage', $androidNamespace)
	if ($targetPackage -ne $PackageName) {
		throw "Instrumentation '$Instrumentation' targets '$targetPackage' instead of '$PackageName'."
	}
}

$settings = [Xml.XmlWriterSettings]::new()
$settings.Encoding = [Text.UTF8Encoding]::new($false)
$settings.Indent = $true
$writer = [Xml.XmlWriter]::Create($manifestFullPath, $settings)
try {
	$document.Save($writer)
} finally {
	$writer.Dispose()
}
