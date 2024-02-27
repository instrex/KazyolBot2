using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazyolBot2.Images;

public class SavedImageData {
    public ulong AuthorId { get; set; }
    public ulong Id { get; set; }
    public string Name { get; set; }
    public HashSet<string> Categories { get; set; }
}
