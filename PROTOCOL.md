# Serial HTML Push Protocol

**Version 1.0**

A device sends markup over a serial link; a host renders it in a browser engine
and sends user interaction back. The device owns the interface entirely —
changing what is shown means changing the device, not the host.

This document is the normative description of the wire format. The host
implementation in this repository is one conforming implementation; the protocol
is not tied to it, to any language, or to any operating system.

---

## 1. Transport

| Property | Value |
|---|---|
| Interface | Asynchronous serial (typically a USB-serial bridge) |
| Baud rate | 115200 by default |
| Framing | 8 data bits, no parity, 1 stop bit |
| Flow control | None |
| Encoding | UTF-8 |
| Record delimiter | Line feed (`0x0A`) |

One frame per line. A record is complete when a line feed is seen. Carriage
returns (`0x0D`) are ignored, so a device emitting CRLF interoperates without
changes.

### 1.1 Frame format

A serial line has no error detection of its own. Every frame therefore carries a
checksum over its payload:

```
<json>|CRC:<checksum>\n
```

| Part | Definition |
|---|---|
| `<json>` | The message object |
| `\|CRC:` | Literal delimiter |
| `<checksum>` | CRC-32 of `<json>`'s UTF-8 bytes, 8 uppercase hex digits, zero padded |

The algorithm is standard IEEE 802.3 CRC-32 — the same polynomial and bit order
as zlib, and as `java.util.zip.CRC32`. No custom variant is involved, so both
ends can use a stock implementation.

```
{"type":"get_page"}|CRC:AE3F5AA2
{"type":"ack"}|CRC:5831CA25
```

**Split on the first delimiter only.** Locate `|CRC:` with an index-of search and
take everything before it as the payload. A payload may legitimately contain that
literal text — splitting on every occurrence rejects exactly those frames while
the sender considers them valid, and both ends then retry a message neither can
agree is well formed.

### 1.2 Verification failure is answered with silence

A frame with no delimiter, or whose checksum does not match, **must be discarded
without any reply** — in particular without an acknowledgement (§3).

This is what makes the link self-correcting. The sender is waiting for an
acknowledgement it never receives, so it retransmits. Acknowledging a frame that
failed verification would instead tell the sender a corrupted message had been
acted on, and the retransmission that would have repaired it never happens.

### 1.3 Non-conforming lines

A device shares its serial link with whatever else writes to it — boot ROM
banners, log output, panic traces. None of that carries a checksum, and a host
**must not** treat it as an error. Any line without a valid frame is ignored for
rendering purposes. Surfacing it in a diagnostic log is recommended, because it
is often the only visible evidence of a device resetting mid-session.

### 1.2 Modem control lines

On many USB-serial bridge modules, DTR and RTS are wired through a transistor
pair to the microcontroller's reset and bootstrap pins. Asserting them on open
therefore resets the device or forces it into its bootloader.

A host **should** leave DTR and RTS deasserted when opening the port, and
**should** expose asserting them as an explicit opt-in for boards that require
it.

### 1.3 Record size

A full page is a single record and may be tens of kilobytes. A host must
reassemble records across arbitrarily fragmented reads and must not assume one
read yields one line. Decoding UTF-8 only once a record is complete avoids
splitting multi-byte sequences.

---

## 2. Message types

Every message is a JSON object with a string `type` field. Unknown types are
ignored, which is what makes the protocol forward-compatible: a device may emit
message types a host predates without breaking it.

### 2.1 Device to host

#### `html` — replace the page

```json
{"type":"html","body":"<html>…</html>"}
```

The complete document to display. Sent in response to `get_page`, and whenever
the device decides the whole interface should change. This tears down and
rebuilds the rendered document, so it **should not** be used to report a changed
value — see `update`.

#### `update` — patch one element

```json
{"type":"update","id":"speed","text":"42"}
```

| Field | Meaning |
|---|---|
| `id` | `id` attribute of the target element in the current document |
| `text` | Replaces the element's text content. Optional. |
| `value` | Replaces the element's `value`, for form controls. Optional. |

Applied in place. No document teardown, no reflow of the whole tree, no visible
flash. This is the correct message for a value that changes periodically.

If no element with that `id` exists, the message is silently discarded.

#### `notify` — transient message

```json
{"type":"notify","id":"temp","title":"Warning","message":"Over temperature"}
```

Shown briefly and dismissed on its own. Requires no acknowledgement. A host is
free to render this as a toast, a status line, or a system notification.

