def publishPackages(storageCredentialId, containerName, storageVirtualPath, filePaths) {
    def status = 0
    try {
         // Note: The following function is provided by the Azure Blob Jenkins plugin
         azureUpload(storageCredentialId: storageCredentialId,
                 storageType: "blobstorage",
                 containerName: containerName,
                 virtualPath: storageVirtualPath,
                 filesPath: filePaths,
                 allowAnonymousAccess: true,
                 pubAccessible: true,
                 doNotWaitForPreviousBuild: true,
                 uploadArtifactsOnlyIfSuccessful: true)
    } catch (error) {
        echo "ERROR : publishPackages: Unexpected error: ${error}"
        status = 1
    }

    return status
}

return this
