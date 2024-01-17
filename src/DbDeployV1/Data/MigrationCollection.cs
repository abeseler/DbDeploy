using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DbDeployV1.Data;

public sealed class MigrationCollection : IDictionary<string, Migration>
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

    public void Add(KeyValuePair<string, Migration> item)
    {
        _migrations.Add(item.Value);
        _migrationsByKey.Add(item.Key, item.Value);
    }

    public void Clear()
    {
        _migrations.Clear();
        _migrationsByKey.Clear();
    }

    public bool Contains(KeyValuePair<string, Migration> item)
    {
        return _migrationsByKey.Contains(item);
    }

    public bool ContainsKey(string key)
    {
        return _migrationsByKey.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<string, Migration>[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<KeyValuePair<string, Migration>> GetEnumerator()
    {
        return _migrationsByKey.GetEnumerator();
    }

    public bool Remove(string key)
    {
        return _migrations.Remove(_migrationsByKey[key]) && _migrationsByKey.Remove(key);
    }

    public bool Remove(KeyValuePair<string, Migration> item)
    {
        return _migrations.Remove(item.Value) && _migrationsByKey.Remove(item.Key);
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out Migration value)
    {
        return _migrationsByKey.TryGetValue(key, out value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _migrations.GetEnumerator();
    }
}
