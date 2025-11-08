using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace TimeTracker.Desktop.EditEntry;

public sealed class EditTimeEntryViewModel : INotifyPropertyChanged
{
    private readonly DateTime _initialStartLocal;
    private readonly DateTime _initialEndLocal;
    private readonly string _initialNotesNormalized;
    private string _startText;
    private string _endText;
    private string _notesText;
    private string _errorMessage = string.Empty;

    public EditTimeEntryViewModel(DateTime startLocal, DateTime endLocal, string notes = "")
    {
        _initialStartLocal = DateTime.SpecifyKind(startLocal, DateTimeKind.Local);
        _initialEndLocal = DateTime.SpecifyKind(endLocal, DateTimeKind.Local);
        _startText = _initialStartLocal.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        _endText = _initialEndLocal.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        _notesText = notes ?? string.Empty;
        _initialNotesNormalized = NormalizeNotes(_notesText);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string StartText
    {
        get => _startText;
        set
        {
            if (SetProperty(ref _startText, value))
            {
                ClearError();
            }
        }
    }

    public string EndText
    {
        get => _endText;
        set
        {
            if (SetProperty(ref _endText, value))
            {
                ClearError();
            }
        }
    }

    public string NotesText
    {
        get => _notesText;
        set => SetProperty(ref _notesText, value ?? string.Empty);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool TryBuildResult(out EditTimeEntryResult result)
    {
        result = default;

        if (!TryParseStart(out var startLocal))
        {
            SetError("Enter a valid start date/time (e.g. 2024-03-01 09:15).");
            return false;
        }

        if (!TryParseEnd(out var endLocal))
        {
            SetError("Enter a valid end date/time (e.g. 2024-03-01 17:30).");
            return false;
        }

        if (endLocal <= startLocal)
        {
            SetError("End time must be later than the start time.");
            return false;
        }

        var normalizedNotes = NormalizeNotes(_notesText);
        var notesChanged = !string.Equals(normalizedNotes, _initialNotesNormalized, StringComparison.Ordinal);

        ClearError();
        result = new EditTimeEntryResult(startLocal, endLocal, notesChanged ? normalizedNotes : null);
        return true;
    }

    private bool TryParseStart(out DateTime value)
        => TryParseDateTime(_startText, _initialStartLocal, out value);

    private bool TryParseEnd(out DateTime value)
        => TryParseDateTime(_endText, _initialEndLocal, out value);

    private static bool TryParseDateTime(string input, DateTime fallback, out DateTime value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var styles = DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces;

        if (DateTime.TryParse(input, CultureInfo.CurrentCulture, styles, out var parsed)
            || DateTime.TryParse(input, CultureInfo.InvariantCulture, styles, out parsed))
        {
            value = EnsureLocalKind(parsed);
            return true;
        }

        if (DateTime.TryParseExact(
                input,
                new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm", "yyyy/MM/dd HH:mm", "HH:mm" },
                CultureInfo.CurrentCulture,
                styles,
                out parsed)
            || DateTime.TryParseExact(
                input,
                new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm", "yyyy/MM/dd HH:mm", "HH:mm" },
                CultureInfo.InvariantCulture,
                styles,
                out parsed))
        {
            if (parsed.TimeOfDay != TimeSpan.Zero && parsed.Date == DateTime.MinValue.Date)
            {
                parsed = new DateTime(
                    fallback.Year,
                    fallback.Month,
                    fallback.Day,
                    parsed.Hour,
                    parsed.Minute,
                    parsed.Second,
                    DateTimeKind.Local);
            }

            value = EnsureLocalKind(parsed);
            return true;
        }

        if (TimeSpan.TryParse(input, CultureInfo.CurrentCulture, out var span)
            || TimeSpan.TryParse(input, CultureInfo.InvariantCulture, out span))
        {
            value = new DateTime(
                fallback.Year,
                fallback.Month,
                fallback.Day,
                span.Hours,
                span.Minutes,
                span.Seconds,
                DateTimeKind.Local);
            return true;
        }

        return false;
    }

    private static DateTime EnsureLocalKind(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Local => value,
            DateTimeKind.Utc => value.ToLocalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local)
        };
    }

    private void ClearError() => ErrorMessage = string.Empty;

    private void SetError(string message) => ErrorMessage = message;

    private static string NormalizeNotes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim();
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
    {
        if (Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public readonly record struct EditTimeEntryResult(DateTime StartLocal, DateTime EndLocal, string Notes);
