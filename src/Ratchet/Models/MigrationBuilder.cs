using System.Security.Cryptography;

namespace Ratchet.Models;

internal sealed class MigrationBuilder(string file, string[] contextFilter, bool requiresContext = false)
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true
    };
    private MigrationHeader? _header;
    private readonly List<string> _sqlStatements = [];
    private readonly StringBuilder _stringBuilder = new();

    public Error? AddHeader(string input)
    {
        try
        {
            _header = JsonSerializer.Deserialize<MigrationHeader>(input, jsonOptions);
            return null;
        }
        catch (JsonException ex)
        {
            return Error.From(ex);
        }
    }

    public void AddToSql(string input) => _stringBuilder.AppendLine(input);

    public void FinishStatement()
    {
        if (_stringBuilder.Length == 0)
            return;

        _sqlStatements.Add(_stringBuilder.ToString().Trim());
        _stringBuilder.Clear();
    }

    public Error? Build(out Migration? migration)
    {
        if (_stringBuilder.Length > 0)
        {
            _sqlStatements.Add(_stringBuilder.ToString());
            _stringBuilder.Clear();
        }

        migration = null;
        if (_header is not { Title: not null } || _sqlStatements.Count == 0)
        {
            Reset();
            return null;
        }

        if (TryParseRun(_header, out var run) is { } runError)
        {
            Reset();
            return runError;
        }

        migration = new Migration
        {
            FileName = file,
            Title = _header.Title,
            SqlStatements = [.. _sqlStatements],
            DependsOn = _header.DependsOn ?? [],
            Hash = CalculateHash(_sqlStatements),
            Run = run,
            RunInTransaction = _header.RunInTransaction ?? true,
            ContextRequired = requiresContext || (_header.ContextRequired ?? false),
            ContextFilter = [.. _header.ContextFilter ?? [], .. contextFilter],
            Timeout = _header.Timeout ?? 30,
            OnError = _header.OnError switch
            {
                string s when s.Equals("Skip", StringComparison.OrdinalIgnoreCase) => Migration.ErrorHandling.Skip,
                string s when s.Equals("Mark", StringComparison.OrdinalIgnoreCase) => Migration.ErrorHandling.Mark,
                _ => Migration.ErrorHandling.Fail
            }
        };

        Reset();
        return null;
    }

    private void Reset()
    {
        _header = null;
        _sqlStatements.Clear();
    }

    private static Error? TryParseRun(MigrationHeader header, out Migration.RunMode run)
    {
        run = Migration.RunMode.Once;
        if (header.RunAlways is not null || header.RunOnChange is not null)
            return Errors.ReplacedRunFlags;

        switch (header.Run)
        {
            case null or "":
                return null;
            case string s when s.Equals("once", StringComparison.OrdinalIgnoreCase):
                return null;
            case string s when s.Equals("onChange", StringComparison.OrdinalIgnoreCase):
                run = Migration.RunMode.OnChange;
                return null;
            case string s when s.Equals("always", StringComparison.OrdinalIgnoreCase):
                run = Migration.RunMode.Always;
                return null;
            case string s when s.Equals("never", StringComparison.OrdinalIgnoreCase):
                run = Migration.RunMode.Never;
                return null;
            case string s:
                return Errors.InvalidRunValue(s);
        }
    }

    private static string CalculateHash(IReadOnlyList<string> input)
    {
        var bytes = input.SelectMany(Encoding.UTF8.GetBytes).ToArray();
        return BitConverter.ToString(MD5.HashData(bytes)).Replace("-", string.Empty);
    }

    private sealed class MigrationHeader
    {
        public string? Title { get; set; }
        public string[]? DependsOn { get; set; }
        public string? Run { get; set; }
        public bool? RunAlways { get; set; }
        public bool? RunOnChange { get; set; }
        public bool? RunInTransaction { get; set; }
        public bool? ContextRequired { get; set; }
        public string[]? ContextFilter { get; set; }
        public int? Timeout { get; set; }
        public string? OnError { get; set; }
    }
}
