using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazyolBot2.Text.Runtime;

public class EnvironmentTable {
    readonly List<Dictionary<string, IValue>> _scopes = [];
    int _depth;

    public void Push() {
        _scopes.Add([ ]);
        _depth++;
    }

    public void Pop() {
        if (_depth <= 0)
            return;

        _scopes.RemoveAt(_depth - 1);
        _depth--;
    }

    public IValue Get(string id) {
        for (var i = _depth - 1; i >= 0; i--) {
            if (_scopes[i].TryGetValue(id, out var result))
                return result;
        }

        return new IValue.Null();
    }

    public void Set(string id, IValue value) {
        _scopes[_depth - 1][id] = value;
    }
}
