#!/usr/bin/env python3
"""Translates upstream's color_spec_{2021,2025,2026}.ts into Radiant.ColorSystem's ColorSpec*.cs.

    python3 transpile-color-specs.py <upstream>/typescript/dynamiccolor src/Radiant.ColorSystem
    dotnet format whitespace src/Radiant.ColorSystem/Radiant.ColorSystem.csproj

The spec files are ~2,800 lines of regular declarations (DynamicColor.fromPalette({...}) with
arrow-function arguments), so they are translated mechanically rather than by hand, and the result
is verified by the scheme fixtures (every role of 4,000 upstream schemes). The C# keeps upstream's
structure line for line, so a newer upstream can be re-translated and the diff reviewed.

What it maps: object-literal arguments to named arguments; this/super to C# members/base;
camelCase members to PascalCase; the string unions (tone polarity, delta constraint, platform,
spec version) to enums, by argument position inside `new ToneDeltaPair(...)` where 'nearer' and
'farther' mean different things; `Object.assign({}, s, {...})` to `DynamicScheme.CloneWith` and
`Object.assign(c.clone(), {...})` to `DynamicColor.With`; JavaScript's out-of-range array reads
(undefined) to `ElementAtOrDefault`; undefined to null.
"""
import re, sys

POLARITY = {'darker': 'Darker', 'lighter': 'Lighter', 'nearer': 'Nearer', 'farther': 'Farther',
            'relative_darker': 'RelativeDarker', 'relative_lighter': 'RelativeLighter'}
CONSTRAINT = {'exact': 'Exact', 'nearer': 'Nearer', 'farther': 'Farther'}
VARIANTS = {'MONOCHROME': 'Monochrome', 'NEUTRAL': 'Neutral', 'TONAL_SPOT': 'TonalSpot', 'VIBRANT': 'Vibrant',
            'EXPRESSIVE': 'Expressive', 'FIDELITY': 'Fidelity', 'CONTENT': 'Content', 'RAINBOW': 'Rainbow',
            'FRUIT_SALAD': 'FruitSalad', 'CMF': 'Cmf'}
TYPES = {'number': 'double', 'boolean': 'bool', 'TonalPalette': 'TonalPalette', 'Hct': 'Hct',
         'DynamicScheme': 'DynamicScheme', 'ContrastCurve': 'ContrastCurve', 'DynamicColor': 'DynamicColor'}

def pascal(n): return n[0].upper() + n[1:]

def matching(s, i, open_ch, close_ch):
    """Index of the bracket closing the one at s[i], skipping string literals."""
    depth = 0
    j = i
    while j < len(s):
        c = s[j]
        if c in "'\"`":
            q = c; j += 1
            while s[j] != q:
                j += 2 if s[j] == '\\' else 1
        elif c == open_ch:
            depth += 1
        elif c == close_ch:
            depth -= 1
            if depth == 0:
                return j
        j += 1
    raise ValueError('unbalanced at %d' % i)

def split_args(s):
    args, depth, cur, j = [], 0, '', 0
    while j < len(s):
        c = s[j]
        if c in "'\"":
            k = s.index(c, j + 1); cur += s[j:k+1]; j = k + 1; continue
        if c in '([{': depth += 1
        if c in ')]}': depth -= 1
        if c == ',' and depth == 0:
            args.append(cur); cur = ''
        else:
            cur += c
        j += 1
    if cur.strip(): args.append(cur)
    return args

def fix_from_palette(s):
    key = 'DynamicColor.fromPalette({'
    while key in s:
        i = s.index(key)
        brace = i + len(key) - 1
        end = matching(s, brace, '{', '}')
        assert s[end + 1] == ')', s[end:end+20]
        inner = s[brace + 1:end].rstrip()
        if inner.endswith(','):
            inner = inner[:-1]
        s = s[:i] + 'DynamicColor.FromPalette(' + inner + '\n)' + s[end + 2:]
    return s

def fix_tone_delta_pairs(s):
    out, pos = '', 0
    for m in re.finditer(r'new ToneDeltaPair\(', s):
        if m.start() < pos: continue
        open_i = m.end() - 1
        close_i = matching(s, open_i, '(', ')')
        args = split_args(s[open_i + 1:close_i])
        def lit(a, table, enum):
            t = a.strip()
            if t.startswith("'"):
                return a.replace(t, enum + '.' + table[t.strip("'")])
            return a
        args[3] = lit(args[3], POLARITY, 'TonePolarity')
        if len(args) > 5:
            args[5] = lit(args[5], CONSTRAINT, 'DeltaConstraint')
        out += s[pos:open_i + 1] + ','.join(args) + ')'
        pos = close_i + 1
    return out + s[pos:]

def fix_object_assign(s):
    """Object.assign({}, s, {...}) -> s.CloneWith(...); Object.assign(x.clone(), {...}) -> x.With(...)."""
    key = 'Object.assign('
    while key in s:
        i = s.index(key)
        open_i = i + len(key) - 1
        close_i = matching(s, open_i, '(', ')')
        args = [a.strip() for a in split_args(s[open_i + 1:close_i])]
        overrides = args[-1]
        assert overrides.startswith('{') and overrides.endswith('}'), overrides
        named = overrides[1:-1].strip().rstrip(',')
        if args[0] == '{}':
            replacement = f'{args[1]}.cloneWith({named})'
        else:
            target = args[0]
            assert target.endswith('.clone()'), target
            replacement = f'{target[:-len(".clone()")]}.with({named})'
        s = s[:i] + replacement + s[close_i + 1:]
    return s

