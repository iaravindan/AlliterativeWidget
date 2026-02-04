using AlliterativeWidget.Models;

namespace AlliterativeWidget.Services;

public interface IContentEngine
{
    DailyContent GenerateContent(DateTime date);
}
