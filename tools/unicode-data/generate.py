#!/usr/bin/env python3
"""Generates Radiant.Text's Unicode property tables from the Unicode Character Database.

    python3 generate.py --ucd <ucd-dir> --file extracted/DerivedLineBreak.txt --alias lb \
        --enum LineBreakClass --table LineBreak --out src/Radiant.Text/Unicode

Reads a UCD property file (lines "XXXX..YYYY ; Value # comment"), applies its "# @missing:"
defaults (later ones override earlier, as the UCD specifies), normalises value names to their short
aliases from PropertyValueAliases.txt, merges adjacent ranges, and writes two files: the enum of
values (<Enum>.g.cs) and the lookup table (<Table>Data.g.cs, a UnicodeRangeTable). Values are
numbered in alphabetical order of their short names, so regenerating is stable.

--values restricts and orders the enum explicitly (e.g. "A B C"); codepoints whose value is not
listed take the default. --binary Name generates a yes/no table for one value of a multi-valued
file such as emoji-data.txt (Extended_Pictographic). --property Name reads one property from a file
that holds several with a field each ("XXXX ; Name ; Value"), such as InCB in
DerivedCoreProperties.txt.
"""
import argparse
import os
import re

ENTRY = re.compile(r'^([0-9A-F]{4,6})(?:\.\.([0-9A-F]{4,6}))?\s*;\s*([^#;]+?)\s*(?:[#;]|$)')
MISSING = re.compile(r'^#\s*@missing:\s*([0-9A-F]{4,6})\.\.([0-9A-F]{4,6})\s*;\s*([^#;]+?)\s*(?:[#;]|$)')
PROPERTY_ENTRY = re.compile(r'^([0-9A-F]{4,6})(?:\.\.([0-9A-F]{4,6}))?\s*;\s*([^#;]+?)\s*;\s*([^#;]+?)\s*(?:#|$)')
PROPERTY_MISSING = re.compile(
    r'^#\s*@missing:\s*([0-9A-F]{4,6})\.\.([0-9A-F]{4,6})\s*;\s*([^#;]+?)\s*;\s*([^#;]+?)\s*(?:#|$)')


def aliases(ucd, prop):
    """Maps every name for a property's values (short and long) to the short name."""
    table = {}
    if not prop:
        return table
    with open(os.path.join(ucd, 'PropertyValueAliases.txt'), encoding='utf-8') as f:
        for line in f:
            line = line.split('#', 1)[0].strip()
            if not line:
                continue
            fields = [x.strip() for x in line.split(';')]
            if fields[0] != prop:
                continue
            short = fields[1]
            for name in fields[1:]:
                table[name] = short
    return table


def match(line, plain, named, prop):
    """(start, end, value) of a line, or None; with prop, only lines naming that property count."""
    m = (named if prop else plain).match(line)
    if not m or (prop and m.group(3) != prop):
        return None
    return int(m.group(1), 16), int(m.group(2) or m.group(1), 16), m.group(4 if prop else 3)


def parse(path, alias, binary, prop):
    points = {}
    defaults = []
    with open(path, encoding='utf-8') as f:
        for line in f:
            if line.startswith('#'):
                m = match(line, MISSING, PROPERTY_MISSING, prop)
                if m:
                    defaults.append((m[0], m[1], alias.get(m[2], m[2])))
                continue
            m = match(line, ENTRY, PROPERTY_ENTRY, prop)
            if not m:
                continue
            start, end = m[0], m[1]
            value = alias.get(m[2], m[2])
            if binary:
                if value != binary:
                    continue
                value = 'Yes'
            for cp in range(start, end + 1):
                points[cp] = value
    return points, defaults


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ucd', required=True)
    ap.add_argument('--file', required=True)
    ap.add_argument('--alias', default='')
    ap.add_argument('--enum', required=True)
    ap.add_argument('--table', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--values', default='')
    ap.add_argument('--binary', default='')
    ap.add_argument('--property', default='')
    ap.add_argument('--default', default='')
    ap.add_argument('--version', default='16.0.0')
    args = ap.parse_args()

    alias = aliases(args.ucd, args.alias)
    points, defaults = parse(os.path.join(args.ucd, args.file), alias, args.binary, args.property)

    if args.binary:
        default = 'No'
        names = ['No', 'Yes']
    else:
        # Global default: the first whole-range @missing line, or --default.
        default = args.default or next((v for s, e, v in defaults if s == 0 and e == 0x10FFFF), None)
        if default is None:
            raise SystemExit('No default: pass --default')
        # Range-specific defaults fill code points the data lines leave out.
        for s, e, v in defaults:
            if s == 0 and e == 0x10FFFF:
                continue
            for cp in range(s, e + 1):
                points.setdefault(cp, v)
        if args.values:
            names = args.values.split()
            points = {cp: v for cp, v in points.items() if v in names}
        else:
            names = sorted(set(points.values()) | {default})

    index = {n: i for i, n in enumerate(names)}
    ranges = []
    for cp in sorted(points):
        v = points[cp]
        if v == default:
            continue
        if ranges and ranges[-1][1] == cp - 1 and ranges[-1][2] == v:
            ranges[-1][1] = cp
        else:
            ranges.append([cp, cp, v])

    header = (f'// <auto-generated>\n// Generated from Unicode {args.version} {args.file} by tools/unicode-data/generate.py.\n'
              f'// Do not edit; regenerate with tools/unicode-data/generate.sh.\n// </auto-generated>\n\n')
    members = ',\n'.join(f'    {n} = {i}' for i, n in enumerate(names))
    with open(os.path.join(args.out, f'{args.enum}.g.cs'), 'w', encoding='utf-8') as f:
        f.write(header + 'namespace Radiant.Text.Unicode;\n\n'
                f'/// <summary>Values of the Unicode property in {os.path.basename(args.file)}, by their short alias.</summary>\n'
                f'internal enum {args.enum} : byte\n{{\n{members},\n}}\n')

    def rows(values, per):
        lines = [', '.join(values[i:i + per]) for i in range(0, len(values), per)]
        return ',\n            '.join(lines)

    starts = rows([f'0x{r[0]:X}' for r in ranges], 12)
    ends = rows([f'0x{r[1]:X}' for r in ranges], 12)
    vals = rows([f'{index[r[2]]}' for r in ranges], 24)
    with open(os.path.join(args.out, f'{args.table}Data.g.cs'), 'w', encoding='utf-8') as f:
        f.write(header + 'namespace Radiant.Text.Unicode;\n\n'
                f'/// <summary>The {args.enum} of every code point ({len(ranges)} ranges; default {default}).</summary>\n'
                f'internal static class {args.table}Data\n{{\n'
                f'    public static UnicodeRangeTable Table {{ get; }} = new(\n'
                f'        [\n            {starts},\n        ],\n'
                f'        [\n            {ends},\n        ],\n'
                f'        [\n            {vals},\n        ],\n'
                f'        {index[default]});\n\n'
                f'    public static {args.enum} Get(int codePoint) => ({args.enum})Table.Lookup(codePoint);\n}}\n')
    print(f'{args.table}: {len(ranges)} ranges, {len(names)} values, default {default}')


if __name__ == '__main__':
    main()
