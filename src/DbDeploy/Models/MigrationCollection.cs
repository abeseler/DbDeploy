using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DbDeploy.Models;

internal sealed class MigrationCollection : IDictionary<string, Migration>
{
    private readonly List<Migration> _migrations = [];
    private readonly Dictionary<string, Migration> _migrationsByKey = [];

    public Migration this[string key]
    {
        get
        {
            return _migrationsByKey[key];
        }
        set
        {
            _migrations.Remove(_migrationsByKey[key]);
            _migrations.Add(value);
            _migrationsByKey[key] = value;
        }
    }

    public ICollection<string> Keys => _migrationsByKey.Keys;
    public ICollection<Migration> Values => _migrations;
    public int Count => _migrations.Count;
    public bool IsReadOnly => true;

    public void Add(string key, Migration value)
    {
        _migrations.Add(value);
        _migrationsByKey.Add(key, value);
    }

    public void AddIntersectionFromRange(IEnumerable<Migration> migrations)
    {
        foreach (var migration in migrations)
        {
            if (_migrationsByKey.ContainsKey(migration.Id))
                continue;

            Add(migration.Id, migration);
        }
    }

    public void Clear()
    {
        _migrations.Clear();
        _migrationsByKey.Clear();
    }

    public void Add(KeyValuePair<string, Migration> item) => Add(item.Key, item.Value);
    public bool Contains(KeyValuePair<string, Migration> item) => _migrationsByKey.Contains(item);
    public bool ContainsKey(string key) => _migrationsByKey.ContainsKey(key);
    public void CopyTo(KeyValuePair<string, Migration>[] array, int arrayIndex) => throw new NotImplementedException();
    public IEnumerator<KeyValuePair<string, Migration>> GetEnumerator() => _migrationsByKey.GetEnumerator();
    public bool Remove(string key) => _migrations.Remove(_migrationsByKey[key]) && _migrationsByKey.Remove(key);
    public bool Remove(KeyValuePair<string, Migration> item) => _migrations.Remove(item.Value) && _migrationsByKey.Remove(item.Key);
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out Migration value) => _migrationsByKey.TryGetValue(key, out value);
    IEnumerator IEnumerable.GetEnumerator() => _migrations.GetEnumerator();
}
