using System.Text;

namespace DbDeploy.Models;

internal sealed class MigrationBuilder(string file, string[] contextFilter, bool contextRequired = false)
{
    private readonly HashSet<string> _contextFilter = [];
    private string? _title;
    private bool _runAlways;
    private bool _runOnChange;
    private bool _runInTransaction = true;
    private bool _contextRequired = contextRequired;
    private int _timeout = 30;
    private Migration.ErrorHandling _onError = Migration.ErrorHandling.Fail;
    private readonly List<string> _sqlStatements = [];
    private readonly StringBuilder _sqlStatementBuilder = new StringBuilder();

    public void AddKeyValuePair(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {        
        if (key.Equals("Title", StringComparison.OrdinalIgnoreCase))
        {
            _title = value.ToString();
        }
        else if (key.Equals("RunOnChange", StringComparison.OrdinalIgnoreCase))
        {
            _runOnChange = bool.TryParse(value, out var result) && result;
        }
        else if (key.Equals("RunAlways", StringComparison.OrdinalIgnoreCase))
        {
            _runAlways = bool.TryParse(value, out var result) && result;
        }
        else if (key.Equals("RunInTransaction", StringComparison.OrdinalIgnoreCase))
        {
            _runInTransaction = bool.TryParse(value, out var result) && result;
        }
        else if (key.Equals("ContextRequired", StringComparison.OrdinalIgnoreCase))
        {
            _contextRequired = bool.TryParse(value, out var result) && result;
        }
        else if (key.Equals("ContextFilter", StringComparison.OrdinalIgnoreCase))
        {
            
        }
        else if (key.Equals("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            _timeout = int.TryParse(value, out var result) ? result : 30;
        }
        else if (key.Equals("OnError", StringComparison.OrdinalIgnoreCase))
        {
            _onError = Enum.TryParse<Migration.ErrorHandling>(value, true, out var result) ? result : Migration.ErrorHandling.Fail;
        }
    }

    public void AddToSql(ReadOnlySpan<char> sql)
    {
        if (sql.StartsWith("GO", StringComparison.OrdinalIgnoreCase))
        {
            if (_sqlStatementBuilder.Length > 0)
            {
                _sqlStatements.Add(_sqlStatementBuilder.ToString());
            }
            _sqlStatementBuilder.Clear();
        }
        else
        {
            _sqlStatementBuilder.Append(sql.TrimEnd());
        }
    }

    public Migration? Build()
    {
        if (_sqlStatementBuilder.Length > 0)
        {
            _sqlStatements.Add(_sqlStatementBuilder.ToString());
            _sqlStatementBuilder.Clear();
        }
        foreach (var context in contextFilter)
        {
            _contextFilter.Add(context);
        }
        var result = _title is not null && _sqlStatements.Count > 0 ? new Migration()
        {
            FileName = file,
            Title = _title,
            SqlStatements = [.. _sqlStatements],
            Hash = "",
            RunAlways = _runAlways,
            RunOnChange = _runOnChange,
            RunInTransaction = _runInTransaction,
            RequireContext = _contextRequired,
            ContextFilter = [.. _contextFilter],
            Timeout = _timeout,
            OnError = _onError
        } : null;

        _title = null;
        _sqlStatements.Clear();
        _runAlways = false;
        _runOnChange = false;
        _runInTransaction = true;
        _contextRequired = false;
        _contextFilter.Clear();
        _timeout = 30;
        _onError = Migration.ErrorHandling.Fail;

        return result;
    }
}
