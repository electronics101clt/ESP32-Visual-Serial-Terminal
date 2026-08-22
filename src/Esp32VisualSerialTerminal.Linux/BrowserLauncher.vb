Imports System.Diagnostics
Imports System.IO

''' <summary>
''' Opens the viewer in a browser window with no browser interface around it.
''' </summary>
''' <remarks>
''' The Windows host embeds WebView2. Linux has no equivalent that can be relied
''' on to be present, so this drives a real browser in application mode instead.
''' Architecturally that is the same arrangement: the page is served from
''' loopback and rendered by a browser engine, exactly as the protocol expects.
''' Nothing about the device's markup changes.
'''
''' A Chromium-family browser is preferred, and not merely because it is common:
''' WebView2 is Chromium, so choosing Chromium here keeps rendering consistent
''' between the two hosts. WebKitGTK-based browsers will display the page, but
''' layout details can differ from what the Windows host shows.
'''
''' Application mode also gives an exact window size with no tab strip or address
''' bar occupying part of it, which matters when the point is to see a target
''' display's dimensions honestly.
''' </remarks>
Public NotInheritable Class BrowserLauncher

    Private Sub New()
    End Sub

    ''' <summary>Chromium-family first, for engine parity with the Windows host.</summary>
    Private Shared ReadOnly ChromiumFamily As String() = {
        "chromium", "chromium-browser", "google-chrome", "google-chrome-stable",
        "brave-browser", "microsoft-edge", "microsoft-edge-stable", "vivaldi"
    }

    Private Shared ReadOnly Fallbacks As String() = {
        "epiphany-browser", "epiphany", "firefox", "xdg-open"
    }

    Public Structure Launch
        Public Process As Process
        Public Command As String
        Public Mode As String
    End Structure

    ''' <summary>
    ''' Starts a browser showing <paramref name="url"/>, sized to the emulated
    ''' display. Returns Nothing when no browser could be found, which is not
    ''' fatal -- the URL can always be opened by hand.
    ''' </summary>
    Public Shared Function Open(url As String, width As Integer, height As Integer,
                                Optional preferred As String = Nothing) As Launch?

        Dim profile = Path.Combine(Path.GetTempPath(), "esp32-visual-serial-terminal-profile")

        Dim candidates As New List(Of String)()
        If Not String.IsNullOrEmpty(preferred) Then candidates.Add(preferred)
        candidates.AddRange(ChromiumFamily)
        candidates.AddRange(Fallbacks)

        For Each candidate In candidates
            Dim exe = Which(candidate)
            If exe Is Nothing Then Continue For

            Dim args As String
            Dim mode As String

            If IsChromiumFamily(candidate) Then
                ' A separate profile directory keeps this window out of an
                ' already-running browser session, which would otherwise open a
                ' tab in the existing window and ignore the size entirely.
                args = $"--app={url} --window-size={width},{height} --user-data-dir=""{profile}"" --no-first-run --no-default-browser-check"
                mode = "application window"
            ElseIf candidate.StartsWith("epiphany", StringComparison.Ordinal) Then
                args = $"--application-mode ""{url}"""
                mode = "application window (WebKit)"
            ElseIf candidate = "firefox" Then
                args = $"--kiosk ""{url}"""
                mode = "kiosk (WebKit-unrelated engine, layout may differ)"
            Else
                args = $"""{url}"""
                mode = "default handler"
            End If

            Try
                Dim psi As New ProcessStartInfo(exe, args) With {
                    .UseShellExecute = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True
                }

                Dim proc = Process.Start(psi)
                If proc Is Nothing Then Continue For

                ' Draining the pipes prevents a chatty browser from filling its
                ' output buffer and stalling.
                proc.BeginOutputReadLine()
                proc.BeginErrorReadLine()

                Return New Launch With {.Process = proc, .Command = candidate, .Mode = mode}

            Catch ex As Exception
                ' Try the next candidate rather than giving up on all of them.
            End Try
        Next

        Return Nothing
    End Function

    Private Shared Function IsChromiumFamily(command As String) As Boolean
        Return ChromiumFamily.Contains(command, StringComparer.Ordinal) OrElse
               command.Contains("chrome") OrElse command.Contains("chromium")
    End Function

    ''' <summary>
    ''' Locates an executable on PATH. Implemented directly rather than by
    ''' shelling out to `which`, which is not guaranteed to be installed on a
    ''' minimal system.
    ''' </summary>
    Public Shared Function Which(command As String) As String
        If String.IsNullOrEmpty(command) Then Return Nothing

        ' An explicit path is used as given.
        If command.Contains("/"c) Then
            Return If(File.Exists(command), command, Nothing)
        End If

        Dim pathVar = Environment.GetEnvironmentVariable("PATH")
        If String.IsNullOrEmpty(pathVar) Then Return Nothing

        ' Not 'dir': that binds to the Dir() function and cannot be assigned.
        For Each folder In pathVar.Split(":"c)
            If String.IsNullOrWhiteSpace(folder) Then Continue For
            Try
                Dim full = Path.Combine(folder, command)
                If File.Exists(full) Then Return full
            Catch ex As Exception
            End Try
        Next

        Return Nothing
    End Function

End Class
