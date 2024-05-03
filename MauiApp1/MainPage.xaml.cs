using MauiApp1.ViewModel;

namespace MauiApp1
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainViewModel vm)
        {
#if ANDROID
            InitializeComponent();
#endif
            BindingContext = vm;
        }
    }

}