def convert_params(params):
    out = []
    for p in split_args(params):
        p = p.strip()
        if not p: continue
        m = re.match(r'(\w+)\??:\s*([\w|]+)(\s*=\s*(.+))?$', p, re.S)
        assert m, p
        name, typ, _, default = m.groups()
        ctype = TYPES[typ.replace('|undefined', '')]
        out.append(f'{ctype} {name}' + (f' = {default.strip()}' if default else ''))
    return ', '.join(out)

def transpile(path, cls_name, base, sealed):
    s = open(path).read()
    s = re.sub(r'/\*\*.*?\*/\n?', '', s, flags=re.S)
    s = re.sub(r'^import .*?;\n', '', s, flags=re.M | re.S)
    s = s[s.index('\n', s.index('limitations under the License.')) + 1:] if 'limitations under the License.' in s else s
    s = s.replace('*/\n', '', 1) if s.lstrip().startswith('*/') else s
    # Top-level functions become private static members.
    funcs = re.findall(r'^function (\w+)', s, flags=re.M)
    header = s.index('export class')
    top, body = s[:header], s[header:]
    top = re.sub(r'^function (\w+)\(([^)]*)\):\s*(\w+)\s*\{',
                 lambda m: f'private static {TYPES[m.group(3)]} {pascal(m.group(1))}({convert_params(m.group(2))})\n{{',
                 top, flags=re.M)
    body = body[body.index('{') + 1:body.rindex('}')]
    s = top + body
    # Members.
    s = re.sub(r'^  (override )?(\w+)\(\):\s*DynamicColor(\|undefined)?\s*\{',
               lambda m: f'  public {"override" if m.group(1) else ("virtual" if not sealed or True else "")} DynamicColor{"?" if m.group(3) else ""} {pascal(m.group(2))}()\n  {{',
               s, flags=re.M)
    s = re.sub(r'^  (override )?highestSurface\(s: DynamicScheme\):\s*DynamicColor\s*\{',
               lambda m: f'  public {"override" if m.group(1) else "virtual"} DynamicColor HighestSurface(DynamicScheme s)\n  {{', s, flags=re.M)
    s = re.sub(r'\((\w+): DynamicScheme\) =>', r'(\1) =>', s)
    s = fix_object_assign(s)
    s = fix_from_palette(s)
    s = fix_tone_delta_pairs(s)
    s = re.sub(r'extendSpecVersion\(', 'DynamicColor.ExtendSpecVersion(', s)
    s = re.sub(r"'(2021|2025|2026)'", r'SpecVersion.Spec\1', s)
    s = s.replace("'phone'", 'Platform.Phone').replace("'watch'", 'Platform.Watch')
    for k, v in VARIANTS.items():
        s = re.sub(r'Variant\.' + k + r'\b', 'Variant.' + v, s)
    s = re.sub(r'\bthis\.(\w+)', lambda m: pascal(m.group(1)), s)
    s = re.sub(r'\bsuper\.(\w+)', lambda m: 'base.' + pascal(m.group(1)), s)
    for f in funcs:
        s = re.sub(r'\b' + f + r'\(', pascal(f) + '(', s)
    s = s.replace('Math.round(', 'MathUtils.Round(')
    s = re.sub(r'\bmath\.', 'MathUtils.', s)
    s = re.sub(r'(?<![.\w])clampDouble\(', 'MathUtils.clampDouble(', s)
    s = re.sub(r'\.([a-z]\w*)', lambda m: '.' + pascal(m.group(1)), s)
    s = re.sub(r'\b(let|const) (\w+)\s*:\s*\w+\s*=', r'var \2 =', s)
    s = re.sub(r'\b(let|const) ', 'var ', s)
    s = s.replace('===', '==').replace('!==', '!=')
    s = re.sub(r'\bundefined\b', 'null', s)
    s = re.sub(r"'([^'\n]*)'", r'"\1"', s)
    s = re.sub(r'(?<![\w)\]])\[(\s*-?\d[\d, \-.\n]*)\]', lambda m: '(double[])[' + m.group(1) + ']', s)
    s = re.sub(r',(\s*)\)', r'\1)', s)
    # JavaScript arrays answer undefined past their end; the second source color is optional.
    s = re.sub(r'\.SourceColorHcts\[(\d+)\]', r'.SourceColorHcts.ElementAtOrDefault(\1)', s)
    kind = 'sealed class' if sealed else 'class'
    usings = ('using System;\n' if 'Math.' in s else '') + ('using System.Linq;\n' if 'ElementAtOrDefault' in s else '')
    usings += '\n' if usings else ''
    head = f'''// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md), translated mechanically from its
// TypeScript and verified against its outputs; the structure deliberately mirrors upstream's so a
// newer upstream can be diffed and ported the same way.

{usings}namespace Radiant.ColorSystem;

/// <summary>The Radiant color roles as the {cls_name[-4:]} spec defines them. Upstream's <c>ColorSpecDelegateImpl{cls_name[-4:]}</c>.</summary>
internal {kind} {cls_name} : {base}
{{
'''
    return head + s.strip('\n') + '\n}\n'

src, dst = sys.argv[1], sys.argv[2]
for year, base, sealed in [('2021', 'IColorSpec', False), ('2025', 'ColorSpec2021', False), ('2026', 'ColorSpec2025', True)]:
    code = transpile(f'{src}/color_spec_{year}.ts', f'ColorSpec{year}', base, sealed)
    open(f'{dst}/ColorSpec{year}.cs', 'w').write(code)
    print(year, len(code.splitlines()), 'lines')
