using System;
using System.Collections.Generic;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A field of a <see cref="Form"/>: its text, its checks, and its error once it's been left (or a
/// submit was tried). Hand its parts to a text field:
/// <code>new TextField("Email") { Value = email.State, OnChange = email.Set, OnFocusChange = email.FocusChanged, Error = email.Error, InputRef = email.InputRef }</code>
/// </summary>
public sealed class FormField
{
    private readonly Form _form;

    internal FormField(Form form, string name, TextEditState state)
    {
        _form = form;
        Name = name;
        State = state;
    }

    /// <summary>The field's name, unique in its form.</summary>
    public string Name { get; }

    /// <summary>The text and selection.</summary>
    public TextEditState State { get; private set; }

    /// <summary>The text.</summary>
    public string Text => State.Text;

    /// <summary>Whether the field has been left since the form started, or a submit was tried.</summary>
    public bool Touched { get; private set; }

    /// <summary>The first failing check's message, or null when every check passes.</summary>
    public string? Problem
    {
        get
        {
            foreach (var check in Checks)
            {
                if (check(State.Text) is { } message)
                {
                    return message;
                }
            }
            return null;
        }
    }

    /// <summary>The error to show: <see cref="Problem"/> once the field is touched, null before.</summary>
    public string? Error => Touched ? Problem : null;

    /// <summary>A handle for the field's input, so a failed submit can focus it.</summary>
    public ElementRef InputRef { get; } = new();

    internal IReadOnlyList<Func<string, string?>> Checks { get; set; } = [];

    /// <summary>Takes a change from the input.</summary>
    public void Set(TextEditState state)
    {
        State = state;
        _form.Changed();
    }

    /// <summary>Takes a focus change from the input: leaving the field touches it, showing any error.</summary>
    public void FocusChanged(bool focused)
    {
        if (!focused && !Touched)
        {
            Touched = true;
            _form.Changed();
        }
    }

    internal void Touch() => Touched = true;

    internal void Reset(TextEditState state)
    {
        State = state;
        Touched = false;
    }
}
