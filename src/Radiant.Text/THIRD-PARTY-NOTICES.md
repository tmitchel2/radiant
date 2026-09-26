# Third-party notices for Radiant.Text

## font-rs

`GlyphRasterizer`'s line accumulation (`Canvas.Line`) is ported from
[font-rs](https://github.com/raphlinus/font-rs).

> Copyright 2015 Google Inc. All rights reserved.
>
> Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except
> in compliance with the License. You may obtain a copy of the License at
>
> http://www.apache.org/licenses/LICENSE-2.0
>
> Unless required by applicable law or agreed to in writing, software distributed under the License
> is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
> or implied. See the License for the specific language governing permissions and limitations under
> the License.

## Slug

`Radiant.Text.Slug` (glyph preparation and the CPU reference evaluator `SlugCoverage`) and the Slug
shader in `Radiant` (`ShaderLibrary.SlugTextShader`) implement the Slug algorithm from Eric
Lengyel, "GPU-Centered Font Rendering Directly from Glyph Outlines", *Journal of Computer Graphics
Techniques* 6(2), 2017 (https://jcgt.org/published/0006/02/02/). The coverage calculation follows
the reference shaders at https://github.com/EricLengyel/Slug.

- **Patent:** US 10,373,352 was dedicated to the public domain by its owner, Terathon Software,
  effective 17 March 2026 (https://terathon.com/blog/decade-slug.html).
- **Reference shaders:** available under the MIT License or the Apache License 2.0, at your option.
  Radiant uses them under the MIT License. The repository asks that software using the code give
  credit.

> MIT License
>
> Copyright (c) 2017 Eric Lengyel
>
> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
> associated documentation files (the "Software"), to deal in the Software without restriction,
> including without limitation the rights to use, copy, modify, merge, publish, distribute,
> sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
> furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or
> substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
> NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
> NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
> DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
> OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## HarfBuzz

Shaping, font variations and glyph outlines use [HarfBuzz](https://github.com/harfbuzz/harfbuzz)
through HarfBuzzSharp, both under the MIT licence.

## Fonts

Inter and JetBrains Mono are under the SIL Open Font License 1.1; their licences are in `Fonts/`.

Material Symbols Rounded (a subset: `tools/icons/icons.txt`, built by `tools/icons/subset.sh`) is
Copyright Google LLC under the Apache License 2.0; the licence is `Fonts/MaterialSymbols-LICENSE.txt`.
