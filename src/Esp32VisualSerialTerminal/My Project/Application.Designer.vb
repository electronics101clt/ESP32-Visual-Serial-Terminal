'------------------------------------------------------------------------------
' Startup configuration for the VB Application Framework.
'
' Visual Studio normally generates this from Application.myapp via a file
' generator that only runs inside the IDE -- "dotnet build" does not run it, so
' a project relying on generation alone starts with no main form and exits
' immediately. Keeping it in source makes the startup path explicit and builds
' identically from the IDE and the command line.
'
' Keep in step with Application.myapp if that file is edited through the
' project's Application property page.
'------------------------------------------------------------------------------

Option Strict On
Option Explicit On

Namespace My

    Partial Friend Class MyApplication

        <Global.System.Diagnostics.DebuggerStepThroughAttribute()>
        Public Sub New()
            MyBase.New(Global.Microsoft.VisualBasic.ApplicationServices.AuthenticationMode.Windows)
            Me.IsSingleInstance = False
            Me.EnableVisualStyles = True
            Me.SaveMySettingsOnExit = True
            Me.ShutDownStyle = Global.Microsoft.VisualBasic.ApplicationServices.ShutdownMode.AfterMainFormCloses
            Me.HighDpiMode = Global.System.Windows.Forms.HighDpiMode.PerMonitorV2
        End Sub

        <Global.System.Diagnostics.DebuggerStepThroughAttribute()>
        Protected Overrides Sub OnCreateMainForm()
            Me.MainForm = New Global.Esp32VisualSerialTerminal.Form1()
        End Sub

    End Class

End Namespace
