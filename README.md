# ESP32 Visual Serial Terminal

A serial terminal that renders a user interface instead of scrolling text.

The connected device sends HTML over the wire; this displays it at the exact
pixel dimensions of a chosen resolution and sends interaction back. Buttons
work. Values update in place.

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

## Requirements

- Windows 10 or 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
  — already present on current Windows installations
- A USB-serial driver for your board (CP210x, CH340, FTDI, …)

To build, the .NET 10 SDK, or Visual Studio with the **.NET desktop
development** workload.

---

## Building

```
git clone <this repository>
cd ESP32-Visual-Serial-Terminal
dotnet build
```

Or open `Esp32VisualSerialTerminal.sln` in Visual Studio and press F5.

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
PROTOCOL.md            the specification
Shell/shell.html       the shell document: frame, event stream, scaling
Protocol/              message types, framing, CRC-32 — no UI, no I/O
Transport/             the serial link and record reassembly
Server/                loopback HTTP server and the push stream
Form1.vb               the main window
SerialLog.vb           the raw link viewer
ViewportDialog.vb      custom resolution prompt
My Project/            Application Framework startup
```

`Protocol/` has no dependency on the transport or on Windows, and `shell.html` is
a plain file served verbatim rather than a string baked into the application, so
it can be dropped into any host implementing the same endpoints.

Everything lives in the project's root namespace, and the Application Framework
starts `Form1` — an ordinary VB Windows Forms application with no custom entry
point. `My Project/Application.Designer.vb` is kept in source rather than
generated, because the Visual Studio generator that normally produces it does not
run under `dotnet build`.

---

## Contributing

Changes to the protocol belong in `PROTOCOL.md` first — the specification is what
other implementations follow, and code that diverges from it silently is worse
than no change. Bump the version when the wire format changes meaning.

---

## License

Apache License 2.0. See [LICENSE](LICENSE).
