using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazyolBot2.Text.Runtime; 

public interface IValue {
    class Null : IValue { public override string ToString() => ""; }
    record Str(string Value) : IValue {
        public override string ToString() => Value;
    }

    record Num(double Value) : IValue {
        public override string ToString() => Value.ToString();
    }

    record TemplateResult : IValue {
        public string Value { get; set; }
        public Stream ImageStream { get; set; }
        public string ImageFormat { get; set; }
        public override string ToString() => Value;
    }

    record Table(Dictionary<string, IValue> Values) : IValue {
        public override string ToString() => $"({string.Join(", ", Values.Select(p => $"{p.Key}: {p.Value}"))})";
    }
}
