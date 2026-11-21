# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

from check_devskim import BANNED_FUNCTION_TAG, banned_findings, main


def report(results):
    return {
        "version": "2.1.0",
        "runs": [{"tool": {"driver": {"name": "devskim"}}, "results": results}],
    }


def finding(tags=None, level="warning"):
    return {
        "ruleId": "DS154189",
        "level": level,
        "properties": {"tags": [BANNED_FUNCTION_TAG] if tags is None else tags},
    }


def run_main(directory, documents, *options):
    paths = []
    for index, document in enumerate(documents):
        path = Path(directory) / f"{index}.sarif"
        path.write_text(json.dumps(document), encoding="utf-8")
        paths.append(str(path))
    with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
        return main([*options, *paths])


class DevSkimTests(unittest.TestCase):
    def test_clean_report(self):
        self.assertEqual([], banned_findings(report([])))

    def test_all_severities_are_gated(self):
        for level in ["error", "warning", "note", "none"]:
            with self.subTest(level=level):
                result = finding(level=level)
                self.assertEqual([result], banned_findings(report([result])))

    def test_other_findings_are_not_banned_functions(self):
        self.assertEqual([], banned_findings(report([finding(["Other.Tag"])])))

    def test_multiple_runs(self):
        document = report([])
        document["runs"] += report([finding()])["runs"]
        self.assertEqual(1, len(banned_findings(document)))

    def test_suppression_does_not_bypass_gate(self):
        result = finding()
        result["suppressions"] = [{"kind": "inSource", "status": "accepted"}]
        self.assertEqual([result], banned_findings(report([result])))

    def test_malformed_reports_fail(self):
        documents = [
            {},
            {"version": "2.1.0", "runs": []},
            {"version": "2.1.0", "runs": [{}]},
            {"version": "2.1.0", "runs": [{"tool": None}]},
            report(None),
            report([{}]),
            report([{"ruleId": "DS154189", "properties": {}}]),
            report([{"ruleId": "DS154189", "properties": None}]),
            report([finding(tags="not-a-list")]),
        ]
        for document in documents:
            with self.subTest(document=document), self.assertRaises(ValueError):
                banned_findings(document)

    def test_exit_codes_and_diagnostics(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "report.sarif"
            for document, expected in [(report([]), 0), (report([finding()]), 1), ({}, 2)]:
                path.write_text(json.dumps(document), encoding="utf-8")
                with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                    self.assertEqual(expected, main([str(path)]))
            for contents in ["", "{not json"]:
                path.write_text(contents, encoding="utf-8")
                with contextlib.redirect_stderr(io.StringIO()) as errors:
                    self.assertEqual(2, main([str(path)]))
                self.assertIn("DevSkim report error", errors.getvalue())
            with contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(2, main([str(Path(directory) / "missing.sarif")]))

    def test_expect_findings(self):
        with tempfile.TemporaryDirectory() as directory:
            for document, expected in [(report([finding()]), 0), (report([]), 1), (report([finding(["Other.Tag"])]), 1), ({}, 2)]:
                with self.subTest(document=document):
                    self.assertEqual(expected, run_main(directory, [document], "--expect-findings"))

    def test_require_results(self):
        with tempfile.TemporaryDirectory() as directory:
            first = str(Path(directory) / "0.sarif")
            for documents, expected in [
                ([report([])], 2),
                ([report([]), report([finding(["Other.Tag"])])], 2),
                ([report([finding(["Other.Tag"])]), report([])], 0),
                ([report([finding(["Other.Tag"])])], 0),
                ([report([finding()]), report([])], 1),
            ]:
                with self.subTest(documents=documents):
                    self.assertEqual(expected, run_main(directory, documents, "--require-results", first))

    def test_require_results_needs_a_listed_report(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(SystemExit) as raised:
                run_main(directory, [report([finding(["Other.Tag"])])], "--require-results", str(Path(directory) / "unlisted.sarif"))
            self.assertEqual(2, raised.exception.code)

    def test_multiple_reports(self):
        with tempfile.TemporaryDirectory() as directory:
            paths = [Path(directory) / f"{index}.sarif" for index in range(2)]
            paths[0].write_text(json.dumps(report([])), encoding="utf-8")
            document = report([finding()])
            document["runs"][0]["results"][0]["locations"] = [{
                "physicalLocation": {
                    "artifactLocation": {"uri": "src/native/example.cc"},
                    "region": {"startLine": 42},
                },
            }]
            paths[1].write_text(json.dumps(document), encoding="utf-8")
            with contextlib.redirect_stdout(io.StringIO()) as output:
                self.assertEqual(1, main([str(path) for path in paths]))
            self.assertIn("src/native/example.cc:42: DS154189", output.getvalue())

    def test_missing_report_among_valid_reports_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            valid = Path(directory) / "src.sarif"
            missing = Path(directory) / "tools.sarif"
            valid.write_text(json.dumps(report([])), encoding="utf-8")
            with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()) as errors:
                self.assertEqual(2, main([str(valid), str(missing)]))
            self.assertIn(str(missing), errors.getvalue())


if __name__ == "__main__":
    unittest.main()
