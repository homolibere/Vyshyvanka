# Vyshyvanka.Plugin.Crypto

Cryptographic operations plugin for Vyshyvanka workflow automation platform.

## Nodes

### Crypto Node

**Type identifier:** `crypto`

Performs cryptographic operations commonly needed for payment gateway integrations: HMAC signature computation, hashing, encoding/decoding, and JWT token generation.

#### Supported Operations

| Operation | Description | Requires Key |
|-----------|-------------|:------------:|
| `hmac-sha256` | HMAC-SHA256 signature | Yes |
| `hmac-sha512` | HMAC-SHA512 signature | Yes |
| `hmac-md5` | HMAC-MD5 signature | Yes |
| `sha256` | SHA-256 hash | No |
| `sha512` | SHA-512 hash | No |
| `md5` | MD5 hash | No |
| `base64-encode` | Base64 encode UTF-8 string | No |
| `base64-decode` | Base64 decode to UTF-8 string | No |
| `hex-encode` | Hex encode UTF-8 string | No |
| `hex-decode` | Hex decode to UTF-8 string | No |

#### Configuration

| Property | Type | Required | Description |
|----------|------|:--------:|-------------|
| `operation` | string | Yes | Operation to perform (see table above) |
| `key` | string | For HMAC | Secret key. Supports credential expressions. |
| `data` | string | No | Data to process. If omitted, uses input port data serialized to string. |
| `encoding` | string | No | Output encoding: `hex` (default), `base64`, `utf8` |

#### Output

```json
{
  "result": "<computed value>",
  "algorithm": "<operation used>",
  "encoding": "<output encoding>"
}
```

#### Usage Examples

**HMAC-SHA256 for payment gateway signing:**
```
operation: "hmac-sha256"
key: "{{ credentials.gateway-secret.secretKey }}"
data: "{{ nodes.<previous>.data.rawBody }}{{ nodes.<trigger>.data.path }}"
encoding: "hex"
```

**SHA-256 hash of a request body:**
```
operation: "sha256"
data: "{{ nodes.<trigger>.data.rawBody }}"
encoding: "hex"
```

## Installation

```bash
dotnet build plugins/Vyshyvanka.Plugin.Crypto
```

Or install via NuGet package management in the Vyshyvanka Designer.
