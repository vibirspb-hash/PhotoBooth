using System.Windows;

namespace PhotoBooth.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
