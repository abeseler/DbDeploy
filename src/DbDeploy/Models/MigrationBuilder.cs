using System.Text;

namespace DbDeploy.Models;

internal sealed class MigrationBuilder(string file)
{
    public string FileName = file;
    public string? Title { get; private set; }
    public bool RunAlways { get; private set; }
    public bool RunOnChange { get; private set; }
    public bool RunInTransaction { get; private set; } = true;
    public int Timeout { get; private set; } = 30;
    public Migration.ErrorHandling OnError { get; private set; } = Migration.ErrorHandling.Fail;
    public List<string> SqlStatements { get; private init; } = [];
    public StringBuilder SqlStatementBuilder { get; private set; } = new StringBuilder();

    public void AddKeyValuePair(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {        
        if (key.Equals("Title", StringComparison.OrdinalIgnoreCase))
        {
            Title = value.ToString();
        }
        else if (key.Equals("RunOnChange", StringComparison.OrdinalIgnoreCase))
        {
            RunOnChange = bool.TryParse(value, out var result) && result;
        }
        else if (key.Equals("RunAlways", StringComparison.OrdinalIgnoreCase))
        {
            RunAlways = bool.TryParse(value, out var result) && result;
        }
        else if (key.Equals("RunInTransaction", StringComparison.OrdinalIgnoreCase))
        {
            RunInTransaction = bool.TryParse(value, out var result) && result;
        }
        else if (key.Equals("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            Timeout = int.TryParse(value, out var result) ? result : 30;
        }
        else if (key.Equals("OnError", StringComparison.OrdinalIgnoreCase))
        {
            OnError = Enum.TryParse<Migration.ErrorHandling>(value, true, out var result) ? result : Migration.ErrorHandling.Fail;
        }
    }

    public void AddToSql(ReadOnlySpan<char> sql)
    {
        if (sql.StartsWith("GO", StringComparison.OrdinalIgnoreCase))
        {
            if (SqlStatementBuilder.Length > 0)
            {
                SqlStatements.Add(SqlStatementBuilder.ToString());
            }
            SqlStatementBuilder.Clear();
        }
        else
        {
            SqlStatementBuilder.Append(sql.TrimEnd());
        }
    }

    public Migration? Build()
    {
        if (SqlStatementBuilder.Length > 0)
        {
            SqlStatements.Add(SqlStatementBuilder.ToString());
            SqlStatementBuilder.Clear();
        }
        var result = Title is not null && SqlStatements.Count > 0 ? new Migration()
        {
            FileName = FileName,
            Title = Title,
            SqlStatements = [.. SqlStatements],
            Hash = "",
            RunAlways = RunAlways,
            RunOnChange = RunOnChange,
            Timeout = Timeout,
            OnError = OnError
        } : null;

        Title = null;
        SqlStatements.Clear();
        RunAlways = false;
        RunOnChange = false;
        RunInTransaction = true;
        Timeout = 30;
        OnError = Migration.ErrorHandling.Fail;

        return result;
    }
}
