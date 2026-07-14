# Vyshyvanka.Plugin.Crypto

Cryptographic operations plugin for Vyshyvanka workflow automation platform.

## Nodes

### Crypto Node

**Type identifier:** `crypto`

Performs cryptographic operations commonly needed for payment gateway integrations: HMAC signature computation, hashing, encoding/decoding, and AES decryption.

#### Supported Operations

| Operation | Description | Requires Key |
|-----------|-------------|:------------:|
| `hmac-sha1` | HMAC-SHA1 signature | Yes |
| `hmac-sha256` | HMAC-SHA256 signature | Yes |
| `hmac-sha512` | HMAC-SHA512 signature | Yes |
| `hmac-md5` | HMAC-MD5 signature | Yes |
| `sha1` | SHA-1 hash | No |
| `sha256` | SHA-256 hash | No |
| `sha512` | SHA-512 hash | No |
| `md5` | MD5 hash | No |
| `base64-encode` | Base64 encode UTF-8 string | No |
| `base64-decode` | Base64 decode to UTF-8 string | No |
| `hex-encode` | Hex encode UTF-8 string | No |
| `hex-decode` | Hex decode to UTF-8 string | No |
| `aes-gcm-decrypt` | AES-GCM authenticated decryption | Yes |
| `aes-cbc-decrypt` | AES-CBC decryption (PKCS7 padding) | Yes |

#### Configuration

| Property | Type | Required | Description |
|----------|------|:--------:|-------------|
| `operation` | string | Yes | Operation to perform (see table above) |
| `key` | string | For HMAC/AES | Secret key. Supports credential expressions. |
| `data` | string | No | Data to process. If omitted, uses input port data serialized to string. |
| `encoding` | string | No | Output encoding: `hex` (default), `base64` |
| `iv` | string | For AES | Initialization vector or nonce. Hex or Base64 encoded. |
| `inputEncoding` | string | No | Encoding of AES ciphertext input: `base64` (default), `hex` |

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

**SHA-1 checksum for MPay notification verification:**
```
operation: "sha1"
data: "{{ credentials.mpay-merchant.apiKey }}{{ nodes.<trigger>.data.body.timestamp }}{{ nodes.<trigger>.data.body.externalTransactionId }}"
encoding: "hex"
```

## Installation

```bash
dotnet build plugins/Vyshyvanka.Plugin.Crypto
```

Or install via NuGet package management in the Vyshyvanka Designer.
