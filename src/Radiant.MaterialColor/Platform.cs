// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.MaterialColor;

/// <summary>The kind of device a scheme is for; the 2025 spec tunes surfaces and chroma per platform.</summary>
public enum Platform
{
    /// <summary>A phone (and, in Radiant, a desktop).</summary>
    Phone,

    /// <summary>A watch.</summary>
    Watch,
}
