using Mu3Library.Editor.Window;
using UnityEditor;

namespace Mu3Library.Sample.UtilWindow
{
    public class UtilWindow : Mu3Window
    {
        private const string WindowName = "Util Window";
        private const string MenuName = MenuRoot + "/" + WindowName;



        [MenuItem(MenuName)]
        public static void ShowWindow()
        {
            GetWindow(typeof(UtilWindow), false, WindowName);
        }
    }
}