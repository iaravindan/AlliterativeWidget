using AlliterativeWidget.Models;

namespace AlliterativeWidget.Services;

public interface IPersistenceService
{
    WidgetConfig LoadConfig();
    void SaveConfig(WidgetConfig config);
    WidgetHistory LoadHistory();
    void SaveHistory(WidgetHistory history);
}
