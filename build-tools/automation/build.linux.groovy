// This file is based on the Jenkins scripted pipeline (as opposed to the declarative pipeline) syntax
// https://jenkins.io/doc/book/pipeline/syntax/#scripted-pipeline

def XADir = "xamarin-android"
def buildTarget = 'jenkins'
def chRootPackages = 'xvfb xauth mono-devel autoconf automake build-essential vim-common p7zip-full cmake gettext libtool libgdk-pixbuf2.0-dev intltool pkg-config ruby scons wget xz-utils git/stretch-backports nuget ca-certificates-mono clang g++-mingw-w64 gcc-mingw-w64 libzip-dev openjdk-8-jdk unzip lib32stdc++6 lib32z1 libtinfo-dev:i386 linux-libc-dev:i386 zlib1g-dev:i386 gcc-multilib g++-multilib referenceassemblies-pcl zip fsharp psmisc libz-mingw-w64-dev msbuild mono-csharp-shell devscripts fakeroot debhelper libsqlite3-dev sqlite3 libc++-dev cli-common-dev curl'
def isPr = false                // Default to CI
def isStable = false            // Stable build workflow
def pBuilderBindMounts = null
def utils = null
def hasPrLabelFullMonoIntegrationBuild = false

def execChRootCommand(chRootName, chRootPackages, pBuilderBindMounts, makeCommand) {
    chroot chrootName: chRootName,
        additionalPackages: chRootPackages,
        bindMounts: pBuilderBindMounts,
        command: """
            export LC_ALL=en_US.UTF-8
            export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/games:/usr/local/games
            locale

            ${makeCommand}
            """
}

