using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A form's fields and their checks, from <see cref="FormHooks.UseForm"/>. Each build declares its
/// fields with <see cref="Field"/>; <see cref="Submit"/> checks them all, shows every error, and
/// focuses the first field with one, or hands over the values when there are none.
/// <code>
/// var form = context.UseForm();
/// var email = form.Field("email", "", Validators.Required(), Validators.Email());
/// … new TextField("Email") { Value = email.State, OnChange = email.Set, OnFocusChange = email.FocusChanged, Error = email.Error, InputRef = email.InputRef }
/// … new SurfaceButton("Save") { OnPress = () => form.Submit(values => save(values["email"])) }
/// </code>
/// </summary>
public sealed class Form
{
    private readonly Dictionary<string, FormField> _fields = [];
    private readonly List<string> _order = [];
    private readonly Action _rebuild;

    internal Form(Action rebuild) => _rebuild = rebuild;

    /// <summary>Whether a submit has been tried (so every field shows its error).</summary>
    public bool Submitted { get; private set; }

    /// <summary>Whether every field passes its checks.</summary>
    public bool IsValid => _fields.Values.All(f => f.Problem is null);

    /// <summary>
    /// The field called <paramref name="name"/>: made with <paramref name="initial"/> text the first
    /// time, and kept (with what's been typed) after. Its checks are the ones given in this build.
    /// </summary>
    public FormField Field(string name, string initial, params Func<string, string?>[] checks)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_fields.TryGetValue(name, out var field))
        {
            field = new FormField(this, name, TextEditState.From(initial ?? ""));
            _fields[name] = field;
            _order.Add(name);
        }
        field.Checks = checks;
        return field;
    }

    /// <summary>The fields' texts by name.</summary>
    public IReadOnlyDictionary<string, string> Values => _fields.ToDictionary(p => p.Key, p => p.Value.Text);

    /// <summary>
    /// Checks every field. With none failing, calls <paramref name="onValid"/> with the values and
    /// returns true; otherwise shows every error, focuses the first failing field and returns false.
    /// </summary>
    public bool Submit(Action<IReadOnlyDictionary<string, string>> onValid)
    {
        ArgumentNullException.ThrowIfNull(onValid);
        Submitted = true;
        foreach (var field in _fields.Values)
        {
            field.Touch();
        }
        _rebuild();
        var first = _order.Select(name => _fields[name]).FirstOrDefault(f => f.Problem is not null);
        if (first is not null)
        {
            first.InputRef.Focus();
            return false;
        }
        onValid(Values);
        return true;
    }

    /// <summary>Puts every field back to <paramref name="values"/> (or empty), untouched.</summary>
    public void Reset(IReadOnlyDictionary<string, string>? values = null)
    {
        Submitted = false;
        foreach (var (name, field) in _fields)
        {
            field.Reset(TextEditState.From(values is not null && values.TryGetValue(name, out var text) ? text : ""));
        }
        _rebuild();
    }

    internal void Changed() => _rebuild();
}
