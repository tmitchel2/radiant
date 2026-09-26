using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.MaterialColor.Tests;

/// <summary>
/// Compares a whole fixture before failing, so a failure says how many values differ and which
/// differed first. Colours must match exactly; doubles to within <see cref="Fixture.IsClose"/>.
/// </summary>
internal sealed class Mismatches
{
    private int _count;
    private int _total;
    private string _first = "";

    public void Colour(int expected, int actual, string label)
    {
        Record(expected == actual, () => $"{label}: expected {Fixture.Hex(expected)}, got {Fixture.Hex(actual)}");
    }

    public void Double(double expected, double actual, string label)
    {
        Record(Fixture.IsClose(expected, actual), () => $"{label}: expected {expected:R}, got {actual:R}");
    }

    public void Bool(bool expected, bool actual, string label)
    {
        Record(expected == actual, () => $"{label}: expected {expected}, got {actual}");
    }

    public void AssertNone()
    {
        Assert.IsTrue(_total > 0, "the fixture held nothing to compare");
        Assert.AreEqual(0, _count, $"{_count} of {_total} differ; first: {_first}");
    }

    private void Record(bool matches, Func<string> describe)
    {
        _total++;
        if (!matches)
        {
            _count++;
            if (_first.Length == 0)
            {
                _first = describe();
            }
        }
    }
}
