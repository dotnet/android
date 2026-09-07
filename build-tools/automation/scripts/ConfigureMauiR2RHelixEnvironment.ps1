if ($env:SYSTEM_PULLREQUEST_ISFORK -eq 'True') {
	Write-Host 'Disabling Azure Pipelines test reporting for anonymous fork PR Helix submissions.'
	Remove-Item Env:SYSTEM_ACCESSTOKEN -ErrorAction Ignore
}
