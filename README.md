# "Screen Control App", a generic name for an app that does TeamViewer things in WPF via SignalR

The backend is written in ASP.NET Core (.NET 8) and sets up a SignalR hub at `http://localhost:5026/screenControlHub`. It can be hosted independently anywhere.\
Because WPF only supports Windows and the app requires access to WinAPI functions to control your peer's screen, the controlling side's frontend is only compatible with Windows.

