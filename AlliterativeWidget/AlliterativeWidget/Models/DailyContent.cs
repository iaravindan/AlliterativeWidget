namespace AlliterativeWidget.Models;

public class DailyContent
{
    public string Headline { get; set; } = "";
    public string Punchline { get; set; } = "";
    public string DailyKey { get; set; } = "";

    public DailyContent() { }

    public DailyContent(string headline, string punchline, string dailyKey)
    {
        Headline = headline;
        Punchline = punchline;
        DailyKey = dailyKey;
    }
}
