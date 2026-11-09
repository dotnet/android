"""Owned child/grandchild for the real native-MTP timeout regression."""

import json
import os
from pathlib import Path
import subprocess
import sys
import time


state = Path(sys.argv[1])
role = sys.argv[2]
record = {"pid": os.getpid(), "parentPid": os.getppid()}
if os.name == "posix":
    record["processGroup"] = os.getpgrp()
(state / f"{role}.json").write_text(json.dumps(record))
if role == "child":
    subprocess.Popen([sys.executable, __file__, str(state), "grandchild"])
print(f"timeout probe {role} PID {os.getpid()}", flush=True)
while True:
    (state / f"{role}.heartbeat").write_text(str(time.monotonic_ns()))
    time.sleep(0.1)
