namespace KazyolBot2.Text.Expressions;

public class TextComponentInfo {
    public record Param(string Id, string Desc = default, string[] Hints = default, bool CanBeNull = false, bool CanBeMultiple = false);

    public string Codename { get; set; }
    public HashSet<string> Id { get; set; }
    public HashSet<string> ResultHints { get; set; }
    public List<Param> Params { get; set; }
    public string Desc { get; set; }
}
