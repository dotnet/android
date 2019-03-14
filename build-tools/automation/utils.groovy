def stageWithTimeout(stageName, timeoutValue, timeoutUnit, directory, fatal, ctAttempts = 0, Closure body) {
    try {
        stage(stageName) {
            def retryAttempt = 0
            def waitSecondsBeforeRetry = 15
            retry(ctAttempts) {     // Retry will always invoke the body at least once for an attempt count of 0 or 1
                timeout(time: timeoutValue, unit: timeoutUnit) {
                    dir(directory) {
                        if (retryAttempt > 0) {
                            echo "WARNING : Stage ${stageName} failed on try #${retryAttempt}. Waiting ${waitSecondsBeforeRetry} seconds"
                            sleep(waitSecondsBeforeRetry)
                            echo "Retrying ..."
                            waitSecondsBeforeRetry = waitSecondsBeforeRetry * 2
                        }

                        retryAttempt++
                        body()
                    }
                }
            }
        }
    } catch (error) {
        def result = fatal ? 'ERROR' : 'UNSTABLE'
        echo "ERROR : ${stageName}: Unexpected error: ${error}. Marking build as ${result}."
        currentBuild.result = result
        if (fatal) {
            throw error
        }
    } finally {
        echo "Stage result: ${stageName}: ${currentBuild.currentResult}"
    }
}

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