timestamps {
    node("${env.BotLabel}") {
        def scmVars = null

        stage ("checkout") {
            def ctAttempts = 3
            def retryAttempt = 0
            def waitSecondsBeforeRetry = 15
            retry(ctAttempts) {     // Retry will always invoke the body at least once for an attempt count of 0 or 1
                dir (XADir) {
                    if (retryAttempt > 0) {
                        echo "WARNING : Stage checkout failed on try #${retryAttempt}. Waiting ${waitSecondsBeforeRetry} seconds"
                        sleep(waitSecondsBeforeRetry)
                        echo "Retrying ..."
                        waitSecondsBeforeRetry = waitSecondsBeforeRetry * 2
                    }

                    retryAttempt++
                    scmVars = checkout scm
                }
            }
        }

        utils = load "${XADir}/build-tools/automation/utils.groovy"

        utils.stageWithTimeout('init', 30, 'SECONDS', XADir, true) {    // Typically takes less than a second for CI builds
            // Note: PR plugin environment variable settings available here: https://wiki.jenkins.io/display/JENKINS/GitHub+pull+request+builder+plugin
            isPr = env.ghprbActualCommit != null
            isStable = env.IsStable == '1'
            def branch = isPr ? env.GIT_BRANCH : scmVars.GIT_BRANCH
            def commit = isPr ? env.ghprbActualCommit : scmVars.GIT_COMMIT

            def buildType = isPr ? 'PR' : 'CI'

            echo "HostName: ${env.NODE_NAME}"
            echo "Git repo: ${env.GitRepo}"     // Defined as an environment variable in the jenkins build definition
            echo "Job: ${env.JOB_BASE_NAME}"
            echo "Workspace: ${env.WORKSPACE}"
            echo "Branch: ${branch}"
            echo "Commit: ${commit}"
            echo "Build type: ${buildType}"
            echo "Stable build workflow: ${isStable}"

            pBuilderBindMounts = "/home/${env.USER}"
            echo "pBuilderBindMounts: ${pBuilderBindMounts}"

            if (isPr) {
                echo "PR id: ${env.ghprbPullId}"
                echo "PR link: ${env.ghprbPullLink}"

                // Clear out the PR title and description. This is the equivalent of $JENKINS_HOME/global-pre-script/remove-problematic-ghprb-parameters.groovy used by freestyle builds
                echo "Clearing the PR title and description environment variables to avoid any special characters contained within from tripping up the build"
                env.ghprbPullTitle = ''
                env.ghprbPullLongDescription = ''

                if (utils.hasPrLabel(env.GitRepo, env.ghprbPullId, 'full-mono-integration-build')) {
                    hasPrLabelFullMonoIntegrationBuild = true
                    buildTarget = 'jenkins'
                } else {
                    buildTarget = 'all'
                }
            }

            sh "env"
        }

        utils.stageWithTimeout('build and package', 7, 'HOURS', XADir, true) {    // Typically takes 4-5 hours
            execChRootCommand(env.ChRootName, chRootPackages, pBuilderBindMounts,
                            """
                                if [ -z "\$JAVA_HOME" ]; then
                                    if [ -f /etc/profile.d/jdk.sh ]; then
                                        source /etc/profile.d/jdk.sh
                                    fi
                                fi

                                make prepare ${buildTarget} CONFIGURATION=${env.BuildFlavor} V=1 NO_SUDO=true MSBUILD_ARGS='/p:MonoRequiredMinimumVersion=5.12'

                                if [[ "${isPr}" != "true" ]]; then
                                    echo 'package deb'
                                    make package-deb CONFIGURATION=${env.BuildFlavor} V=1
                                else
                                    echo 'Skipping debian packaging for PR builds'
                                fi

                                if [[ "${isPr}" != "true" && "${isStable}" != "true" ]]; then
                                    echo 'build tests'
                                    xvfb-run -a -- make all-tests CONFIGURATION=${env.BuildFlavor} V=1
                                else
                                    echo 'Skipping build tests for PR and stable builds'
                                fi

                                make package-build-status CONFIGURATION=${env.BuildFlavor}
                            """)
        }

        utils.stageWithTimeout('publish packages to Azure', 30, 'MINUTES', '', true, 3) {    // Typically takes less than a minute, but provide ample time in situations where logs may be quite large
            if (isPr) {
                echo "Skipping package publishing for PR builds"
                return
            }

            def publishBuildFilePaths = "${XADir}/xamarin-android/*xamarin.android*.tar*";
            publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/xamarin-android/bin/${env.BuildFlavor}/bundle-*.zip"
            publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/*.dsc"
            publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/*.deb"
            publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/build-status*"
            publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/xa-build-status*"

            echo "publishBuildFilePaths: ${publishBuildFilePaths}"
            def commandStatus = utils.publishPackages(env.StorageCredentialId, env.ContainerName, env.StorageVirtualPath, publishBuildFilePaths)
            if (commandStatus != 0) {
                error "publish packages to Azure FAILED, status: ${commandStatus}"    // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        utils.stageWithTimeout('run all tests and package results', 360, 'MINUTES', XADir, false) {
            if (isPr || isStable) {
                echo "Skipping test run for PR and stable builds"
                return
            }

            echo "running tests"

            execChRootCommand(env.ChRootName, chRootPackages, pBuilderBindMounts,
                    """
                        xvfb-run -a -- make run-all-tests CONFIGURATION=${env.BuildFlavor} V=1 || (killall adb && false)
                        killall adb || true

                        echo "packaging test error logs"
                        make -C ${XADir} -k package-test-results CONFIGURATION=${env.BuildFlavor}
                    """)

            def publishTestFilePaths = "${XADir}/xa-test-results*,${XADir}/test-errors.zip"

            echo "publishTestFilePaths: ${publishTestFilePaths}"
            def commandStatus = utils.publishPackages(env.StorageCredentialId, env.ContainerName, env.StorageVirtualPath, publishTestFilePaths)
            if (commandStatus != 0) {
                error "publish test error logs to Azure FAILED, status: ${commandStatus}"    // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        utils.stageWithTimeout('Publish test results', 5, 'MINUTES', XADir, false, 3) {    // Typically takes under 1 minute to publish test results
            if (isPr || isStable) {
                echo "Skipping publishing of test results for PR and stable builds"
                return
            }

            def initialStageResult = currentBuild.currentResult

            xunit thresholds: [
                    failed(unstableNewThreshold: '0', unstableThreshold: '0'),
                    skipped()                                                       // Note: Empty threshold settings per settings in the xamarin-android freestyle build are not permitted here
                ],
                tools: [
                    NUnit2(deleteOutputFiles: true,
                            failIfNotNew: true,
                            pattern: 'TestResult-*.xml',
                            skipNoTestFiles: true,
                            stopProcessingIfError: false)
                ]

            if (initialStageResult == 'SUCCESS' && currentBuild.currentResult == 'UNSTABLE') {
                error "One or more tests failed"                // Force an error condition if there was a test failure to indicate that this stage was the source of the build failure
            }
        }
    }
}