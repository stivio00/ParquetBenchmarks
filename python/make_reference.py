#!/usr/bin/env python3
"""Generates the canonical cross-language reference parquet file.

Writes data/bench-1m.parquet (1M rows x 10 columns, seed 42, Snappy) from the
same generate_dataset() used by benchmark.py. The file is meant to be kept in
the repo so the C# and Python suites decode identical bytes:

- C#  : SharedFileReadBenchmarks (Benchmarks/SharedFileReadBenchmarks.cs)
- py  : SharedFile_* benchmarks in benchmark.py (run automatically when the
        file is present)

Run from this folder:
    uv run python make_reference.py
"""

from __future__ import annotations

import argparse
from pathlib import Path

import pyarrow as pa
import pyarrow.parquet as pq

from benchmark import generate_dataset

# Microsecond timestamps and split nullability, so that every reader in both
# suites can decode the file:
# - string columns are OPTIONAL: Parquet.Net's POCO mapping requires nullable
#   string fields in the file (a required string column throws
#   InvalidDataException on deserialize), and all other engines handle
#   optional columns natively (the data itself contains no nulls);
# - value-type columns are REQUIRED to match the non-nullable C# properties;
# - timestamp[us] is the most portable unit across all engines, and the
#   minute-resolution data makes the ns -> us cast lossless.
SCHEMA = pa.schema(
    [
        ("Id", pa.int64(), False),
        ("Name", pa.string(), True),
        ("Price", pa.float32(), False),
        ("CreatedAt", pa.timestamp("us"), False),
        ("CreatedAtText", pa.string(), True),
        ("IsActive", pa.bool_(), False),
        ("Category", pa.string(), True),
        ("Rating", pa.float64(), False),
        ("ExternalId", pa.string(), True),
        ("Description", pa.string(), True),
    ]
)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Write the shared cross-language reference parquet file",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("--rows", type=int, default=1_000_000, help="row count (the shared benchmarks expect the 1M default)")
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument(
        "--out",
        type=Path,
        default=None,
        help="output path (default: ../data/bench-1m.parquet relative to this script)",
    )
    args = parser.parse_args()

    out = args.out or Path(__file__).resolve().parent.parent / "data" / "bench-1m.parquet"
    out.parent.mkdir(parents=True, exist_ok=True)

    df = generate_dataset(args.rows, args.seed)
    table = pa.Table.from_pandas(df, preserve_index=False).cast(SCHEMA)
    pq.write_table(table, out, compression="snappy")

    print(f"rows      : {table.num_rows:,}")
    print(f"columns   : {table.num_columns}")
    print(f"size      : {out.stat().st_size / 1024 / 1024:,.1f} MB")
    print(f"written to: {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
