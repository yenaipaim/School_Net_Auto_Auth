from __future__ import annotations

import sys
from pathlib import Path

from generate_schedule_test_suite import write_csv, write_ics, write_json
from schedule_test_data import get_variant


WRITERS = {
    "json": write_json,
    "csv": write_csv,
    "ics": write_ics,
}


def main() -> None:
    if len(sys.argv) != 4:
        raise SystemExit(
            "usage: generate_unique_schedule_files.py <variant> <json|csv|ics> <output>"
        )

    variant = get_variant(sys.argv[1])
    writer = WRITERS.get(sys.argv[2].lower())
    if writer is None:
        raise SystemExit(f"unsupported output format: {sys.argv[2]}")

    output_path = Path(sys.argv[3]).resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    writer(variant, output_path)
    print(output_path)


if __name__ == "__main__":
    main()
