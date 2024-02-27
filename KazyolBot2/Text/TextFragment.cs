using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazyolBot2.Text;

public record TextFragment {
    public string Text { get; set; }
    public string Category { get; set; }
    public ulong AuthorId { get; set; }
    public ulong Id { get; set; }
}
