using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazyolBot2.Text.Runtime; 

public interface IValue {
    class Null : IValue { public override string ToString() => ""; }
    record Str(string Value) : IValue {
        public override string ToString() => Value;
    }

    record Num(int Value) : IValue {
        public override string ToString() => Value.ToString();
    }

    record TemplateResult : IValue {
        public string Value { get; set; }
        public override string ToString() => Value;
    }
}
