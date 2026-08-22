# ESP32 Visual Serial Terminal

A terminal where the device sends HTML instead of escape codes.

An ANSI terminal renders whatever the far end sends it — text, colour, cursor
moves — and the far end owns what appears on screen. This works the same way,
with the same division of responsibility. Only the display language differs:
the device sends a page, the terminal renders it at a fixed pixel resolution,
and sends interaction back. Buttons work. Values update in place.

The device owns the entire interface. Nothing about it lives in this
application.

---

## What it does

- **Renders pages pushed over serial** — the device sends HTML, it appears
- **Patches values in place** — no reload, no flash, no flicker
- **Sends interaction back** — clicks and input reach the device as events
- **Renders at a fixed resolution** — 1024×600 by default, presets and custom
- **Scales without distorting layout** — fit-to-window, actual size, fullscreen
- **Shows the raw link** — a serial log, including non-protocol output
- **Opens browser DevTools** — inspect the device's markup live (`F12`)

The emulated viewport is fixed at the chosen pixel size and only the rasterised
output is scaled, so layout is not approximated. Media queries, text wrapping and
element geometry resolve at the configured dimensions whatever size the window
happens to be.

---

## Two hosts, one protocol

| | Windows | Linux |
|---|---|---|
| Window | Windows Forms + WebView2 | Browser in application mode |
| Controls | Menu bar | The terminal it runs in |
| Ships as | `.exe` | Single self-contained executable |

Both drive the **same** `LinkSession`, the same serial transport, the same
loopback server and the same shell page. The protocol exists in one place and
neither host reimplements it, so a device that works against one works against
the other.

---

## Windows

Requires Windows 10 or 11, the
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0), the
[WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
(already present on current installations), and a USB-serial driver for your
board (CP210x, CH340, FTDI, …).

```
git clone <this repository>
cd ESP32-Visual-Serial-Terminal
dotnet build
```

Or open `Esp32VisualSerialTerminal.sln` in Visual Studio — the **.NET desktop
development** workload — and press F5.

---

## Linux

Builds to one self-contained executable. No .NET runtime needs to be installed
on the machine that runs it.

```
git clone <this repository>
cd ESP32-Visual-Serial-Terminal
./linux/build.sh          # needs the .NET 10 SDK
./linux/install.sh        # optional: ~/.local/bin + a desktop entry
```

Then:

```
esp32-visual-serial-terminal                 # auto-detect, 1024x600
esp32-visual-serial-terminal -s 800x480
esp32-visual-serial-terminal -p /dev/ttyUSB0 -b 115200
esp32-visual-serial-terminal --list-ports
esp32-visual-serial-terminal --help
```

The terminal is the control surface in place of a menu bar:

```
c  connect        d  disconnect     r  request page
x  clear view     l  toggle log     s  status          q  quit
```

**Serial permissions.** Serial devices belong to the `dialout` group, and a user
outside it gets a permission error that reads like a missing device. If a port
will not open:

```
sudo usermod -aG dialout $USER
```

then log out and back in. `--list-ports` marks devices you cannot currently open.

**Which browser.** A Chromium-family browser is preferred and used
automatically when present, because WebView2 is Chromium and picking the same
engine keeps rendering consistent between the two hosts. It is launched with
`--app` at the exact emulated size, so there is no tab strip or address bar
taking up part of the window. WebKit-based browsers display the page correctly
but can differ in layout details. Override with `--browser <command>`, or use
`--no-browser` and open the printed address yourself.

---

## Using it

1. Plug the board in.
2. **Connection → Serial Port** and pick the port. It connects automatically
   when a port appears, if that option is left enabled.
3. The page appears once the device answers.

Set the rendered size under **View → Device Resolution**. `F11` is fullscreen,
`Escape` leaves it. `Ctrl+0` resizes the window so the rendered area is exactly
the configured pixel count.

If nothing appears, open **Tools → Serial Log** (`Ctrl+L`). Frames the
application sends are prefixed `>`, received frames `<`, and anything the device
prints that is not protocol traffic is shown unprefixed.

### If the board resets when you connect

Some boards wire DTR and RTS to the reset and bootstrap pins. Both are left
deasserted on open for that reason. Boards that instead require DTR held active
can enable it under **Connection**.

---

## The protocol

Newline-delimited JSON at 115200 baud, each frame carrying a CRC-32 of its
payload:

```
{"type":"html","body":"<html>…</html>"}|CRC:1A2B3C4D
{"type":"update","id":"speed","text":"42"}|CRC:5E6F7A8B
```

and in the other direction:

```
{"type":"get_page"}|CRC:AE3F5AA2
{"type":"event","id":"led","action":"click","value":""}|CRC:9C0D1E2F
```

Three rules matter more than the rest:

- **The checksum is mandatory in both directions.** A frame without one is
  ignored, so a host sending bare JSON receives silence and never gets a page.
- **A frame that fails verification is answered with silence**, never an
  acknowledgement — that silence is what makes the sender retransmit.
- **Every acted-on message is acknowledged** with `{"type":"ack"}`. Without it
  the sender repeats the same frame indefinitely.

The host requests the page and keeps requesting until it arrives, rather than
assuming the device sent one at the right moment.

**[PROTOCOL.md](PROTOCOL.md) is the full specification.** It is written to be
implemented independently — the wire format is not tied to this application, to
Windows, or to any language.

The device's markup reaches the host's own endpoint with a plain `fetch`. There
is no bridge object and no host-specific API, which is why the same markup
renders in any engine.

---

## How it is put together

```
PROTOCOL.md                          the specification

src/Esp32VisualSerialTerminal/       shared core, plus the Windows host
  Protocol/                          message types, framing, CRC-32
  Transport/                         the serial link and record reassembly
  Server/                            loopback HTTP server and the push stream
  Session/LinkSession.vb             the protocol itself, host-independent
  Shell/shell.html                   frame, event stream, scaling
  Form1.vb                           the Windows window
  SerialLog.vb, StatusDialog.vb, ViewportDialog.vb

src/Esp32VisualSerialTerminal.Linux/ the Linux host
  Program.vb                         terminal control surface
  LinuxSerialPorts.vb                /dev enumeration
  BrowserLauncher.vb                 application-mode browser window

linux/                               build.sh, install.sh, desktop entry
```

The first four of those are free of any user-interface dependency. The Linux
project compiles them **by reference, not by copy** — it links the same files
rather than holding its own versions. A reference implementation carrying two
divergent implementations of its own specification would leave the specification
answering to neither.

`shell.html` is a plain file served verbatim rather than a string baked into a
binary, so it can be dropped into any host implementing the same endpoints.

Everything lives in the project's root namespace, and on Windows the Application
Framework starts `Form1` — an ordinary VB Windows Forms application with no
custom entry point. `My Project/Application.Designer.vb` is kept in source rather
than generated, because the Visual Studio generator that normally produces it
does not run under `dotnet build`.

---

## Contributing

Changes to the protocol belong in `PROTOCOL.md` first — the specification is what
other implementations follow, and code that diverges from it silently is worse
than no change. Bump the version when the wire format changes meaning.

---

## License

Apache License 2.0. See [LICENSE](LICENSE).
