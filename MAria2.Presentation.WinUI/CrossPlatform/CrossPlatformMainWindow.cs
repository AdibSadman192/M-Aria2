using System;
using System.Runtime.InteropServices;

namespace MAria2.Presentation.WinUI.CrossPlatform
{
    public abstract class CrossPlatformMainWindow
    {
        public abstract void Initialize();
        public abstract void Show();
        public abstract void Hide();

        public static CrossPlatformMainWindow Create()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsMainWindow();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new MacOSMainWindow();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new LinuxMainWindow();
            }
            
            throw new PlatformNotSupportedException("Unsupported operating system");
        }
    }

    internal class WindowsMainWindow : CrossPlatformMainWindow
    {
        public override void Initialize()
        {
            // Windows-specific initialization
        }

        public override void Show()
        {
            // Windows show logic
        }

        public override void Hide()
        {
            // Windows hide logic
        }
    }

    internal class MacOSMainWindow : CrossPlatformMainWindow
    {
        public override void Initialize()
        {
            // macOS-specific initialization
        }

        public override void Show()
        {
            // macOS show logic
        }

        public override void Hide()
        {
            // macOS hide logic
        }
    }

    internal class LinuxMainWindow : CrossPlatformMainWindow
    {
        public override void Initialize()
        {
            // Linux-specific initialization
        }

        public override void Show()
        {
            // Linux show logic
        }

        public override void Hide()
        {
            // Linux hide logic
        }
    }
}
