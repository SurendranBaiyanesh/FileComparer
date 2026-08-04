# FileComparer

Console application that compares an input file and an output file row by row, using one or more
columns as the key. Row order does not matter — rows are paired by their key values and then every
other shared column is compared.

## Running it

```bash
dotnet run -- -i samples/input.txt -o samples/output-success.txt -c PersonNumber
```

Anything not passed on the command line is read from `appsettings.json`, and anything still missing
is prompted for, so this works too:

```bash
dotnet run
```

```
Input file path : C:\Users\abb\Desktop\input.txt
Output file path: C:\Users\abb\Desktop\output.txt
Column(s) to compare on (comma separated, e.g. Name,PersonNumber): PersonNumber
```

## Options

| Command line | appsettings.json | Meaning |
| --- | --- | --- |
| `-i`, `--input` | `InputFilePath` | Input file path. |
| `-o`, `--output` | `OutputFilePath` | Output file path. |
| `-c`, `--columns` | `KeyColumns` | Key column(s), e.g. `PersonNumber` or `Name,PersonNumber`. |
| `--compare-columns` | `CompareColumns` | Columns compared once rows are paired. Default: every column the two files share. |
| `-s`, `--show-non-matching` | `ShowNonMatchingRows` | List the non-matching rows. |
| `--max-rows` | `MaxNonMatchingRowsToShow` | Cap on rows listed per category. `0` = all. |
| `--ignore-case` | `IgnoreCase` | Compare values case-insensitively. |
| `--trim` | `TrimValues` | Trim values before comparing. Default `true`. |
| `-d`, `--delimiter` | `Delimiter` | Delimiter for text files. Default: auto-detected from the header. |
| `--sheet` | `SheetName` | Worksheet for `.xlsx` files. Default: the first sheet. |
| `--config` | — | Settings file. Default: `appsettings.json` next to the executable. |

Exit codes: `0` files match, `1` differences found, `2` error.

## What gets reported

- Row counts for both files, matching rows, rows with value differences, rows missing from the
  output, and extra rows in the output.
- When `ShowNonMatchingRows` is on, each non-matching row with the exact column that differs and the
  line number it came from.
- Warnings for columns present in only one file (these are skipped, not compared) and for duplicate
  key values.

Success case — same rows in a different order:

```
    Rows in input                 : 3
    Rows in output                : 3
    Matching rows                 : 3
    Non-matching rows (total)     : 0

  RESULT: SUCCESS - all 3 row(s) match.
```

Fail case — same keys, different values:

```
  NON-MATCHING ROWS
    Value differences (2):
      [PersonNumber=101]
        Education: input='BSC'  output='MSC'
        input  (line 3): 101;Anbarasan;Anbhalagan;27;BSC
        output (line 2): 101;Anbarasan;Anbhalagan;27;MSC

  RESULT: FAILED - 2 non-matching row(s).
```

## Formats

| Format | Extensions | Notes |
| --- | --- | --- |
| Delimited text | `.csv` `.txt` `.tsv` `.psv` `.dat` | Delimiter auto-detected from `; , \t \|`. RFC 4180 quoting. |
| XML | `.xml` | Each record is an element; columns are its attributes and leaf child elements. |
| JSON | `.json` | An array of objects, or an object whose first array property holds the records. |
| Excel | `.xlsx` `.xlsm` | Read straight from the Open XML package — no third-party library. First row is the header. |

The two files do not have to be in the same format: comparing a `.txt` against an `.xlsx` works, as
long as the key columns exist on both sides.

Columns are matched by name, not position, so files with columns in a different order compare fine.
A header ending in a trailing separator (`PersonNumber;Name;LastName;Age;Education;`) is handled —
the empty trailing column is dropped.

Dates in `.xlsx` files are read as their underlying serial number, since cell number formats are not
interpreted. Two spreadsheets still compare correctly against each other; a spreadsheet compared
against a text file needs the dates written as text.

## Adding another format

Implement `ITableReader` (`CanRead` + `Read`) and register it in `TableReaderFactory`. Use
`TableBuilder.Build` to produce the `DataTable` so the header and ragged-row rules stay consistent
with the other readers.

## Layout

```
Program.cs                     entry point: config, prompts, run, exit code
Configuration/                 appsettings.json binding and command-line parsing
Model/DataTable.cs             format-independent table of string values
Readers/                       one reader per format + the shared TableBuilder
Comparison/                    key-based row pairing and value comparison
Reporting/ConsoleReport.cs     console output
samples/                       sample input/output files for the success and fail cases
```
