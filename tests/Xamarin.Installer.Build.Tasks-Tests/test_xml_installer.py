#!/usr/bin/env python3
"""Exercise the XML installer without compiling or loading any repository tasks."""

import hashlib
import http.server
import io
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tarfile
import tempfile
import threading
import unittest
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
TARGETS = ROOT / "src/Xamarin.Installer.Build.Tasks/Xamarin.Installer.Common.targets"
CLI = b"""#!/usr/bin/env python3
import json, pathlib, sys, time
root = pathlib.Path(__file__).parent
(root / "args.json").write_text(json.dumps(sys.argv[1:]))
mode = (root / "mode").read_text() if (root / "mode").exists() else ""
if mode == "fail":
    sys.exit(7)
if mode == "sleep":
    time.sleep(5)
sdk = pathlib.Path(next(a[6:] for a in sys.argv if a.startswith("--sdk=")))
if mode != "noop":
    for package in sys.argv[5:]:
        name, _, version = package.partition("@")
        version = version or name.rsplit("/", 1)[-1]
        path = sdk / name / "source.properties"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("Pkg.Revision = " + version + "\\n")
"""


class FixtureServer(http.server.BaseHTTPRequestHandler):
    files = {}
    requests = []

    def do_GET(self):
        self.requests.append(self.path)
        if self.path not in self.files:
            self.send_error(404)
            return
        data = self.files[self.path]
        self.send_response(200)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def log_message(self, *_):
        pass


class XmlInstallerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not shutil.which("dotnet"):
            raise unittest.SkipTest("dotnet is required")
        if os.name == "nt":
            raise unittest.SkipTest("The fake CLI fixture currently requires a Unix host")
        output = ROOT / "bin/installer-prototype"
        output.mkdir(parents=True, exist_ok=True)
        cls.suite = tempfile.TemporaryDirectory(prefix="xml-", dir=output)
        cls.server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), FixtureServer)
        cls.thread = threading.Thread(target=cls.server.serve_forever)
        cls.thread.start()
        cls.url = f"http://127.0.0.1:{cls.server.server_port}"

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.thread.join()
        cls.suite.cleanup()

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(dir=self.suite.name, prefix="paths with spaces ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.sdk = self.root / "sdk"
        self.jdk = self.root / "jdk"
        self.cache = self.root / "cli cache"
        self.cache.mkdir()
        self.cli = self.cache / "android-cli"
        self.cli.write_bytes(CLI)
        self.cli.chmod(0o755)
        FixtureServer.files = {"/android-cli": CLI}
        FixtureServer.requests = []
        self.project = ET.Element("Project")
        ET.SubElement(self.project, "Import", Project=str(TARGETS))
        get_dependencies = ET.SubElement(self.project, "Target", Name="GetAndroidDependencies")
        self.dependencies = ET.SubElement(get_dependencies, "ItemGroup")
        for name, version in [
            ("build-tools/37.0.0", "37.0.0"),
            ("platform-tools", "37.0.1"),
            ("ndk-bundle", "27.2.12479018"),
            ("platforms/android-37.0", ""),
        ]:
            ET.SubElement(self.dependencies, "AndroidDependency", Include=name, Version=version)
        self.properties = {
            "AndroidSdkDirectory": str(self.sdk),
            "JavaSdkDirectory": str(self.jdk),
            "AcceptAndroidSDKLicenses": "true",
            "AndroidInstallJavaDependencies": "false",
            "_AndroidCliPath": str(self.cli),
        }

    def run_installer(self, success=True, **properties):
        project = self.root / "test.proj"
        ET.ElementTree(self.project).write(project, encoding="utf-8")
        args = ["dotnet", "msbuild", str(project), "-t:InstallAndroidDependencies",
                "-v:minimal", "-nr:false", "-nologo"]
        args.extend(f"-p:{key}={value}" for key, value in (self.properties | properties).items())
        result = subprocess.run(args, cwd=ROOT, text=True, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, timeout=90)
        self.assertEqual(result.returncode == 0, success, result.stdout)
        return result.stdout

    def installed_package(self, name, revision):
        path = self.sdk / name / "source.properties"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(f"Pkg.Revision = {revision}\n")

    def test_package_arguments_and_second_run(self):
        self.run_installer()
        self.assertEqual(json.loads((self.cache / "args.json").read_text()), [
            "--no-metrics", f"--sdk={self.sdk}", "sdk", "install",
            "build-tools/37.0.0", "platform-tools@37.0.1", "ndk/27.2.12479018",
            "platforms/android-37.0",
        ])
        (self.cache / "args.json").unlink()
        self.run_installer(AcceptAndroidSDKLicenses="false")
        self.assertFalse((self.cache / "args.json").exists(), "Already installed packages must skip the CLI")

    def test_older_revision_is_installed_but_newer_revision_is_skipped(self):
        self.installed_package("platform-tools", "38.0.0")
        self.installed_package("build-tools/37.0.0", "36.0.0-rc1")
        self.run_installer()
        args = json.loads((self.cache / "args.json").read_text())
        self.assertNotIn("platform-tools@37.0.1", args)
        self.assertIn("build-tools/37.0.0", args)

    def test_license_guard(self):
        output = self.run_installer(success=False, AcceptAndroidSDKLicenses="false")
        self.assertIn("license agreements were not accepted", output)
        self.assertFalse((self.cache / "args.json").exists())

    def test_no_dependencies(self):
        self.dependencies.clear()
        self.run_installer(AcceptAndroidSDKLicenses="false")
        self.assertFalse((self.cache / "args.json").exists())

    def test_cli_failure(self):
        (self.cache / "mode").write_text("fail")
        output = self.run_installer(success=False)
        self.assertIn("code 7", output)

    def test_success_without_installation_warns(self):
        (self.cache / "mode").write_text("noop")
        output = self.run_installer()
        self.assertEqual(output.count("The Android CLI did not install Android SDK package"), 4)

    def test_timeout(self):
        output = self.run_installer(success=False, AndroidDependencyInstallationTimeout="0")
        self.assertIn("timed out or was cancelled", output)
        (self.cache / "mode").write_text("sleep")
        self.run_installer(success=False, AndroidDependencyInstallationTimeout="0.001")

    def configure_download(self, expected_hash=None):
        host = "darwin" if platform.system() == "Darwin" else "linux"
        arch = "arm64" if platform.machine().lower() in ("arm64", "aarch64") else "x86_64"
        items = ET.SubElement(self.project, "ItemGroup")
        ET.SubElement(items, "_AndroidCliPackage", Include=f"{host}_{arch}",
                      Url=f"{self.url}/android-cli",
                      Sha256=expected_hash or hashlib.sha256(CLI).hexdigest())
        self.properties["_AndroidCliPath"] = ""
        self.properties["_AndroidCliDirectory"] = str(self.cache)

    def test_cli_cache_is_verified_and_repaired(self):
        self.configure_download()
        self.run_installer()
        self.assertEqual(FixtureServer.requests, [])
        self.cli.write_text("corrupted cache")
        self.run_installer(AndroidSdkDirectory=str(self.root / "second sdk"))
        self.assertEqual(FixtureServer.requests, ["/android-cli"])
        self.assertEqual(self.cli.read_bytes(), CLI)
        self.assertEqual(set(self.cache.iterdir()), {self.cli, self.cache / "args.json"})

    def test_download_hash_mismatch_never_runs_cli(self):
        self.configure_download("0" * 64)
        self.cli.unlink()
        self.run_installer(success=False)
        self.assertFalse(self.cli.exists())
        self.assertEqual(list(self.cache.iterdir()), [])

    def configure_jdk(self):
        ET.SubElement(self.dependencies, "JavaDependency", Include="jdk", Version="21.0.8")
        self.properties["AndroidInstallJavaDependencies"] = "true"

    def test_existing_newer_jdk_is_not_downloaded(self):
        self.configure_jdk()
        self.jdk.mkdir()
        (self.jdk / "release").write_text('JAVA_VERSION="25.0.1"\n')
        self.run_installer()
        self.assertEqual(FixtureServer.requests, [])

    def make_signed_jdk(self):
        if not shutil.which("gpg"):
            self.skipTest("GnuPG is required for signed JDK tests")
        self.configure_jdk()
        # macOS Unix-domain socket paths are too short for a homedir under this worktree.
        key_directory = tempfile.TemporaryDirectory(prefix="xmlgpg-", dir="/tmp")
        self.addCleanup(key_directory.cleanup)
        gnupg = Path(key_directory.name)

        def gpg(*args):
            return subprocess.run(
                ["gpg", "--no-options", "--batch", "--homedir", str(gnupg), *args],
                check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            ).stdout

        gpg("--pinentry-mode", "loopback", "--passphrase", "",
            "--quick-generate-key", "XML installer test", "rsa2048", "sign", "0")
        self.addCleanup(subprocess.run, ["gpgconf", "--homedir", str(gnupg), "--kill", "gpg-agent"],
                        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        key = gpg("--export")
        FixtureServer.files["/key.asc"] = gpg("--armor", "--export")
        archive = self.root / "jdk.tar.gz"
        with tarfile.open(archive, "w:gz") as tar:
            home = "jdk/Contents/Home" if platform.system() == "Darwin" else "jdk"
            for name, content, mode in [
                (f"{home}/release", b'JAVA_VERSION="21.0.8"\n', 0o644),
                (f"{home}/bin/java", b"#!/bin/sh\necho fixture-java\n", 0o755),
            ]:
                entry = tarfile.TarInfo(name)
                entry.size = len(content)
                entry.mode = mode
                tar.addfile(entry, io.BytesIO(content))
        gpg("--detach-sign", str(archive))
        FixtureServer.files["/jdk.tar.gz"] = archive.read_bytes()
        FixtureServer.files["/jdk.tar.gz.sig"] = archive.with_suffix(".gz.sig").read_bytes()
        self.properties.update({
            "_AndroidJdkUrl": f"{self.url}/jdk.tar.gz",
            "_AndroidOpenJdkPublicKeyUrl": f"{self.url}/key.asc",
            "_AndroidOpenJdkPublicKeySha256": hashlib.sha256(key).hexdigest(),
        })

    def test_signed_jdk_installation(self):
        self.make_signed_jdk()
        self.run_installer(JavaSdkDirectory=f"{self.jdk}/")
        self.assertIn('JAVA_VERSION="21.0.8"', (self.jdk / "release").read_text())
        result = subprocess.run([self.jdk / "bin/java"], text=True, stdout=subprocess.PIPE, check=True)
        self.assertEqual(result.stdout.strip(), "fixture-java")
        self.assertEqual(set(self.root.glob("jdk.*")), {self.root / "jdk.tar.gz", self.root / "jdk.tar.gz.sig"})

    def test_tampered_jdk_is_not_extracted(self):
        self.make_signed_jdk()
        FixtureServer.files["/jdk.tar.gz"] += b"tampered"
        output = self.run_installer(success=False)
        self.assertIn("XA5305", output)
        self.assertFalse(self.jdk.exists())

    def test_untrusted_jdk_key_is_rejected(self):
        self.make_signed_jdk()
        output = self.run_installer(success=False, _AndroidOpenJdkPublicKeySha256="0" * 64)
        self.assertIn("XA5305", output)
        self.assertFalse(self.jdk.exists())

    def test_explicit_signature_skip_is_logged(self):
        self.make_signed_jdk()
        del FixtureServer.files["/jdk.tar.gz.sig"]
        output = self.run_installer(_SkipSignCheck="true")
        self.assertIn("signature verification was skipped", output)
        self.assertNotIn("/key.asc", FixtureServer.requests)
        self.assertTrue((self.jdk / "release").exists())

    def test_existing_non_jdk_directory_is_not_deleted(self):
        self.configure_jdk()
        self.jdk.mkdir()
        important = self.jdk / "unrelated.txt"
        important.write_text("preserve")
        output = self.run_installer(success=False)
        self.assertIn("Unable to install the Java SDK", output)
        self.assertEqual(important.read_text(), "preserve")


if __name__ == "__main__":
    unittest.main()
