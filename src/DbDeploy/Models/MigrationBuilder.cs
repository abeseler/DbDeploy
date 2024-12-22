using System.Security.Cryptography;

namespace DbDeploy.Models;

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

    public void AddHeader(string input)
    {
        _header = JsonSerializer.Deserialize<MigrationHeader>(input, jsonOptions);
    }

    public void AddToSql(string input)
    {
        if (input.StartsWith("--NewStatement", StringComparison.OrdinalIgnoreCase))
        {
            if (_stringBuilder.Length > 0)
            {
                var sql = _stringBuilder.ToString().Trim();
                _sqlStatements.Add(sql);
            }
            _stringBuilder.Clear();
            return;
        }

        _stringBuilder.AppendLine(input);
    }

    public Migration? Build()
    {
        if (_stringBuilder.Length > 0)
        {
            _sqlStatements.Add(_stringBuilder.ToString());
            _stringBuilder.Clear();
        }
        var result = _header is { Title: not null } && _sqlStatements.Count > 0 ? new Migration()
        {
            FileName = file,
            Title = _header.Title,
            SqlStatements = [.. _sqlStatements],
            Hash = CalculateHash(_sqlStatements),
            RunAlways = _header.RunAlways ?? false,
            RunOnChange = _header.RunOnChange ?? false,
            RunInTransaction = _header.RunInTransaction ?? true,
            RequiresContext = requiresContext || (_header.RequireContext ?? false),
            ContextFilter = [.. _header.ContextFilter ?? [], .. contextFilter],
            Timeout = _header.Timeout ?? 30,
            OnError = _header.OnError switch
            {
                string s when s.Equals("Skip", StringComparison.OrdinalIgnoreCase) => Migration.ErrorHandling.Skip,
                string s when s.Equals("Mark", StringComparison.OrdinalIgnoreCase) => Migration.ErrorHandling.Mark,
                _ => Migration.ErrorHandling.Fail
            }
        } : null;

        _header = null;
        _sqlStatements.Clear();

        return result;
    }

    private static string CalculateHash(IReadOnlyList<string> input)
    {
        var bytes = input.SelectMany(Encoding.UTF8.GetBytes).ToArray();
        return BitConverter.ToString(MD5.HashData(bytes)).Replace("-", string.Empty);
    }

    private sealed class MigrationHeader
    {
        public string? Title { get; set; }
        public bool? RunAlways { get; set; }
        public bool? RunOnChange { get; set; }
        public bool? RunInTransaction { get; set; }
        public bool? RequireContext { get; set; }
        public int? Timeout { get; set; }
        public string[]? ContextFilter { get; set; }
        public string? OnError { get; set; }
    }
}