#### `dialog` — message requiring acknowledgement

```json
{"type":"dialog","id":"confirm1","title":"Confirm","message":"Proceed?"}
```

Blocks interaction until dismissed. The host **must** reply once the user
dismisses it, so the device knows the message was actually seen:

```json
{"type":"event","id":"confirm1","action":"dialog_dismiss","value":""}
```

### 2.2 Host to device

#### `get_page` — request the current page

```json
{"type":"get_page"}
```

Optionally naming a specific page where the device serves more than one:

```json
{"type":"get_page","file":"status.html"}
```

#### `event` — report an interaction

```json
{"type":"event","id":"led","action":"click","value":""}
```

| Field | Meaning |
|---|---|
| `id` | Identifier of the element that was interacted with |
| `action` | What happened — `click`, `change`, `dialog_dismiss`, … |
| `value` | Associated value, or an empty string. Always present. |

`action` is deliberately open. A device and its own markup agree on the
vocabulary; the host passes it through without interpreting it.

### 2.3 `ack` — both directions

```json
{"type":"ack"}
```

Retires the sender's in-flight message. Sent by whichever end successfully
received and **acted on** a message, and never sent in reply to an `ack` itself.

Exactly one message is in flight per direction at a time. An acknowledgement
carries no identifier, so it can only be understood as referring to the single
outstanding message — allowing two would make an acknowledgement ambiguous, and
a reply intended for one message would silently retire another.

**Acknowledge after acting, not on receipt.** The point of the acknowledgement is
to tell the sender the content reached its destination, not merely that bytes
were read off the wire.

A sender retransmits an unacknowledged message until it is acknowledged or an
implementation-defined attempt limit is reached. A host that never acknowledges
leaves the device repeating the same frame indefinitely, which is the
characteristic symptom of an unimplemented acknowledgement path.

Requests for the page are idempotent and may be collapsed: a second outstanding
`get_page` asks the same question as the first. Interaction events **must not**
be collapsed — two presses of a button are two distinct intents.

---

## 3. Startup handshake

A raw serial link has no acknowledgement and no retransmission. A device that
sends its page once at boot has no way to know whether anything was listening,
and a host that assumes one will arrive can wait forever.

**The host asks; the device answers.**

1. On opening the port, the host sends `get_page` immediately.
2. If no `html` message has arrived within **3 seconds**, it sends `get_page`
   again.
3. This repeats until an `html` message arrives.

A conforming device **must** handle `get_page` every time it is received, not
only the first. Sending the page once at boot and ignoring later requests is not
conforming and will fail whenever the first request is dropped — which happens
routinely, because the host often opens the port while the device is still
booting.

---

## 4. Rendering model

The protocol does not require any particular renderer, but conforming hosts are
expected to preserve two properties that the design depends on.

### 4.1 No host-specific interface in the page

The device's markup contains nothing host-specific: no injected bridge object,
no custom URL scheme, no platform API. Interaction travels as ordinary HTTP
requests to the host's own loopback endpoint. The same markup therefore renders
identically in an embedded browser control, an external browser, or any other
engine — and a device is never aware of, and never adapts to, which one is in
use.

This is the single most important property of the design. Preserve it.

### 4.2 The shell and the frame

A host serves a small static shell document containing one frame and one
persistent event stream. Pages arrive as stream events and are written into the
frame; patches are applied to elements already inside it.

Keeping the device's document inside a frame is what makes `update` cheap: the
shell holds the stream connection, so patching an element never disturbs it.

Recommended stream events map one-to-one onto the message types:

| Stream event | Payload |
|---|---|
| `page` | The document from an `html` message |
| `update` | `{id, text, value}` |
| `notify` | `{id, title, message}` |
| `dialog` | `{id, title, message}` |
| `clear` | Sent when the link drops, to stop showing a stale page as if live |

### 4.3 Emulating a target display

A host rendering on hardware other than the target should render at the target's
exact pixel dimensions and scale the resulting image, rather than resizing the
viewport. Resizing the viewport changes which layout the page computes — media
queries evaluate differently, text wraps elsewhere — and the result is no longer
what the target displays.

Fixing the frame's size and applying a scale transform to a container preserves
layout exactly while letting the output fill any window.

---

## 5. Versioning

The version at the top of this document identifies the wire format. Adding a new
message type is backward compatible, because unknown types are ignored. Changing
the meaning of an existing field, or the framing, is not, and requires a version
increment.
