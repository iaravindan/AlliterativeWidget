namespace AlliterativeWidget.Services;

public interface IWindowPositionService
{
    (int X, int Y) CalculateTopRightPosition(IntPtr hwnd, int windowWidth, int windowHeight, int padding);
}
