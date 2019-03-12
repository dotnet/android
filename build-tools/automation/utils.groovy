def publishPackages(filePaths) {
    def status = 0
    try {
         // Note: The following function is provided by the Azure Blob Jenkins plugin
         azureUpload(storageCredentialId: env.StorageCredentialId,
                 storageType: "blobstorage",
                 containerName: env.ContainerName,
                 virtualPath: env.StorageVirtualPath,
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
