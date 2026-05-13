# Send Email

Send emails via SMTP with HTML or plain text content.

- **Category:** Action
- **Type ID:** `email-send`
- **Icon:** `fa-solid fa-envelope`
- **Base class:** `BaseActionNode`

## Ports

| Direction | Name | Display Name | Required |
|-----------|------|--------------|----------|
| Input | `input` | Input | No |
| Output | `output` | Result | — |

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `to` | string | Yes | Recipient email address(es), comma-separated |
| `subject` | string | Yes | Email subject |
| `body` | string | Yes | Email body content |
| `from` | string | No | Sender email address |
| `cc` | string | No | CC recipients, comma-separated |
| `bcc` | string | No | BCC recipients, comma-separated |
| `isHtml` | boolean | No | Whether body is HTML content (default: `false`) |
| `smtpHost` | string | No | SMTP server hostname |
| `smtpPort` | number | No | SMTP server port (default: 587) |
| `enableSsl` | boolean | No | Enable SSL/TLS (default: `true`) |
| `replyTo` | string | No | Reply-to email address |

## Credentials

When a credential is attached, its fields provide SMTP connection details:

| Credential Field | Usage |
|-----------------|-------|
| `host` | SMTP server hostname (fallback if `smtpHost` not configured) |
| `port` | SMTP server port (fallback if `smtpPort` not configured) |
| `username` | SMTP authentication username (also used as `from` if not configured) |
| `password` | SMTP authentication password |

Configuration values take precedence over credential values for `smtpHost` and `smtpPort`.

## Output Data

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | Always `true` on success |
| `to` | string | Recipient address(es) |
| `subject` | string | Email subject |
| `messageId` | string/null | Message-ID header value |
| `sentAt` | string | UTC timestamp when the email was sent |

## Error Handling

| Error | Output |
|-------|--------|
| SMTP error | `"SMTP error: <message>"` |
| Missing host | `"SMTP host is required"` |
| Missing sender | `"Sender email is required"` |
| Other | `"Email send error: <message>"` |

## Usage

Use the Send Email node when you want to:

- Send notifications based on workflow events
- Deliver reports or summaries
- Alert users about errors or important changes
- Send confirmation emails after processing

## Expression Examples

```
{{$node.EmailSend.data.success}}     // Whether email was sent
{{$node.EmailSend.data.sentAt}}      // When it was sent
{{$node.EmailSend.data.messageId}}   // Message ID for tracking
```

## Notes

- Multiple recipients in `to`, `cc`, and `bcc` are separated by commas.
- The `from` address is required — it can come from the configuration, the credential's `username` field, or must be explicitly set.
- SSL/TLS is enabled by default. Set `enableSsl` to `false` only for local/development SMTP servers.
- HTML emails should include proper HTML structure for best compatibility across email clients.
